using System;
using System.IO;
using System.Threading;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Integration tests for persistence with Init.cs and auto-save functionality.
/// </summary>
public class PersistenceIntegrationTests : IDisposable
{
    private readonly string _testDbPath;

    public PersistenceIntegrationTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PSCue.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "integration-test.db");
    }

    public void Dispose()
    {
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
    public void FullCycle_SaveLoadSaveLoad_DataConsistent()
    {
        // Arrange - First session
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            var graph = new ArgumentGraph();
            graph.RecordUsage("git", new[] { "commit", "-m", "first" });
            graph.RecordUsage("git", new[] { "push" });

            var history = new CommandHistory();
            history.Add("git", "git commit", new[] { "commit" }, success: true);

            // Act - Save first session
            persistence.SaveArgumentGraph(graph);
            persistence.SaveCommandHistory(history);
        }

        // Session 2 - Simulate new learning (not loading old data)
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            // In real usage, Session 2 would start fresh with new learning
            var newGraph = new ArgumentGraph();
            newGraph.RecordUsage("git", new[] { "pull" });
            newGraph.RecordUsage("docker", new[] { "run" });

            var newHistory = new CommandHistory();
            newHistory.Add("docker", "docker run", new[] { "run" }, success: true);

            // Save - will merge additively with Session 1 data
            persistence.SaveArgumentGraph(newGraph);
            persistence.SaveCommandHistory(newHistory);
        }

        // Load final state - Third session
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            var finalGraph = persistence.LoadArgumentGraph();
            var finalHistory = persistence.LoadCommandHistory();

            // Assert - Should have both sessions' data (additively merged)
            Assert.Equal(2, finalGraph.GetTrackedCommands().Count); // git, docker

            var gitKnowledge = finalGraph.GetCommandKnowledge("git");
            Assert.NotNull(gitKnowledge);
            // Session 1: commit + push (2), Session 2: pull (1) = 3 total
            Assert.Equal(3, gitKnowledge.TotalUsageCount);

            var dockerKnowledge = finalGraph.GetCommandKnowledge("docker");
            Assert.NotNull(dockerKnowledge);
            Assert.Equal(1, dockerKnowledge.TotalUsageCount);

            // History uses replacement strategy (last save wins)
            Assert.Equal(1, finalHistory.Count); // Only Session 2's history
        }
    }

    [Fact]
    public void SessionSimulation_LoadModifySave_PersistsCorrectly()
    {
        // Simulate what Init.cs does: load on import, save on remove

        // Session 1
        {
            // OnImport - Load
            using var persistence = new PersistenceManager(_testDbPath);
            var graph = persistence.LoadArgumentGraph(); // Empty on first load
            var history = persistence.LoadCommandHistory();

            Assert.Empty(graph.GetTrackedCommands());
            Assert.Equal(0, history.Count);

            // User runs commands (FeedbackProvider records)
            graph.RecordUsage("git", new[] { "status" });
            graph.RecordUsage("git", new[] { "commit" });
            history.Add("git", "git status", new[] { "status" }, success: true);

            // OnRemove - Save
            persistence.SaveArgumentGraph(graph);
            persistence.SaveCommandHistory(history);
        }

        // Session 2
        {
            // OnImport - Load (should have Session 1 data)
            using var persistence = new PersistenceManager(_testDbPath);
            var loadedGraph = persistence.LoadArgumentGraph();
            var loadedHistory = persistence.LoadCommandHistory();

            Assert.Single(loadedGraph.GetTrackedCommands());
            Assert.Equal(1, loadedHistory.Count);

            var gitKnowledge = loadedGraph.GetCommandKnowledge("git");
            Assert.NotNull(gitKnowledge);
            Assert.Equal(2, gitKnowledge.TotalUsageCount);

            // User runs more commands - create NEW graph for this session's learning
            var newGraph = new ArgumentGraph();
            newGraph.RecordUsage("git", new[] { "push" });

            var newHistory = new CommandHistory();
            newHistory.Add("git", "git push", new[] { "push" }, success: true);

            // OnRemove - Save only the new learning (not the loaded+new)
            persistence.SaveArgumentGraph(newGraph);
            persistence.SaveCommandHistory(newHistory);
        }

        // Session 3 - Verify cumulative data
        {
            using var persistence = new PersistenceManager(_testDbPath);
            var graph = persistence.LoadArgumentGraph();
            var history = persistence.LoadCommandHistory();

            var gitKnowledge = graph.GetCommandKnowledge("git");
            Assert.NotNull(gitKnowledge);
            Assert.Equal(3, gitKnowledge.TotalUsageCount); // status, commit, push

            // History uses replacement strategy
            Assert.Equal(1, history.Count); // Only Session 2's history
        }
    }

    [Fact]
    public void MultipleLoadsSameSession_Idempotent()
    {
        // Arrange - Populate database
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            var graph = new ArgumentGraph();
            graph.RecordUsage("test", new[] { "arg" });
            persistence.SaveArgumentGraph(graph);
        }

        // Act - Load multiple times in same session
        using var sessionPersistence = new PersistenceManager(_testDbPath);

        var load1 = sessionPersistence.LoadArgumentGraph();
        var load2 = sessionPersistence.LoadArgumentGraph();
        var load3 = sessionPersistence.LoadArgumentGraph();

        // Assert - All loads should return equivalent data
        Assert.Single(load1.GetTrackedCommands());
        Assert.Single(load2.GetTrackedCommands());
        Assert.Single(load3.GetTrackedCommands());

        var knowledge1 = load1.GetCommandKnowledge("test");
        var knowledge2 = load2.GetCommandKnowledge("test");
        var knowledge3 = load3.GetCommandKnowledge("test");

        Assert.NotNull(knowledge1);
        Assert.NotNull(knowledge2);
        Assert.NotNull(knowledge3);

        Assert.Equal(knowledge1.TotalUsageCount, knowledge2.TotalUsageCount);
        Assert.Equal(knowledge2.TotalUsageCount, knowledge3.TotalUsageCount);
    }

    [Fact]
    public void SaveWithoutLoad_CreatesNewData()
    {
        // Act - Save without loading first (simulates first run)
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            var graph = new ArgumentGraph();
            graph.RecordUsage("initial", new[] { "data" });
            persistence.SaveArgumentGraph(graph);
        }

        // Assert - Data should be persisted
        using var verifyPersistence = new PersistenceManager(_testDbPath);
        var loaded = verifyPersistence.LoadArgumentGraph();

        Assert.Single(loaded.GetTrackedCommands());
        Assert.Contains("initial", loaded.GetTrackedCommands());
    }

    [Fact]
    public void CrashSimulation_DataNotLost()
    {
        // Arrange - Simulate Session 1 saves data
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            var graph = new ArgumentGraph();
            graph.RecordUsage("git", new[] { "commit" });
            persistence.SaveArgumentGraph(graph);
        }

        // Simulate crash - no explicit cleanup, just new session

        // Act - Session 2 loads (should recover saved data)
        using var newPersistence = new PersistenceManager(_testDbPath);
        var recovered = newPersistence.LoadArgumentGraph();

        // Assert - Data should be intact
        Assert.Single(recovered.GetTrackedCommands());
        var gitKnowledge = recovered.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        Assert.Equal(1, gitKnowledge.TotalUsageCount);
    }

    [Fact]
    public void LongRunningSessions_PeriodicSave_DataNotLost()
    {
        // This test simulates what auto-save timer does
        // In real scenario, Init.cs saves the CURRENT in-memory ArgumentGraph state
        // which includes both old loaded data + new learning

        using var persistence = new PersistenceManager(_testDbPath);

        // Simulate 5 save cycles
        for (int i = 0; i < 5; i++)
        {
            // Each cycle: user runs a command, then periodic save happens
            var deltaGraph = new ArgumentGraph();
            deltaGraph.RecordUsage("test", new[] { $"arg{i}" });

            // Periodic auto-save (save only the delta)
            persistence.SaveArgumentGraph(deltaGraph);

            // Simulate time passing
            Thread.Sleep(10);
        }

        // Verify all data was saved
        var loaded = persistence.LoadArgumentGraph();
        var testKnowledge = loaded.GetCommandKnowledge("test");
        Assert.NotNull(testKnowledge);
        Assert.Equal(5, testKnowledge.TotalUsageCount); // 1+1+1+1+1 = 5
    }

    [Fact]
    public void DifferentConfigSettings_LoadedCorrectly()
    {
        // Arrange - Save with one set of limits
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);

            for (int i = 0; i < 10; i++)
            {
                graph.RecordUsage($"cmd{i}", new[] { "arg1", "arg2", "arg3" });
            }

            persistence.SaveArgumentGraph(graph);
        }

        // Act - Load with different limits
        using var newPersistence = new PersistenceManager(_testDbPath);
        var loaded = newPersistence.LoadArgumentGraph(maxCommands: 5, maxArgumentsPerCommand: 20);

        // Assert - Should load all saved data (limits apply to new learning, not loading)
        Assert.Equal(10, loaded.GetTrackedCommands().Count);
    }

    [Fact]
    public void EmptyDatabaseOnFirstLoad_ReturnsEmptyStructures()
    {
        // Act - First load ever (database doesn't exist yet)
        using var persistence = new PersistenceManager(_testDbPath);
        var graph = persistence.LoadArgumentGraph();
        var history = persistence.LoadCommandHistory();

        // Assert
        Assert.NotNull(graph);
        Assert.Empty(graph.GetTrackedCommands());

        Assert.NotNull(history);
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void SaveEmptyGraph_ClearsExistingData()
    {
        // Arrange - Populate database
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            var graph = new ArgumentGraph();
            graph.RecordUsage("test", new[] { "arg" });
            persistence.SaveArgumentGraph(graph);
        }

        // Act - Save empty graph (simulates Clear + Save)
        using (var persistence = new PersistenceManager(_testDbPath))
        {
            persistence.Clear(); // Explicit clear

            var emptyGraph = new ArgumentGraph();
            persistence.SaveArgumentGraph(emptyGraph);
        }

        // Assert - Should be empty
        using var verifyPersistence = new PersistenceManager(_testDbPath);
        var loaded = verifyPersistence.LoadArgumentGraph();
        Assert.Empty(loaded.GetTrackedCommands());
    }

    [Fact]
    public void HistoryRingBuffer_ExceedsLimit_OnlyRecentPreserved()
    {
        // Arrange - Create history with limit
        using var persistence = new PersistenceManager(_testDbPath);
        var history = new CommandHistory(maxSize: 10);

        // Add 20 entries (exceeds ring buffer)
        for (int i = 0; i < 20; i++)
        {
            history.Add("test", $"command {i}", new[] { $"arg{i}" }, success: true);
        }

        // Act - Save with limit
        persistence.SaveCommandHistory(history, maxEntries: 5);

        // Load
        var loaded = persistence.LoadCommandHistory(maxSize: 10);

        // Assert - Should only have 5 most recent
        Assert.Equal(5, loaded.Count);

        // Most recent should be command 19
        var mostRecent = loaded.GetMostRecent();
        Assert.NotNull(mostRecent);
        Assert.Contains("19", mostRecent.CommandLine);
    }
}
