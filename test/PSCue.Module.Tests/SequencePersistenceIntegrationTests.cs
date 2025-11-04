using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

/// <summary>
/// Integration tests for SequencePredictor persistence with PersistenceManager.
/// Tests end-to-end flow of recording, saving, and loading command sequences.
/// </summary>
public class SequencePersistenceIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PersistenceManager _persistence;

    public SequencePersistenceIntegrationTests()
    {
        // Create temp database for testing
        _testDbPath = Path.Combine(Path.GetTempPath(), $"pscue-test-{Guid.NewGuid()}.db");
        _persistence = new PersistenceManager(_testDbPath);
    }

    public void Dispose()
    {
        _persistence?.Dispose();

        // Clean up test database files
        try
        {
            if (File.Exists(_testDbPath))
                File.Delete(_testDbPath);

            // Clean up SQLite WAL files
            var walPath = _testDbPath + "-wal";
            if (File.Exists(walPath))
                File.Delete(walPath);

            var shmPath = _testDbPath + "-shm";
            if (File.Exists(shmPath))
                File.Delete(shmPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void EndToEnd_RecordSaveLoad_ShouldPersistSequences()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);

        // Record some sequences
        predictor.RecordSequence(new[] { "git", "add", "commit", "push" });
        predictor.RecordSequence(new[] { "docker", "build", "run" });

        // Act - Save to database
        var delta = predictor.GetDelta();
        _persistence.SaveCommandSequences(delta);

        // Load into new predictor
        var loadedSequences = _persistence.LoadCommandSequences();
        var newPredictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        newPredictor.Initialize(loadedSequences);

        // Assert - Predictions should match
        var predictions = newPredictor.GetPredictions(new[] { "git" });
        Assert.Contains(predictions, p => p.nextCommand == "add");

        predictions = newPredictor.GetPredictions(new[] { "docker" });
        Assert.Contains(predictions, p => p.nextCommand == "build");
    }

    [Fact]
    public void SaveLoad_EmptySequences_ShouldSucceed()
    {
        // Arrange
        var predictor = new SequencePredictor();
        var delta = predictor.GetDelta();

        // Act - Save empty delta
        _persistence.SaveCommandSequences(delta);

        // Load
        var loaded = _persistence.LoadCommandSequences();

        // Assert
        Assert.Empty(loaded);
    }

    [Fact]
    public void SaveCommandSequences_AdditiveMerging_ShouldIncrementFrequencies()
    {
        // Arrange
        var sequences1 = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>
        {
            ["git"] = new Dictionary<string, (int frequency, DateTime lastSeen)>
            {
                ["add"] = (3, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            }
        };

        var sequences2 = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>
        {
            ["git"] = new Dictionary<string, (int frequency, DateTime lastSeen)>
            {
                ["add"] = (2, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc))
            }
        };

        // Act - Save twice (additive merge)
        _persistence.SaveCommandSequences(sequences1);
        _persistence.SaveCommandSequences(sequences2);

        // Load
        var loaded = _persistence.LoadCommandSequences();

        // Assert - Frequencies should be summed (3 + 2 = 5)
        Assert.Equal(5, loaded["git"]["add"].frequency);

        // Timestamp should be max (most recent)
        Assert.Equal(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), loaded["git"]["add"].lastSeen);
    }

    [Fact]
    public void SaveCommandSequences_MultipleCommands_ShouldPersistAll()
    {
        // Arrange
        var sequences = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>
        {
            ["git"] = new Dictionary<string, (int frequency, DateTime lastSeen)>
            {
                ["add"] = (5, DateTime.UtcNow),
                ["commit"] = (3, DateTime.UtcNow),
                ["push"] = (2, DateTime.UtcNow)
            },
            ["docker"] = new Dictionary<string, (int frequency, DateTime lastSeen)>
            {
                ["build"] = (4, DateTime.UtcNow),
                ["run"] = (1, DateTime.UtcNow)
            }
        };

        // Act
        _persistence.SaveCommandSequences(sequences);
        var loaded = _persistence.LoadCommandSequences();

        // Assert
        Assert.Equal(2, loaded.Count); // git, docker
        Assert.Equal(3, loaded["git"].Count); // add, commit, push
        Assert.Equal(2, loaded["docker"].Count); // build, run
    }

    [Fact]
    public void LoadCommandSequences_EmptyDatabase_ShouldReturnEmpty()
    {
        // Act
        var loaded = _persistence.LoadCommandSequences();

        // Assert
        Assert.NotNull(loaded);
        Assert.Empty(loaded);
    }

    [Fact]
    public void FullWorkflow_WithDeltaPattern_ShouldWork()
    {
        // This tests the real-world pattern: load -> record -> get delta -> save delta -> clear delta

        // Arrange - Simulate initial state
        var initialSequences = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>
        {
            ["git"] = new Dictionary<string, (int frequency, DateTime lastSeen)>
            {
                ["add"] = (10, DateTime.UtcNow.AddDays(-1))
            }
        };
        _persistence.SaveCommandSequences(initialSequences);

        // Act 1 - Load existing data
        var loaded = _persistence.LoadCommandSequences();
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        predictor.Initialize(loaded);

        // Act 2 - Record new usage (delta)
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "commit" });

        // Act 3 - Save delta and clear
        var delta = predictor.GetDelta();
        _persistence.SaveCommandSequences(delta);
        predictor.ClearDelta();

        // Act 4 - Load again to verify merge
        var reloaded = _persistence.LoadCommandSequences();

        // Assert - git->add frequency should be 10 + 1 = 11
        Assert.Equal(11, reloaded["git"]["add"].frequency);
        // git->commit should be new (frequency = 1)
        Assert.Equal(1, reloaded["git"]["commit"].frequency);
    }

    [Fact]
    public void ConcurrentSessions_AdditiveMerge_ShouldNotLoseData()
    {
        // Simulate two PowerShell sessions saving concurrently

        // Session 1 records git->add (3 times)
        var predictor1 = new SequencePredictor(ngramOrder: 2);
        for (int i = 0; i < 3; i++)
            predictor1.RecordSequence(new[] { "git", "add" });

        // Session 2 records git->add (2 times)
        var predictor2 = new SequencePredictor(ngramOrder: 2);
        for (int i = 0; i < 2; i++)
            predictor2.RecordSequence(new[] { "git", "add" });

        // Both save their deltas
        _persistence.SaveCommandSequences(predictor1.GetDelta());
        _persistence.SaveCommandSequences(predictor2.GetDelta());

        // Load and verify
        var loaded = _persistence.LoadCommandSequences();

        // Should have total of 5 (3 + 2)
        Assert.Equal(5, loaded["git"]["add"].frequency);
    }
}
