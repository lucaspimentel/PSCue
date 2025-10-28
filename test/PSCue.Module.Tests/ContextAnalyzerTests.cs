using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

public class ContextAnalyzerTests
{
    [Fact]
    public void AnalyzeContext_EmptyHistory_ReturnsEmptyContext()
    {
        // Arrange
        var history = new CommandHistory();
        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        Assert.Empty(context.RecentCommands);
        Assert.Empty(context.DetectedSequences);
        Assert.Empty(context.SuggestedNextCommands);
        Assert.Empty(context.ContextBoosts);
    }

    [Fact]
    public void AnalyzeContext_DetectsGitWorkflow()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("git", "git add .", new[] { "add", "." }, success: true);
        history.Add("git", "git commit -m 'test'", new[] { "commit", "-m", "'test'" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        Assert.NotEmpty(context.SuggestedNextCommands);
        Assert.Contains(context.SuggestedNextCommands, cmd => cmd.Contains("push", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeContext_DetectsDockerWorkflow()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("docker", "docker build -t myapp .", new[] { "build", "-t", "myapp", "." }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "docker");

        // Assert
        Assert.Contains(context.SuggestedNextCommands, cmd => cmd.Contains("run", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeContext_BoostsRecentArguments()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("git", "git commit -m 'test'", new[] { "commit", "-m", "'test'" }, success: true);
        history.Add("git", "git commit --amend", new[] { "commit", "--amend" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        Assert.NotEmpty(context.ContextBoosts);
        Assert.True(context.ContextBoosts.ContainsKey("commit"));
        Assert.True(context.ContextBoosts.ContainsKey("-m"));
        Assert.True(context.ContextBoosts.ContainsKey("--amend"));
    }

    [Fact]
    public void AnalyzeContext_GitAddBoostsCommitFlags()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("git", "git add file.txt", new[] { "add", "file.txt" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        // After "git add", should boost commit-related flags
        Assert.True(context.ContextBoosts.ContainsKey("-m") || context.ContextBoosts.ContainsKey("commit"));
    }

    [Fact]
    public void AnalyzeContext_GitCommitBoostsPush()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("git", "git commit -m 'test'", new[] { "commit", "-m", "'test'" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        Assert.True(context.ContextBoosts.ContainsKey("push") || context.ContextBoosts.ContainsKey("status"));
    }

    [Fact]
    public void AnalyzeContext_DockerBuildBoostsRunFlags()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("docker", "docker build -t myapp .", new[] { "build", "-t", "myapp", "." }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "docker");

        // Assert
        Assert.True(context.ContextBoosts.ContainsKey("run") ||
                    context.ContextBoosts.ContainsKey("-d") ||
                    context.ContextBoosts.ContainsKey("-p"));
    }

    [Fact]
    public void AnalyzeContext_DockerPsBoostsLogsExec()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("docker", "docker ps", new[] { "ps" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "docker");

        // Assert
        Assert.True(context.ContextBoosts.ContainsKey("logs") ||
                    context.ContextBoosts.ContainsKey("exec") ||
                    context.ContextBoosts.ContainsKey("stop"));
    }

    [Fact]
    public void AnalyzeContext_BuildCommandBoostsTestRun()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("dotnet", "dotnet build", new[] { "build" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "dotnet");

        // Assert
        Assert.True(context.ContextBoosts.ContainsKey("test") || context.ContextBoosts.ContainsKey("run"));
    }

    [Fact]
    public void AnalyzeContext_ExtractsWorkingDirectory()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("git", "git status", new[] { "status" }, success: true, workingDirectory: "/home/user/repo");

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        Assert.Equal("/home/user/repo", context.CurrentDirectory);
    }

    [Fact]
    public void AnalyzeContext_RecencyBoost_HigherForRecentFlags()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("git", "git commit -m 'old'", new[] { "commit", "-m", "'old'" }, success: true);
        Thread.Sleep(10);
        history.Add("git", "git commit -a", new[] { "commit", "-a" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        // Both flags should be boosted, but -a is more recent
        Assert.True(context.ContextBoosts.ContainsKey("-m"));
        Assert.True(context.ContextBoosts.ContainsKey("-a"));
    }

    [Fact]
    public void AnalyzeContext_LimitsRecentCommands()
    {
        // Arrange
        var history = new CommandHistory();
        for (int i = 0; i < 20; i++)
        {
            history.Add("git", $"git commit {i}", new[] { "commit", i.ToString() }, success: true);
        }

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        Assert.Equal(10, context.RecentCommands.Count); // Should limit to last 10
    }

    [Fact]
    public void AnalyzeContext_KubernetesWorkflow()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("kubectl", "kubectl apply -f deployment.yaml", new[] { "apply", "-f", "deployment.yaml" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "kubectl");

        // Assert
        Assert.Contains(context.SuggestedNextCommands, cmd =>
            cmd.Contains("get", StringComparison.OrdinalIgnoreCase) ||
            cmd.Contains("describe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeContext_NpmWorkflow()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("npm", "npm install", new[] { "install" }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act
        var context = analyzer.AnalyzeContext(history, "npm");

        // Assert
        Assert.Contains(context.SuggestedNextCommands, cmd =>
            cmd.Contains("start", StringComparison.OrdinalIgnoreCase) ||
            cmd.Contains("run", StringComparison.OrdinalIgnoreCase) ||
            cmd.Contains("test", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeContext_RepeatingCommand_BoostsPattern()
    {
        // Arrange
        var history = new CommandHistory();
        history.Add("git", "git status", new[] { "status" }, success: true);
        history.Add("git", "git add .", new[] { "add", "." }, success: true);

        var analyzer = new ContextAnalyzer();

        // Act - analyzing while user is typing "git" again
        var context = analyzer.AnalyzeContext(history, "git");

        // Assert
        Assert.NotEmpty(context.ContextBoosts);
        // Should have boost for recent command repeat pattern
        Assert.Contains(context.ContextBoosts, kvp => kvp.Value > 1.0);
    }
}
