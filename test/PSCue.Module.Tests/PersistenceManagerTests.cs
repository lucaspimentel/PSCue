using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;

namespace PSCue.Module.Tests;

public class PersistenceManagerTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PersistenceManager _persistence;
    private readonly SqliteConnection _connection;

    public PersistenceManagerTests()
    {
        // Create unique temp database for each test
        var tempDir = Path.Combine(Path.GetTempPath(), "PSCue.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "test.db");
        _persistence = new PersistenceManager(_testDbPath);
        _connection = _persistence.CreateSharedConnection();
    }

    public void Dispose()
    {
        _connection?.Dispose();
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
        var loaded = _persistence.LoadArgumentGraph(_connection, maxCommands: 10, maxArgumentsPerCommand: 5, scoreDecayDays: 30);

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
        var loaded = _persistence.LoadArgumentGraph(_connection);

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
        var loaded = _persistence.LoadCommandHistory(_connection, maxSize: 10);

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
        var loaded = _persistence.LoadCommandHistory(_connection, maxSize: 100);

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
        var loadedGraph = _persistence.LoadArgumentGraph(_connection);
        var loadedHistory = _persistence.LoadCommandHistory(_connection);

        Assert.Empty(loadedGraph.GetTrackedCommands());
        Assert.Equal(0, loadedHistory.Count);
    }

    [Fact]
    public void LoadFromEmptyDatabase_ReturnsEmptyStructures()
    {
        // Act
        var graph = _persistence.LoadArgumentGraph(_connection);
        var history = _persistence.LoadCommandHistory(_connection);

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
        var loaded = _persistence.LoadArgumentGraph(_connection);

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
        var loaded = _persistence.LoadArgumentGraph(_connection);

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
        var loaded = _persistence.LoadArgumentGraph(_connection);

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
        var loaded = _persistence.LoadArgumentGraph(_connection);

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

    [Fact]
    public void PruneStaleDirectoryEntries_RemovesNonExistentPaths()
    {
        // Arrange - save a graph with a real and a fake directory path
        var graph = new ArgumentGraph();
        var realPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fakePath = @"C:\this\path\does\not\exist\";

        graph.RecordUsage("cd", new[] { realPath });
        graph.RecordUsage("cd", new[] { fakePath });

        _persistence.SaveArgumentGraph(graph);

        // Act
        int pruned = _persistence.PruneStaleDirectoryEntries(graph);

        // Assert - fake path should be pruned
        Assert.Equal(1, pruned);

        // Verify via reload
        var loaded = _persistence.LoadArgumentGraph(_connection);
        var cdKnowledge = loaded.GetCommandKnowledge("cd");
        Assert.NotNull(cdKnowledge);
        Assert.Contains(cdKnowledge.Arguments.Keys, k => k.Equals(realPath, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(cdKnowledge.Arguments.Keys, k => k.Equals(fakePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PruneStaleDirectoryEntries_RemovesFromInMemoryGraph()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var fakePath = @"C:\nonexistent\directory\for\test\";

        graph.RecordUsage("cd", new[] { fakePath });
        _persistence.SaveArgumentGraph(graph);

        // Verify it's in memory before pruning
        var cdKnowledge = graph.GetCommandKnowledge("cd");
        Assert.NotNull(cdKnowledge);
        Assert.Contains(cdKnowledge.Arguments.Keys, k => k.Equals(fakePath, StringComparison.OrdinalIgnoreCase));

        // Act
        _persistence.PruneStaleDirectoryEntries(graph);

        // Assert - should be removed from in-memory graph too
        cdKnowledge = graph.GetCommandKnowledge("cd");
        Assert.NotNull(cdKnowledge);
        Assert.DoesNotContain(cdKnowledge.Arguments.Keys, k => k.Equals(fakePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PruneStaleDirectoryEntries_OnlyAffectsNavigationCommands()
    {
        // Arrange - save both a navigation command and a regular command with non-existent args
        var graph = new ArgumentGraph();
        graph.RecordUsage("cd", new[] { @"C:\nonexistent\path\" });
        graph.RecordUsage("git", new[] { "nonexistent-branch" });

        _persistence.SaveArgumentGraph(graph);

        // Act
        _persistence.PruneStaleDirectoryEntries(graph);

        // Assert - git argument should not be affected
        var loaded = _persistence.LoadArgumentGraph(_connection);
        var gitKnowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        Assert.Contains(gitKnowledge.Arguments.Keys, k => k == "nonexistent-branch");
    }

    [Fact]
    public void SaveArgumentGraph_UpdatesArgumentCasing_OnConflict()
    {
        // Arrange - save with original casing
        var graph1 = new ArgumentGraph();
        graph1.RecordUsage("git", new[] { "MyBranch" });
        _persistence.SaveArgumentGraph(graph1);

        // Act - save with different casing
        var graph2 = new ArgumentGraph();
        graph2.RecordUsage("git", new[] { "mybranch" });
        _persistence.SaveArgumentGraph(graph2);

        // Assert - loaded argument text should have updated casing
        var loaded = _persistence.LoadArgumentGraph(_connection);
        var knowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(knowledge);
        var arg = knowledge.Arguments["mybranch"];
        Assert.Equal("mybranch", arg.Argument);
        Assert.Equal(2, arg.UsageCount);
    }

    [Fact]
    public void CreateSharedConnection_MultipleLoads_ConnectionStaysOpen()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "status" });
        var history = new CommandHistory();
        history.Add("git", "git status", new[] { "status" }, success: true);

        _persistence.SaveArgumentGraph(graph);
        _persistence.SaveCommandHistory(history);

        // Act: open one connection, run multiple Loads through it
        using var shared = _persistence.CreateSharedConnection();

        var loadedGraph = _persistence.LoadArgumentGraph(shared);
        Assert.Equal(System.Data.ConnectionState.Open, shared.State);

        var loadedHistory = _persistence.LoadCommandHistory(shared);
        Assert.Equal(System.Data.ConnectionState.Open, shared.State);

        var loadedBookmarks = _persistence.LoadBookmarks(shared);
        Assert.Equal(System.Data.ConnectionState.Open, shared.State);

        // Assert: Load methods must not dispose the shared connection, and they return correct data
        Assert.Contains("git", loadedGraph.GetTrackedCommands());
        Assert.Equal(1, loadedHistory.Count);
        Assert.NotNull(loadedBookmarks);
    }

    [Fact]
    public void PruneStaleDirectoryEntries_ReturnsZeroWhenNothingToPrune()
    {
        // Arrange - save with only existing paths
        var graph = new ArgumentGraph();
        var realPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        graph.RecordUsage("cd", new[] { realPath });

        _persistence.SaveArgumentGraph(graph);

        // Act
        int pruned = _persistence.PruneStaleDirectoryEntries(graph);

        // Assert
        Assert.Equal(0, pruned);
    }

    [Fact]
    public void InitializeDatabase_FreshDb_SetsSchemaVersion()
    {
        // Fixture already created a PersistenceManager for a fresh path.
        var version = GetUserVersion(_connection);
        Assert.Equal(1, version);
    }

    [Fact]
    public void InitializeDatabase_ExistingDb_PreservesDataAndVersion()
    {
        // Arrange - save data through the fixture instance
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "status" });
        _persistence.SaveArgumentGraph(graph);

        // Tear down first instance (closes its connections)
        _connection.Dispose();
        _persistence.Dispose();

        // Act - open a second PersistenceManager against the same DB file
        using var second = new PersistenceManager(_testDbPath);
        using var secondConn = second.CreateSharedConnection();

        // Assert - version is still current
        Assert.Equal(1, GetUserVersion(secondConn));

        // Assert - previously-saved data is intact (DDL skip did not wipe anything)
        var loaded = second.LoadArgumentGraph(secondConn);
        var gitKnowledge = loaded.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        Assert.Equal(1, gitKnowledge.TotalUsageCount);
        Assert.Contains(gitKnowledge.Arguments.Keys, k => k == "status");
    }

    [Fact]
    public void InitializeDatabase_PreVersioningDb_UpgradesToCurrent()
    {
        // Arrange - simulate a database from before the user_version gate was introduced
        // by forcing the version back to 0 on the fixture DB.
        using (var resetCmd = _connection.CreateCommand())
        {
            resetCmd.CommandText = "PRAGMA user_version = 0;";
            resetCmd.ExecuteNonQuery();
        }
        Assert.Equal(0, GetUserVersion(_connection));

        // Close the fixture's handles so the new instance's DDL path runs cleanly.
        _connection.Dispose();
        _persistence.Dispose();

        // Act - new PersistenceManager should see version 0 and run the DDL path,
        // then bump the version to 1.
        using var upgraded = new PersistenceManager(_testDbPath);
        using var upgradedConn = upgraded.CreateSharedConnection();

        // Assert
        Assert.Equal(1, GetUserVersion(upgradedConn));

        var expectedTables = new[]
        {
            "commands", "arguments", "co_occurrences", "flag_combinations",
            "argument_sequences", "command_history", "command_sequences",
            "workflow_transitions", "parameters", "parameter_values", "bookmarks",
        };
        foreach (var table in expectedTables)
        {
            Assert.True(TableExists(upgradedConn, table), $"Expected table '{table}' after upgrade");
        }
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        return cmd.ExecuteScalar() != null;
    }
}
