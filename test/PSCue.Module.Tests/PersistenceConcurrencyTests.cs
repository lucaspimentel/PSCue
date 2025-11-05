using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Integration tests for cross-session persistence concurrency.
/// Verifies that multiple PowerShell sessions can safely read/write simultaneously.
/// </summary>
public class PersistenceConcurrencyTests : IDisposable
{
    private readonly string _testDbPath;

    public PersistenceConcurrencyTests()
    {
        // Create unique temp database for each test
        var tempDir = Path.Combine(Path.GetTempPath(), "PSCue.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _testDbPath = Path.Combine(tempDir, "concurrent-test.db");
    }

    public void Dispose()
    {
        // Clean up test database and WAL files
        try
        {
            // Delete main database file
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }

            // Delete SQLite WAL files
            var walPath = _testDbPath + "-wal";
            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }

            var shmPath = _testDbPath + "-shm";
            if (File.Exists(shmPath))
            {
                File.Delete(shmPath);
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
    public async Task ConcurrentSessions_SameCommand_FrequenciesSummed()
    {
        // Arrange - 3 sessions all learning "git push"
        const int sessionCount = 3;

        var tasks = new List<Task>();

        // Act - Each session records "git push" 5 times
        for (int i = 0; i < sessionCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                using var persistence = new PersistenceManager(_testDbPath);
                var graph = new ArgumentGraph();

                for (int j = 0; j < 5; j++)
                {
                    graph.RecordUsage("git", new[] { "push", "origin", "main" });
                }

                persistence.SaveArgumentGraph(graph);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Frequencies should be summed (3 sessions * 5 times = 15)
        using var finalPersistence = new PersistenceManager(_testDbPath);
        var finalGraph = finalPersistence.LoadArgumentGraph();

        var gitKnowledge = finalGraph.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);
        Assert.Equal(15, gitKnowledge.TotalUsageCount);

        Assert.Equal(15, gitKnowledge.Arguments["push"].UsageCount);
        Assert.Equal(15, gitKnowledge.Arguments["origin"].UsageCount);
        Assert.Equal(15, gitKnowledge.Arguments["main"].UsageCount);
    }

    [Fact]
    public async Task ConcurrentSessions_DifferentCommands_AllPersisted()
    {
        // Arrange - Each session learns a different command
        var commands = new[] { "git", "docker", "kubectl", "az", "gh" };
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < commands.Length; i++)
        {
            var command = commands[i];
            tasks.Add(Task.Run(() =>
            {
                using var persistence = new PersistenceManager(_testDbPath);
                var graph = new ArgumentGraph();

                graph.RecordUsage(command, new[] { "subcommand", "--flag" });
                persistence.SaveArgumentGraph(graph);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All commands should be present
        using var finalPersistence = new PersistenceManager(_testDbPath);
        var finalGraph = finalPersistence.LoadArgumentGraph();

        var trackedCommands = finalGraph.GetTrackedCommands();
        Assert.Equal(commands.Length, trackedCommands.Count);

        foreach (var command in commands)
        {
            Assert.Contains(command, trackedCommands, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task StressTest_100ConcurrentWrites_DatabaseIntegrity()
    {
        // Arrange - Stress test with many concurrent writers
        const int writerCount = 100;
        const int commandsPerWriter = 5;

        var tasks = new List<Task>();

        // Act - 100 concurrent writers
        for (int i = 0; i < writerCount; i++)
        {
            var writerId = i;
            tasks.Add(Task.Run(() =>
            {
                using var persistence = new PersistenceManager(_testDbPath);
                var graph = new ArgumentGraph();

                for (int j = 0; j < commandsPerWriter; j++)
                {
                    graph.RecordUsage("stress-test", new[] { $"arg{writerId % 10}" });
                }

                persistence.SaveArgumentGraph(graph);
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert - Database should be intact and consistent
        using var finalPersistence = new PersistenceManager(_testDbPath);
        var finalGraph = finalPersistence.LoadArgumentGraph();

        var stressKnowledge = finalGraph.GetCommandKnowledge("stress-test");
        Assert.NotNull(stressKnowledge);

        // Total should be sum of all writes
        var expectedTotal = writerCount * commandsPerWriter;
        Assert.Equal(expectedTotal, stressKnowledge.TotalUsageCount);
    }

    [Fact]
    public async Task ConcurrentSessions_CoOccurrences_MergedCorrectly()
    {
        // Arrange - Multiple sessions learning flag combinations
        const int sessionCount = 5;
        var tasks = new List<Task>();

        // Act - Each session records "git commit -a -m"
        for (int i = 0; i < sessionCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                using var persistence = new PersistenceManager(_testDbPath);
                var graph = new ArgumentGraph();

                graph.RecordUsage("git", new[] { "commit", "-a", "-m", "message" });
                persistence.SaveArgumentGraph(graph);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Co-occurrences should be merged
        using var finalPersistence = new PersistenceManager(_testDbPath);
        var finalGraph = finalPersistence.LoadArgumentGraph();

        var gitKnowledge = finalGraph.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);

        var commitStats = gitKnowledge.Arguments["commit"];
        Assert.Contains("-a", commitStats.CoOccurrences.Keys);
        Assert.Contains("-m", commitStats.CoOccurrences.Keys);

        // Co-occurrence count should reflect all sessions
        var actualCount = commitStats.CoOccurrences["-a"];
        Assert.True(actualCount >= sessionCount,
            $"Expected co-occurrence count >= {sessionCount}, but got {actualCount}");
    }

    [Fact]
    public async Task ConcurrentSessions_TimestampPreservation_UsesMaxTimestamp()
    {
        // Arrange
        var tasks = new List<Task>();
        var startTime = DateTime.UtcNow;

        // Act - 3 sessions with staggered timestamps
        for (int i = 0; i < 3; i++)
        {
            var delay = i * 100; // 0ms, 100ms, 200ms
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(delay);

                using var persistence = new PersistenceManager(_testDbPath);
                var graph = new ArgumentGraph();

                graph.RecordUsage("git", new[] { "push" });
                persistence.SaveArgumentGraph(graph);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - LastUsed should be the most recent timestamp
        using var finalPersistence = new PersistenceManager(_testDbPath);
        var finalGraph = finalPersistence.LoadArgumentGraph();

        var gitKnowledge = finalGraph.GetCommandKnowledge("git");
        Assert.NotNull(gitKnowledge);

        // LastUsed should be close to current time (within 1 second)
        var timeDiff = DateTime.UtcNow - gitKnowledge.LastUsed;
        Assert.True(timeDiff.TotalSeconds < 2, $"Timestamp difference: {timeDiff.TotalSeconds}s");

        // LastUsed should be after start time
        Assert.True(gitKnowledge.LastUsed >= startTime);
    }

    [Fact]
    public void DatabaseLocking_NoCorruption_AfterManyOperations()
    {
        // Arrange - Rapid sequence of save/load operations
        const int iterations = 50;

        // Act - Rapidly save and load
        for (int i = 0; i < iterations; i++)
        {
            using var persistence = new PersistenceManager(_testDbPath);

            // Save
            var graph = new ArgumentGraph();
            graph.RecordUsage("test", new[] { $"arg{i}" });
            persistence.SaveArgumentGraph(graph);

            // Immediate load
            var loaded = persistence.LoadArgumentGraph();
            Assert.NotNull(loaded);
        }

        // Assert - Database should still be valid
        using var finalPersistence = new PersistenceManager(_testDbPath);
        var finalGraph = finalPersistence.LoadArgumentGraph();

        var testKnowledge = finalGraph.GetCommandKnowledge("test");
        Assert.NotNull(testKnowledge);
        Assert.Equal(iterations, testKnowledge.TotalUsageCount);
    }
}
