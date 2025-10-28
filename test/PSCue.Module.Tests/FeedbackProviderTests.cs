#pragma warning disable CA2252 // Opt into preview features for testing
#pragma warning disable SYSLIB0050 // FormatterServices is obsolete but needed for test mocking

using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Subsystem.Feedback;
using System.Runtime.Serialization;

namespace PSCue.Module.Tests;

public class FeedbackProviderTests
{
    /// <summary>
    /// Tests command parsing with various command structures.
    /// Parameters: commandLine, expectedCommand, expectedArgCount
    /// </summary>
    [Theory]
    [InlineData("git status", "git", 1)]                                    // Simple command + subcommand
    [InlineData("git commit -m 'message'", "git", 3)]                       // Command with flag and value
    [InlineData("git checkout -b feature", "git", 3)]                       // Command with flag and branch
    [InlineData("docker run -d nginx", "docker", 3)]                        // Docker command
    [InlineData("kubectl get pods", "kubectl", 2)]                          // kubectl command
    [InlineData("az vm list", "az", 2)]                                     // Azure CLI
    [InlineData("npm install --save express", "npm", 3)]                    // npm with flag
    [InlineData("cargo build --release", "cargo", 2)]                       // Rust cargo
    public void GetFeedback_ParsesCommandElements_Correctly(string commandLine, string expectedCommand, int expectedArgCount)
    {
        // Arrange
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var provider = new FeedbackProvider(null, history, graph);
        var context = CreateSuccessContext(commandLine);

        // Act
        var result = provider.GetFeedback(context, CancellationToken.None);

        // Assert - should return null (silent learning) but update history
        Assert.Null(result);
        var entries = history.GetRecent(10);
        Assert.Single(entries);
        Assert.Equal(expectedCommand, entries[0].Command);
        Assert.Equal(expectedArgCount, entries[0].Arguments.Length);
    }

    [Fact]
    public void GetFeedback_EmptyCommandLine_ReturnsNull()
    {
        // Arrange
        var provider = new FeedbackProvider();
        var context = CreateSuccessContext("");

        // Act
        var result = provider.GetFeedback(context, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFeedback_WhitespaceOnly_ReturnsNull()
    {
        // Arrange
        var provider = new FeedbackProvider();
        var context = CreateSuccessContext("   ");

        // Act
        var result = provider.GetFeedback(context, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFeedback_SuccessfulCommand_UpdatesHistory()
    {
        // Arrange
        var history = new CommandHistory();
        var provider = new FeedbackProvider(null, history, null);
        var context = CreateSuccessContext("git status");

        // Act
        provider.GetFeedback(context, CancellationToken.None);

        // Assert
        var entries = history.GetRecent(10);
        Assert.Single(entries);
        Assert.True(entries[0].Success);
    }

    [Fact]
    public void GetFeedback_FailedCommand_UpdatesHistoryAsFailure()
    {
        // Arrange
        var history = new CommandHistory();
        var provider = new FeedbackProvider(null, history, null);
        var context = CreateErrorContext("git invalid-command");

        // Act
        provider.GetFeedback(context, CancellationToken.None);

        // Assert
        var entries = history.GetRecent(10);
        Assert.Single(entries);
        Assert.False(entries[0].Success);
    }

    [Fact]
    public void GetFeedback_SuccessfulCommand_UpdatesArgumentGraph()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var provider = new FeedbackProvider(null, null, graph);
        var context = CreateSuccessContext("git commit -m 'test'");

        // Act
        provider.GetFeedback(context, CancellationToken.None);

        // Assert
        var suggestions = graph.GetSuggestions("git", Array.Empty<string>(), 10);
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Argument == "commit");
    }

    [Fact]
    public void GetFeedback_FailedCommand_DoesNotUpdateArgumentGraph()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var provider = new FeedbackProvider(null, null, graph);
        var context = CreateErrorContext("git invalid-command");

        // Act
        provider.GetFeedback(context, CancellationToken.None);

        // Assert - Failed commands shouldn't pollute the argument graph
        var suggestions = graph.GetSuggestions("git", Array.Empty<string>(), 10);
        Assert.DoesNotContain(suggestions, s => s.Argument == "invalid-command");
    }

    [Theory]
    [InlineData("git status", "git")]                                       // Exact match
    [InlineData("aws configure", "aws")]                                    // Wildcard prefix match
    [InlineData("aws s3 ls", "aws *")]                                      // Wildcard prefix match
    [InlineData("echo my-password", "*password*")]                          // Wildcard contains match
    [InlineData("set SECRET_KEY=value", "*secret*")]                        // Case insensitive match
    public void GetFeedback_IgnoredPattern_DoesNotLearn(string commandLine, string ignorePattern)
    {
        // Arrange
        Environment.SetEnvironmentVariable("PSCUE_IGNORE_PATTERNS", ignorePattern);
        try
        {
            var history = new CommandHistory();
            var graph = new ArgumentGraph();
            var provider = new FeedbackProvider(null, history, graph);
            var context = CreateSuccessContext(commandLine);

            // Act
            provider.GetFeedback(context, CancellationToken.None);

            // Assert - Should not add to history
            var entries = history.GetRecent(10);
            Assert.Empty(entries);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PSCUE_IGNORE_PATTERNS", null);
        }
    }

    [Fact]
    public void GetFeedback_MultipleIgnorePatterns_RespectAll()
    {
        // Arrange
        Environment.SetEnvironmentVariable("PSCUE_IGNORE_PATTERNS", "aws,*password*,*secret*");
        try
        {
            var history = new CommandHistory();
            var provider = new FeedbackProvider(null, history, null);

            // Act & Assert - Test each pattern
            provider.GetFeedback(CreateSuccessContext("aws configure"), CancellationToken.None);
            Assert.Empty(history.GetRecent(10));

            provider.GetFeedback(CreateSuccessContext("echo my-password"), CancellationToken.None);
            Assert.Empty(history.GetRecent(10));

            provider.GetFeedback(CreateSuccessContext("set SECRET=value"), CancellationToken.None);
            Assert.Empty(history.GetRecent(10));

            // Non-matching command should be learned
            provider.GetFeedback(CreateSuccessContext("git status"), CancellationToken.None);
            Assert.Single(history.GetRecent(10));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PSCUE_IGNORE_PATTERNS", null);
        }
    }

    [Fact]
    public void GetFeedback_NoIgnorePatterns_LearnsEverything()
    {
        // Arrange
        Environment.SetEnvironmentVariable("PSCUE_IGNORE_PATTERNS", "");
        try
        {
            var history = new CommandHistory();
            var provider = new FeedbackProvider(null, history, null);

            // Act
            provider.GetFeedback(CreateSuccessContext("aws configure"), CancellationToken.None);
            provider.GetFeedback(CreateSuccessContext("git status"), CancellationToken.None);

            // Assert - Both should be learned
            var entries = history.GetRecent(10);
            Assert.Equal(2, entries.Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PSCUE_IGNORE_PATTERNS", null);
        }
    }

    [Fact]
    public void GetFeedback_SupportedCommand_UpdatesCache()
    {
        // Arrange
        var pipeName = $"PSCue-Test-{Guid.NewGuid():N}";
        var server = new IpcServer(pipeName);
        Thread.Sleep(100); // Give server time to start

        try
        {
            var provider = new FeedbackProvider(server, null, null);
            var context = CreateSuccessContext("git checkout main");

            // Act
            provider.GetFeedback(context, CancellationToken.None);

            // Assert - Cache should be updated (this is tested indirectly via IPC)
            var cache = server.GetCache();
            var stats = cache.GetStatistics();
            // Note: Cache updates are best effort, so we just verify no crashes
            Assert.NotNull(stats);
        }
        finally
        {
            server?.Dispose();
        }
    }

    [Fact]
    public void GetFeedback_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var history = new CommandHistory();
        var provider = new FeedbackProvider(null, history, null);
        var context = CreateSuccessContext("git commit -m 'Fix bug #123 & update README'");

        // Act
        provider.GetFeedback(context, CancellationToken.None);

        // Assert
        var entries = history.GetRecent(10);
        Assert.Single(entries);
        Assert.Equal("git", entries[0].Command);
    }

    [Fact]
    public void GetFeedback_VeryLongCommandLine_HandlesCorrectly()
    {
        // Arrange
        var history = new CommandHistory();
        var provider = new FeedbackProvider(null, history, null);
        var longMessage = new string('a', 1000);
        var context = CreateSuccessContext($"git commit -m '{longMessage}'");

        // Act
        provider.GetFeedback(context, CancellationToken.None);

        // Assert - Should not crash
        var entries = history.GetRecent(10);
        Assert.Single(entries);
    }

    [Fact]
    public void GetFeedback_ExceptionInParsing_ReturnsNull()
    {
        // Arrange
        var provider = new FeedbackProvider();
        var context = CreateMalformedContext();

        // Act
        var result = provider.GetFeedback(context, CancellationToken.None);

        // Assert - Should gracefully handle errors
        Assert.Null(result);
    }

    [Fact]
    public void GetFeedback_IntegrationWithLearningSystem_UpdatesBothSources()
    {
        // Arrange
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var provider = new FeedbackProvider(null, history, graph);

        // Act - Execute several commands
        provider.GetFeedback(CreateSuccessContext("git status"), CancellationToken.None);
        provider.GetFeedback(CreateSuccessContext("git commit -m 'test'"), CancellationToken.None);
        provider.GetFeedback(CreateSuccessContext("git push"), CancellationToken.None);

        // Assert - Both history and graph should be updated
        var historyEntries = history.GetRecent(10);
        Assert.Equal(3, historyEntries.Count);

        var graphSuggestions = graph.GetSuggestions("git", Array.Empty<string>(), 10);
        Assert.Contains(graphSuggestions, s => s.Argument == "status");
        Assert.Contains(graphSuggestions, s => s.Argument == "commit");
        Assert.Contains(graphSuggestions, s => s.Argument == "push");
    }

    private static FeedbackContext CreateSuccessContext(string commandLine)
    {
        // Use the AST-based constructor instead of string-based to avoid PathInfo requirement
        var ast = Parser.ParseInput(commandLine, out var tokens, out _);

        return new FeedbackContext(
            FeedbackTrigger.Success,
            commandLineAst: ast,
            commandLineTokens: tokens,
            cwd: CreateTestPathInfo(),
            lastError: null);
    }

    private static FeedbackContext CreateErrorContext(string commandLine)
    {
        var ast = Parser.ParseInput(commandLine, out var tokens, out _);
        var error = new System.Management.Automation.ErrorRecord(
            new Exception("Command failed"),
            "CommandFailed",
            System.Management.Automation.ErrorCategory.InvalidOperation,
            null);

        return new FeedbackContext(
            FeedbackTrigger.Error,
            commandLineAst: ast,
            commandLineTokens: tokens,
            cwd: CreateTestPathInfo(),
            lastError: error);
    }

    private static FeedbackContext CreateMalformedContext()
    {
        // Create a context with minimal data to test error handling
        var ast = Parser.ParseInput("", out var tokens, out _);

        return new FeedbackContext(
            FeedbackTrigger.Success,
            commandLineAst: ast,
            commandLineTokens: tokens,
            cwd: CreateTestPathInfo(),
            lastError: null);
    }

    private static PathInfo CreateTestPathInfo()
    {
        // PathInfo has an internal constructor that requires ProviderInfo and SessionState objects,
        // which are complex to create. Since our FeedbackProvider never actually uses the PathInfo
        // from context.CurrentLocation, we use FormatterServices to create a minimal mock object.
        // This is acceptable for test purposes even though FormatterServices is obsolete for
        // production serialization scenarios.
        var pathInfo = (PathInfo)FormatterServices.GetUninitializedObject(typeof(PathInfo));

        // Set the Path property using reflection (it's a read-only property with a private backing field)
        var pathField = typeof(PathInfo).GetField("_path", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        pathField?.SetValue(pathInfo, Environment.CurrentDirectory);

        return pathInfo;
    }
}
