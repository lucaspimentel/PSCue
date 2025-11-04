using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

/// <summary>
/// Unit tests for the SequencePredictor (N-gram ML prediction).
/// Tests bigram/trigram tracking, predictions, caching, and persistence.
/// </summary>
public class SequencePredictorTests
{
    [Fact]
    public void Constructor_DefaultValues_ShouldSucceed()
    {
        // Act
        var predictor = new SequencePredictor();

        // Assert
        var (cacheEntries, deltaEntries, ngramOrder, minFrequency) = predictor.GetDiagnostics();
        Assert.Equal(0, cacheEntries);
        Assert.Equal(0, deltaEntries);
        Assert.Equal(2, ngramOrder); // Default bigrams
        Assert.Equal(3, minFrequency); // Default min frequency
    }

    [Fact]
    public void Constructor_CustomValues_ShouldSucceed()
    {
        // Act
        var predictor = new SequencePredictor(ngramOrder: 3, minFrequency: 5);

        // Assert
        var (_, _, ngramOrder, minFrequency) = predictor.GetDiagnostics();
        Assert.Equal(3, ngramOrder);
        Assert.Equal(5, minFrequency);
    }

    [Theory]
    [InlineData(1)] // Too low
    [InlineData(4)] // Too high
    public void Constructor_InvalidNgramOrder_ShouldThrow(int invalidOrder)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequencePredictor(ngramOrder: invalidOrder));
    }

    [Fact]
    public void Constructor_InvalidMinFrequency_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequencePredictor(minFrequency: 0));
    }

    [Fact]
    public void RecordSequence_Bigrams_ShouldTrackTwoCommandSequence()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        var commands = new[] { "git", "add", "commit" };

        // Act
        predictor.RecordSequence(commands);

        // Assert
        var predictions = predictor.GetPredictions(new[] { "git" });
        Assert.Contains(predictions, p => p.nextCommand == "add");

        predictions = predictor.GetPredictions(new[] { "add" });
        Assert.Contains(predictions, p => p.nextCommand == "commit");
    }

    [Fact]
    public void RecordSequence_Trigrams_ShouldTrackThreeCommandSequence()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 3, minFrequency: 1);
        var commands = new[] { "git", "add", "commit", "push" };

        // Act
        predictor.RecordSequence(commands);

        // Assert
        // Trigram: (git, add) -> commit
        var predictions = predictor.GetPredictions(new[] { "git", "add" });
        Assert.Contains(predictions, p => p.nextCommand == "commit");

        // Trigram: (add, commit) -> push
        predictions = predictor.GetPredictions(new[] { "add", "commit" });
        Assert.Contains(predictions, p => p.nextCommand == "push");
    }

    [Fact]
    public void RecordSequence_MultipleOccurrences_ShouldIncreaseFrequency()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);

        // Act - Record sequence multiple times
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "add" });

        // Assert
        var delta = predictor.GetDelta();
        Assert.True(delta.ContainsKey("git"));
        Assert.Equal(3, delta["git"]["add"].frequency);
    }

    [Fact]
    public void RecordSequence_NullOrEmpty_ShouldNotThrow()
    {
        // Arrange
        var predictor = new SequencePredictor();

        // Act & Assert - Should not throw
        predictor.RecordSequence(null!);
        predictor.RecordSequence(Array.Empty<string>());
        predictor.RecordSequence(new[] { "single" }); // Not enough for bigram
    }

    [Fact]
    public void GetPredictions_Bigrams_ShouldReturnCorrectNextCommands()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);

        // Record: git -> add (3 times), git -> status (1 time)
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "status" });

        // Act
        var predictions = predictor.GetPredictions(new[] { "git" });

        // Assert
        Assert.Equal(2, predictions.Count); // Both "add" and "status"
        Assert.Equal("add", predictions[0].nextCommand); // Higher frequency -> higher score
        Assert.True(predictions[0].score > predictions[1].score);
    }

    [Fact]
    public void GetPredictions_WithMinFrequency_ShouldFilterLowFrequencyItems()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 3);

        // Record: git -> add (3 times), git -> status (1 time)
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "status" });

        // Act
        var predictions = predictor.GetPredictions(new[] { "git" });

        // Assert
        Assert.Single(predictions); // Only "add" (frequency >= 3)
        Assert.Equal("add", predictions[0].nextCommand);
    }

    [Fact]
    public void GetPredictions_NoHistory_ShouldReturnEmpty()
    {
        // Arrange
        var predictor = new SequencePredictor();

        // Act
        var predictions = predictor.GetPredictions(new[] { "unknown" });

        // Assert
        Assert.Empty(predictions);
    }

    [Fact]
    public void GetPredictions_NullOrEmpty_ShouldReturnEmpty()
    {
        // Arrange
        var predictor = new SequencePredictor();

        // Act & Assert
        Assert.Empty(predictor.GetPredictions(null!));
        Assert.Empty(predictor.GetPredictions(Array.Empty<string>()));
    }

    [Fact]
    public void GetPredictions_Trigrams_InsufficientHistory_ShouldReturnEmpty()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 3, minFrequency: 1);
        predictor.RecordSequence(new[] { "git", "add", "commit" });

        // Act - Only one command in history (need two for trigrams)
        var predictions = predictor.GetPredictions(new[] { "git" });

        // Assert
        Assert.Empty(predictions);
    }

    [Fact]
    public void GetPredictions_RecencyBoost_ShouldAffectScore()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);

        // Record same frequency for both, but different timestamps
        predictor.RecordSequence(new[] { "git", "add" });
        Thread.Sleep(100); // Ensure different timestamps
        predictor.RecordSequence(new[] { "git", "commit" });

        // Act
        var predictions = predictor.GetPredictions(new[] { "git" });

        // Assert
        Assert.Equal(2, predictions.Count);
        // "commit" is more recent, should have higher score (with recency boost)
        Assert.Equal("commit", predictions[0].nextCommand);
    }

    [Fact]
    public void Initialize_WithSequences_ShouldPopulateCache()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        var sequences = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>
        {
            ["git"] = new Dictionary<string, (int frequency, DateTime lastSeen)>
            {
                ["add"] = (5, DateTime.UtcNow),
                ["status"] = (3, DateTime.UtcNow)
            }
        };

        // Act
        predictor.Initialize(sequences);

        // Assert
        var predictions = predictor.GetPredictions(new[] { "git" });
        Assert.Equal(2, predictions.Count);
        Assert.Equal("add", predictions[0].nextCommand); // Higher frequency
    }

    [Fact]
    public void Initialize_NullSequences_ShouldThrow()
    {
        // Arrange
        var predictor = new SequencePredictor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => predictor.Initialize(null!));
    }

    [Fact]
    public void GetDelta_AfterRecording_ShouldReturnNewSequences()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2);

        // Act
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "commit" });
        var delta = predictor.GetDelta();

        // Assert
        Assert.Single(delta); // One prev_command: "git"
        Assert.Equal(2, delta["git"].Count); // Two next_commands: "add", "commit"
        Assert.True(delta["git"].ContainsKey("add"));
        Assert.True(delta["git"].ContainsKey("commit"));
    }

    [Fact]
    public void ClearDelta_ShouldResetDelta()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2);
        predictor.RecordSequence(new[] { "git", "add" });

        // Act
        predictor.ClearDelta();
        var delta = predictor.GetDelta();

        // Assert
        Assert.Empty(delta);
    }

    [Fact]
    public void ClearDelta_ShouldNotAffectCache()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        predictor.RecordSequence(new[] { "git", "add" });

        // Act
        predictor.ClearDelta();
        var predictions = predictor.GetPredictions(new[] { "git" });

        // Assert
        Assert.Single(predictions); // Cache still has the sequence
        Assert.Equal("add", predictions[0].nextCommand);
    }

    [Fact]
    public void CaseInsensitive_ShouldTreatCommandsEqual()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);

        // Act - Record with different casing
        predictor.RecordSequence(new[] { "Git", "Add" });
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "GIT", "ADD" });

        // Assert
        var delta = predictor.GetDelta();
        Assert.Single(delta); // All treated as "git"
        Assert.Equal(3, delta.First().Value.First().Value.frequency);
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var predictor = new SequencePredictor();

        // Act & Assert - Should not throw
        predictor.Dispose();
        predictor.Dispose(); // Double dispose should be safe
    }

    [Fact]
    public void RecordSequence_ConcurrentAccess_ShouldBeSafe()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        var threads = new List<Thread>();

        // Act - Record from multiple threads
        for (int i = 0; i < 10; i++)
        {
            var thread = new Thread(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    predictor.RecordSequence(new[] { "git", "add" });
                }
            });
            threads.Add(thread);
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        // Assert
        var delta = predictor.GetDelta();
        Assert.Equal(1000, delta["git"]["add"].frequency);
    }

    [Fact]
    public void ScoreCalculation_ShouldBeProbabilityPlusRecency()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);

        // Record: git -> add (7 times), git -> commit (3 times)
        for (int i = 0; i < 7; i++)
            predictor.RecordSequence(new[] { "git", "add" });
        for (int i = 0; i < 3; i++)
            predictor.RecordSequence(new[] { "git", "commit" });

        // Act
        var predictions = predictor.GetPredictions(new[] { "git" });

        // Assert
        Assert.Equal(2, predictions.Count);

        // "add" probability: 7/10 = 0.7
        // Score should be ~70% probability + ~30% recency
        Assert.InRange(predictions[0].score, 0.5, 1.0);
        Assert.True(predictions[0].score > predictions[1].score); // Higher frequency -> higher score
    }

    [Fact]
    public void GetDiagnostics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 3);

        // Act
        predictor.RecordSequence(new[] { "git", "add" });
        predictor.RecordSequence(new[] { "git", "commit" });
        predictor.RecordSequence(new[] { "docker", "build" });

        var (cacheEntries, deltaEntries, ngramOrder, minFrequency) = predictor.GetDiagnostics();

        // Assert
        Assert.Equal(3, cacheEntries); // git->add, git->commit, docker->build
        Assert.Equal(3, deltaEntries);
        Assert.Equal(2, ngramOrder);
        Assert.Equal(3, minFrequency);
    }
}
