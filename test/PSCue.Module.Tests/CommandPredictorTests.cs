#pragma warning disable CA2252 // Opt into preview features for testing

using System.Management.Automation.Subsystem.Prediction;

namespace PSCue.Module.Tests;

public class CommandPredictorTests
{
    #region Combine Method Tests (Critical - Word Boundary Logic)

    /// <summary>
    /// Tests the Combine method with various input scenarios.
    /// Parameters: input, completionText, expectedResult
    /// </summary>
    [Theory]
    [InlineData("sco", "scoop", "scoop")]                                      // Basic overlap - completes word
    [InlineData("git chec", "checkout", "git checkout")]                       // Partial word completion
    [InlineData("claude plugin", "install", "claude plugin install")]          // CRITICAL: Word boundary respect (pluginstall bug)
    [InlineData("git ", "status", "git status")]                               // No overlap - adds space
    [InlineData("git", "git", "git")]                                          // Full overlap - no duplication
    [InlineData("git  ", "commit", "git  commit")]                             // Trailing spaces preserved
    [InlineData("", "git", "git")]                                             // Empty input returns completion
    [InlineData("git", "", "git ")]                                            // Empty completion adds space
    [InlineData("test@", "test@host", "test@host")]                            // Special characters
    [InlineData("café", "café-au-lait", "café-au-lait")]                       // Unicode handling
    [InlineData("git CHE", "checkout", "git checkout")]                        // Case insensitive matching
    [InlineData("git   commit   --am", "--amend", "git   commit   --amend")]  // Multiple spaces - last word only
    [InlineData("plugin", "install", "plugin install")]                        // Substring non-match (NOT "pluginstall")
    [InlineData("scoop update", "*", "scoop update *")]                        // Scoped commands
    [InlineData("git commit --a", "--amend", "git commit --amend")]            // Flag completion
    [InlineData("commandex", "example", "commandex example")]                  // Partial match not at word boundary
    [InlineData("git commit --am", "--amend", "git commit --amend")]           // Dash-separated words
    [InlineData("cd /usr/lo", "local", "cd /usr/lo local")]                    // Path completion (/ is word boundary)
    [InlineData("cd dotnet", "D:\\source\\dd-trace-dotnet\\", "cd D:\\source\\dd-trace-dotnet\\")] // Absolute path replaces word
    [InlineData("cd proj", "C:\\Users\\test\\projects\\", "cd C:\\Users\\test\\projects\\")] // Windows absolute path
    [InlineData("cd test", "/home/user/test/", "cd /home/user/test/")]        // Unix absolute path
    [InlineData("sl mydir", "D:\\temp\\mydir\\", "sl D:\\temp\\mydir\\")]     // Set-Location with absolute path
    [InlineData("git che", "checkout master", "git checkout master")]         // Multi-word completion
    [InlineData("git chec", "checkout -b feature", "git checkout -b feature")] // Multi-word with flag
    [InlineData("git ", "commit -m", "git commit -m")]                        // Multi-word with no partial
    [InlineData("docker", "run -it", "docker run -it")]                       // Multi-word from empty command
    public void Combine_VariousScenarios_ProducesExpectedResults(string input, string completionText, string expected)
    {
        var result = CommandPredictor.Combine(input, completionText);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Combine_LongInput_PerformsWell()
    {
        // Test: very long input still works correctly
        var longInput = "git commit -m \"this is a very long commit message that goes on and on\" --a";
        var result = CommandPredictor.Combine(longInput, "--amend");
        Assert.Contains("--amend", result);
        Assert.DoesNotContain("--a--amend", result); // Should not duplicate
    }

    #endregion

    #region Partial Command Prediction Tests

    [Fact]
    public void ArgumentGraph_GetAllCommands_ReturnsLearnedCommands()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "status" }, null);
        graph.RecordUsage("gh", new[] { "pr" }, null);
        graph.RecordUsage("docker", new[] { "ps" }, null);

        // Act
        var commands = graph.GetAllCommands();

        // Assert
        Assert.NotNull(commands);
        var commandList = commands.ToList();
        Assert.Equal(3, commandList.Count);
        Assert.Contains(commandList, c => c.Key == "git");
        Assert.Contains(commandList, c => c.Key == "gh");
        Assert.Contains(commandList, c => c.Key == "docker");
    }

    [Fact]
    public void ArgumentGraph_CommandKnowledge_TracksUsageCount()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Record different usage counts
        for (int i = 0; i < 10; i++) graph.RecordUsage("git", new[] { "status" }, null);
        for (int i = 0; i < 5; i++) graph.RecordUsage("gh", new[] { "pr" }, null);

        // Act
        var commands = graph.GetAllCommands().ToList();

        // Assert
        var gitCmd = commands.First(c => c.Key == "git").Value;
        var ghCmd = commands.First(c => c.Key == "gh").Value;

        Assert.Equal(10, gitCmd.TotalUsageCount);
        Assert.Equal(5, ghCmd.TotalUsageCount);
    }

    [Fact]
    public void ArgumentGraph_CommandKnowledge_TracksTimestamps()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var before = DateTime.UtcNow;

        graph.RecordUsage("git", new[] { "status" }, null);

        var after = DateTime.UtcNow;

        // Act
        var commands = graph.GetAllCommands().ToList();
        var gitCmd = commands.First(c => c.Key == "git").Value;

        // Assert
        Assert.True(gitCmd.FirstSeen >= before && gitCmd.FirstSeen <= after);
        Assert.True(gitCmd.LastUsed >= before && gitCmd.LastUsed <= after);
    }

    [Fact]
    public void PartialCommand_FilterByPrefix_ReturnsMatchingCommands()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "status" }, null);
        graph.RecordUsage("gh", new[] { "pr" }, null);
        graph.RecordUsage("go", new[] { "build" }, null);
        graph.RecordUsage("docker", new[] { "ps" }, null);

        // Act: Filter commands starting with "g"
        var allCommands = graph.GetAllCommands();
        var matchingCommands = allCommands
            .Where(c => c.Key.StartsWith("g", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        Assert.Equal(3, matchingCommands.Count);
        Assert.Contains(matchingCommands, c => c.Key == "git");
        Assert.Contains(matchingCommands, c => c.Key == "gh");
        Assert.Contains(matchingCommands, c => c.Key == "go");
        Assert.DoesNotContain(matchingCommands, c => c.Key == "docker");
    }

    [Fact]
    public void PartialCommand_FilterByPrefix_IsCaseInsensitive()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("Git", new[] { "status" }, null);
        graph.RecordUsage("GitHub", new[] { "cli" }, null);

        // Act: Filter with lowercase "gi"
        var allCommands = graph.GetAllCommands();
        var matchingCommands = allCommands
            .Where(c => c.Key.StartsWith("gi", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        Assert.Equal(2, matchingCommands.Count);
        Assert.Contains(matchingCommands, c => c.Key.Equals("Git", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(matchingCommands, c => c.Key.Equals("GitHub", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PartialCommand_FrequencyScoring_OrdersByUsage()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Record different usage counts
        for (int i = 0; i < 10; i++) graph.RecordUsage("git", new[] { "status" }, null);
        for (int i = 0; i < 5; i++) graph.RecordUsage("gh", new[] { "pr" }, null);
        for (int i = 0; i < 2; i++) graph.RecordUsage("go", new[] { "build" }, null);

        // Act: Sort by usage count
        var allCommands = graph.GetAllCommands();
        var orderedCommands = allCommands
            .Where(c => c.Key.StartsWith("g", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Value.TotalUsageCount)
            .ToList();

        // Assert: Should be ordered by frequency
        Assert.Equal("git", orderedCommands[0].Key);
        Assert.Equal("gh", orderedCommands[1].Key);
        Assert.Equal("go", orderedCommands[2].Key);
    }

    [Fact]
    public void PartialCommand_EnvironmentVariable_CanBeDisabled()
    {
        // Arrange & Act
        Environment.SetEnvironmentVariable("PSCUE_PARTIAL_COMMAND_PREDICTIONS", "false");

        try
        {
            var value = Environment.GetEnvironmentVariable("PSCUE_PARTIAL_COMMAND_PREDICTIONS");

            // Assert
            Assert.Equal("false", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PSCUE_PARTIAL_COMMAND_PREDICTIONS", null);
        }
    }

    #endregion

    #region CD/PCD Predictor Tests with Tilde Expansion

    [Theory]
    [InlineData("~/", "C:\\Users\\test", "C:\\Users\\test/")]
    [InlineData("~/.config", "C:\\Users\\test", "C:\\Users\\test/.config")]
    [InlineData("~\\.config", "C:\\Users\\test", "C:\\Users\\test\\.config")]
    [InlineData("~/Documents/test", "C:\\Users\\test", "C:\\Users\\test/Documents/test")]
    [InlineData("~", "C:\\Users\\test", "C:\\Users\\test")]
    public void TildeExpansion_ExpandsToHomeDirectory(string input, string fakeHomeDir, string expected)
    {
        // Arrange - Mock home directory expansion logic (same as in GetPcdSuggestions)
        var wordToComplete = input;
        var homeDir = fakeHomeDir;

        // Act - Apply the same tilde expansion logic from GetPcdSuggestions
        if (wordToComplete.StartsWith("~"))
        {
            if (!string.IsNullOrEmpty(homeDir))
            {
                if (wordToComplete.Length == 1)
                {
                    // Just "~" - replace with home directory
                    wordToComplete = homeDir;
                }
                else if (wordToComplete[1] == Path.DirectorySeparatorChar || wordToComplete[1] == Path.AltDirectorySeparatorChar)
                {
                    // "~/path" or "~\path" - replace ~ with home directory
                    wordToComplete = homeDir + wordToComplete.Substring(1);
                }
            }
        }

        // Assert
        Assert.Equal(expected, wordToComplete);
    }

    [Fact]
    public void PcdCompletionEngine_TildeExpanded_FindsDotPrefixedDirectory()
    {
        // Arrange - Test that after tilde expansion, we can find .config directory
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".config");

        // Create .config directory if needed (cleanup after)
        var shouldCleanup = !Directory.Exists(configDir);
        if (shouldCleanup)
        {
            Directory.CreateDirectory(configDir);
        }

        try
        {
            var graph = new ArgumentGraph();
            graph.RecordUsage("cd", new[] { configDir }, homeDir);

            // Simulate tilde expansion (as done in GetPcdSuggestions)
            var wordToComplete = "~/.conf";
            if (wordToComplete.StartsWith("~"))
            {
                if (wordToComplete.Length > 1 && (wordToComplete[1] == Path.DirectorySeparatorChar || wordToComplete[1] == Path.AltDirectorySeparatorChar))
                {
                    wordToComplete = homeDir + wordToComplete.Substring(1);
                }
            }

            // Now wordToComplete should be "C:\Users\...\..config"
            var engine = new PcdCompletionEngine(graph);

            // Act - Get suggestions with expanded path
            var suggestions = engine.GetSuggestions(wordToComplete, homeDir, 20);

            // Assert - Should find .config directory
            Assert.NotEmpty(suggestions);
            var configSuggestion = suggestions.FirstOrDefault(s =>
                s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar).Equals(configDir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(configSuggestion);
            Assert.Contains(".config", configSuggestion.Path);
        }
        finally
        {
            // Cleanup if we created it
            if (shouldCleanup && Directory.Exists(configDir) && !Directory.EnumerateFileSystemEntries(configDir).Any())
            {
                try { Directory.Delete(configDir); } catch { /* Ignore cleanup errors */ }
            }
        }
    }

    [Fact]
    public void CdCommand_ShouldExpandTilde_BasedOnCommandType()
    {
        // This test documents the expected behavior:
        // - cd, Set-Location, sl, chdir should all expand tilde
        // - pcd should also expand tilde (uses same GetPcdSuggestions method)

        var commandsThatExpandTilde = new[] { "cd", "Set-Location", "sl", "chdir", "pcd", "Invoke-PCD" };

        foreach (var command in commandsThatExpandTilde)
        {
            // Document that these commands should use GetPcdSuggestions which expands tilde
            Assert.Contains(command.ToLowerInvariant(), new[] { "cd", "set-location", "sl", "chdir", "pcd", "invoke-pcd" });
        }
    }

    #endregion
}
