using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

public class CommandHistoryTests
{
    [Fact]
    public void Add_StoresCommandEntry()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);

        // Act
        history.Add("git", "git commit -m 'test'", new[] { "commit", "-m", "'test'" }, success: true);

        // Assert
        Assert.Equal(1, history.Count);
        var recent = history.GetMostRecent();
        Assert.NotNull(recent);
        Assert.Equal("git", recent.Command);
        Assert.Equal("git commit -m 'test'", recent.CommandLine);
        Assert.True(recent.Success);
    }

    [Fact]
    public void Add_EnforcesMaxSize()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 3);

        // Act - add 5 entries
        for (int i = 0; i < 5; i++)
        {
            history.Add($"cmd{i}", $"cmd{i} arg", new[] { "arg" }, success: true);
        }

        // Assert - only last 3 should be kept
        Assert.Equal(3, history.Count);
        var recent = history.GetRecent();
        Assert.Equal("cmd4", recent[0].Command); // Most recent
        Assert.Equal("cmd3", recent[1].Command);
        Assert.Equal("cmd2", recent[2].Command);
    }

    [Fact]
    public void GetRecent_ReturnsInReverseOrder()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);
        history.Add("git", "git status", new[] { "status" }, success: true);
        history.Add("npm", "npm install", new[] { "install" }, success: true);
        history.Add("docker", "docker ps", new[] { "ps" }, success: true);

        // Act
        var recent = history.GetRecent();

        // Assert - most recent first
        Assert.Equal(3, recent.Count);
        Assert.Equal("docker", recent[0].Command);
        Assert.Equal("npm", recent[1].Command);
        Assert.Equal("git", recent[2].Command);
    }

    [Fact]
    public void GetRecent_WithCount_LimitsResults()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);
        for (int i = 0; i < 5; i++)
        {
            history.Add($"cmd{i}", $"cmd{i}", Array.Empty<string>(), success: true);
        }

        // Act
        var recent = history.GetRecent(count: 2);

        // Assert
        Assert.Equal(2, recent.Count);
        Assert.Equal("cmd4", recent[0].Command);
        Assert.Equal("cmd3", recent[1].Command);
    }

    [Fact]
    public void GetForCommand_FiltersCorrectly()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);
        history.Add("git", "git status", new[] { "status" }, success: true);
        history.Add("npm", "npm install", new[] { "install" }, success: true);
        history.Add("git", "git commit", new[] { "commit" }, success: true);
        history.Add("docker", "docker ps", new[] { "ps" }, success: true);
        history.Add("git", "git push", new[] { "push" }, success: true);

        // Act
        var gitCommands = history.GetForCommand("git");

        // Assert
        Assert.Equal(3, gitCommands.Count);
        Assert.All(gitCommands, entry => Assert.Equal("git", entry.Command));
        // Should be in reverse order (most recent first)
        Assert.Equal("push", gitCommands[0].Arguments[0]);
        Assert.Equal("commit", gitCommands[1].Arguments[0]);
        Assert.Equal("status", gitCommands[2].Arguments[0]);
    }

    [Fact]
    public void GetForCommand_CaseInsensitive()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);
        history.Add("Git", "Git status", new[] { "status" }, success: true);
        history.Add("GIT", "GIT commit", new[] { "commit" }, success: true);

        // Act
        var gitCommands = history.GetForCommand("git");

        // Assert
        Assert.Equal(2, gitCommands.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);
        history.Add("git", "git status", new[] { "status" }, success: true);
        history.Add("npm", "npm install", new[] { "install" }, success: true);

        // Act
        history.Clear();

        // Assert
        Assert.Equal(0, history.Count);
        Assert.Null(history.GetMostRecent());
    }

    [Fact]
    public void GetStatistics_CalculatesCorrectly()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);
        history.Add("git", "git status", new[] { "status" }, success: true);
        history.Add("git", "git commit", new[] { "commit" }, success: true);
        history.Add("npm", "npm install", new[] { "install" }, success: false); // Failed
        history.Add("git", "git push", new[] { "push" }, success: true);
        history.Add("docker", "docker ps", new[] { "ps" }, success: true);

        // Act
        var stats = history.GetStatistics();

        // Assert
        Assert.Equal(5, stats.TotalCommands);
        Assert.Equal(10, stats.MaxSize);
        Assert.Equal(3, stats.UniqueCommands); // git, npm, docker
        Assert.Equal(4, stats.SuccessCount);
        Assert.Equal(1, stats.FailureCount);
        Assert.Equal(0.8, stats.SuccessRate); // 4/5
        Assert.Equal("git", stats.MostCommonCommand);
        Assert.Equal(3, stats.MostCommonCommandCount);
        Assert.NotNull(stats.OldestEntry);
        Assert.NotNull(stats.NewestEntry);
    }

    [Fact]
    public void GetStatistics_EmptyHistory()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);

        // Act
        var stats = history.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalCommands);
        Assert.Equal(0, stats.UniqueCommands);
        Assert.Equal(0.0, stats.SuccessRate);
        Assert.Null(stats.OldestEntry);
        Assert.Null(stats.NewestEntry);
    }

    [Fact]
    public void Add_WithWorkingDirectory_Stores()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);

        // Act
        history.Add("git", "git status", new[] { "status" }, success: true, workingDirectory: "/home/user/repo");

        // Assert
        var recent = history.GetMostRecent();
        Assert.NotNull(recent);
        Assert.Equal("/home/user/repo", recent.WorkingDirectory);
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidSize()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandHistory(maxSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandHistory(maxSize: -1));
    }

    [Fact]
    public void Add_NullEntry_ThrowsArgumentNullException()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => history.Add(null!));
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentAdds()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 1000);
        var tasks = new List<Task>();

        // Act - concurrent adds from multiple threads
        for (int i = 0; i < 10; i++)
        {
            int threadNum = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    history.Add($"cmd{threadNum}", $"cmd{threadNum} arg{j}", new[] { $"arg{j}" }, success: true);
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert - should have 1000 entries (or close to it due to max size)
        Assert.Equal(1000, history.Count);
        var stats = history.GetStatistics();
        Assert.Equal(10, stats.UniqueCommands);
    }
}
