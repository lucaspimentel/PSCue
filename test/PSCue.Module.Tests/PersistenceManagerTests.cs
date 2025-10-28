using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PSCue.Module.Tests;

public class PersistenceManagerTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PersistenceManager _persistence;

    public PersistenceManagerTests()
    {
        // Create unique temp database for each test
        var tempDir = Path.Combine(Path.GetTempPath(), "PSCue.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "test.db");
        _persistence = new PersistenceManager(_testDbPath);
    }

    public void Dispose()
    {
        _persistence?.Dispose();

        // Clean up test database
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }

            var dir = Path.GetDirectoryName(_testDbPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void SaveAndLoad_ArgumentGraph_RoundTrip()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 10, maxArgumentsPerCommand: 5, scoreDecayDays: 30);
        graph.RecordUsage("git", new[] { "commit", "-m", "test message" });
        graph.RecordUsage("git", new[] { "push", "origin", "main" });
        graph.RecordUsage("docker", new[] { "run", "-it", "ubuntu" });

        // Act - Save
        _persistence.SaveArgumentGraph(graph);

        // Act - Load
        var loaded = _persistence.LoadArgumentGraph(maxCommands: 10, maxArgumentsPerCommand: 5, scoreDecayDays: 30);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.GetTrackedCommands().Count); // git, docker

        var gitKnowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        Assert.Equal(2, gitKnowledge.TotalUsageCount); // 2 git commands
        Assert.Contains(gitKnowledge.Arguments.Keys, k => k == "commit");
        Assert.Contains(gitKnowledge.Arguments.Keys, k => k == "push");
        Assert.Contains(gitKnowledge.Arguments.Keys, k => k == "-m");

        var dockerKnowledge = loaded.GetCommandKnowledge("docker");
        Assert.NotNull(dockerKnowledge);
        Assert.Equal(1, dockerKnowledge.TotalUsageCount);
        Assert.Contains(dockerKnowledge.Arguments.Keys, k => k == "run");
    }

    [Fact]
    public void SaveArgumentGraph_Twice_AdditiveMerge()
    {
        // Arrange
        var graph1 = new ArgumentGraph();
        graph1.RecordUsage("git", new[] { "commit", "-m", "first" });
        graph1.RecordUsage("git", new[] { "commit", "-m", "second" }); // Used twice

        var graph2 = new ArgumentGraph();
        graph2.RecordUsage("git", new[] { "commit", "-m", "third" }); // Used once

        // Act - Save first graph
        _persistence.SaveArgumentGraph(graph1);

        // Act - Save second graph (should merge additively)
        _persistence.SaveArgumentGraph(graph2);

        // Act - Load
        var loaded = _persistence.LoadArgumentGraph();

        // Assert - Frequencies should be summed
        var gitKnowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        Assert.Equal(3, gitKnowledge.TotalUsageCount); // 2 + 1 = 3

        var commitArg = gitKnowledge.Arguments["commit"];
        Assert.Equal(3, commitArg.UsageCount); // 2 + 1 = 3
    }

    [Fact]
    public void SaveAndLoad_CommandHistory_RoundTrip()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 10);
        history.Add("git", "git commit -m \"test\"", new[] { "commit", "-m", "test" }, success: true);
        history.Add("git", "git push", new[] { "push" }, success: true);
        history.Add("docker", "docker ps", new[] { "ps" }, success: false);

        // Act - Save
        _persistence.SaveCommandHistory(history, maxEntries: 10);

        // Act - Load
        var loaded = _persistence.LoadCommandHistory(maxSize: 10);

        // Assert
        Assert.Equal(3, loaded.Count);

        var recent = loaded.GetRecent();
        Assert.Equal(3, recent.Count);

        // Most recent should be docker
        var mostRecent = loaded.GetMostRecent();
        Assert.NotNull(mostRecent);
        Assert.Equal("docker", mostRecent.Command);
        Assert.False(mostRecent.Success);
    }

    [Fact]
    public void SaveCommandHistory_ExceedsMaxEntries_KeepsOnlyRecent()
    {
        // Arrange
        var history = new CommandHistory(maxSize: 100);

        // Add 20 entries
        for (int i = 0; i < 20; i++)
        {
            history.Add("test", $"test command {i}", new[] { "arg" }, success: true);
        }

        // Act - Save with max 10 entries
        _persistence.SaveCommandHistory(history, maxEntries: 10);

        // Act - Load
        var loaded = _persistence.LoadCommandHistory(maxSize: 100);

        // Assert - Should only have 10 most recent
        Assert.Equal(10, loaded.Count);
    }

    [Fact]
    public void Clear_RemovesAllData()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit" });

        var history = new CommandHistory();
        history.Add("git", "git commit", new[] { "commit" }, success: true);

        _persistence.SaveArgumentGraph(graph);
        _persistence.SaveCommandHistory(history);

        // Act
        _persistence.Clear();

        // Assert
        var loadedGraph = _persistence.LoadArgumentGraph();
        var loadedHistory = _persistence.LoadCommandHistory();

        Assert.Empty(loadedGraph.GetTrackedCommands());
        Assert.Equal(0, loadedHistory.Count);
    }

    [Fact]
    public void LoadFromEmptyDatabase_ReturnsEmptyStructures()
    {
        // Act
        var graph = _persistence.LoadArgumentGraph();
        var history = _persistence.LoadCommandHistory();

        // Assert
        Assert.NotNull(graph);
        Assert.Empty(graph.GetTrackedCommands());

        Assert.NotNull(history);
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void SaveArgumentGraph_PreservesCoOccurrences()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit", "-a", "-m", "message" });

        // Act
        _persistence.SaveArgumentGraph(graph);
        var loaded = _persistence.LoadArgumentGraph();

        // Assert
        var gitKnowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);

        var commitStats = gitKnowledge.Arguments["commit"];
        Assert.Contains("-a", commitStats.CoOccurrences.Keys);
        Assert.Contains("-m", commitStats.CoOccurrences.Keys);
    }

    [Fact]
    public void SaveArgumentGraph_PreservesFlagCombinations()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit", "-a", "-m", "message" });

        // Act
        _persistence.SaveArgumentGraph(graph);
        var loaded = _persistence.LoadArgumentGraph();

        // Assert
        var gitKnowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        Assert.NotEmpty(gitKnowledge.FlagCombinations);
    }

    [Fact]
    public void SaveArgumentGraph_PreservesTimestamps()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit" });

        var gitKnowledge = graph.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        var originalFirstSeen = gitKnowledge.FirstSeen;
        var originalLastUsed = gitKnowledge.LastUsed;

        // Act
        _persistence.SaveArgumentGraph(graph);
        var loaded = _persistence.LoadArgumentGraph();

        // Assert
        var loadedKnowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(loadedKnowledge);

        // Timestamps should be preserved within 1 second (account for serialization)
        var timeDiffFirstSeen = Math.Abs((originalFirstSeen - loadedKnowledge.FirstSeen).TotalSeconds);
        var timeDiffLastUsed = Math.Abs((originalLastUsed - loadedKnowledge.LastUsed).TotalSeconds);

        Assert.True(timeDiffFirstSeen < 1, $"FirstSeen timestamp differs by {timeDiffFirstSeen} seconds");
        Assert.True(timeDiffLastUsed < 1, $"LastUsed timestamp differs by {timeDiffLastUsed} seconds");
    }

    [Fact]
    public void MultipleOperations_DatabaseRemainsConsistent()
    {
        // Arrange
        var graph1 = new ArgumentGraph();
        graph1.RecordUsage("git", new[] { "commit" });

        var graph2 = new ArgumentGraph();
        graph2.RecordUsage("git", new[] { "push" });

        var graph3 = new ArgumentGraph();
        graph3.RecordUsage("docker", new[] { "run" });

        // Act - Multiple saves
        _persistence.SaveArgumentGraph(graph1);
        _persistence.SaveArgumentGraph(graph2);
        _persistence.SaveArgumentGraph(graph3);

        // Load and verify
        var loaded = _persistence.LoadArgumentGraph();

        // Assert
        Assert.Equal(2, loaded.GetTrackedCommands().Count); // git, docker

        var gitKnowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        Assert.Equal(2, gitKnowledge.TotalUsageCount); // commit + push
        Assert.Contains(gitKnowledge.Arguments.Keys, k => k == "commit");
        Assert.Contains(gitKnowledge.Arguments.Keys, k => k == "push");

        var dockerKnowledge = loaded.GetCommandKnowledge("docker");
        Assert.NotNull(dockerKnowledge);
        Assert.Equal(1, dockerKnowledge.TotalUsageCount);
    }
}
