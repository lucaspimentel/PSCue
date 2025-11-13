using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

/// <summary>
/// Unit tests for the WorkflowLearner (dynamic workflow learning).
/// Tests transition tracking, predictions, time-sensitive scoring, and persistence.
/// </summary>
public class WorkflowLearnerTests
{
    [Fact]
    public void Constructor_DefaultValues_ShouldSucceed()
    {
        // Act
        var learner = new WorkflowLearner();

        // Assert
        var (totalTransitions, uniqueCommands, deltaTransitions) = learner.GetDiagnostics();
        Assert.Equal(0, totalTransitions);
        Assert.Equal(0, uniqueCommands);
        Assert.Equal(0, deltaTransitions);
    }

    [Fact]
    public void Constructor_CustomValues_ShouldSucceed()
    {
        // Act
        var learner = new WorkflowLearner(
            minFrequency: 10,
            maxTimeDeltaMinutes: 30,
            minConfidence: 0.8,
            decayDays: 60
        );

        // Assert - no exceptions thrown
        Assert.NotNull(learner);
    }

    [Theory]
    [InlineData(0)]  // Too low
    [InlineData(-1)] // Negative
    public void Constructor_InvalidMinFrequency_ShouldThrow(int invalidFreq)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkflowLearner(minFrequency: invalidFreq));
    }

    [Theory]
    [InlineData(0)]  // Too low
    [InlineData(-5)] // Negative
    public void Constructor_InvalidMaxTimeDelta_ShouldThrow(int invalidTime)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkflowLearner(maxTimeDeltaMinutes: invalidTime));
    }

    [Theory]
    [InlineData(-0.1)] // Negative
    [InlineData(1.5)]  // Too high
    public void Constructor_InvalidMinConfidence_ShouldThrow(double invalidConf)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkflowLearner(minConfidence: invalidConf));
    }

    [Fact]
    public void RecordTransition_SimpleWorkflow_ShouldTrackTransition()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);

        // Act
        learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));

        // Assert
        var predictions = learner.GetNextCommandPredictions("git add");
        Assert.Single(predictions);
        Assert.Equal("git commit", predictions[0].Command);
    }

    [Fact]
    public void RecordTransition_MultipleOccurrences_ShouldIncreaseFrequency()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);

        // Act
        for (int i = 0; i < 10; i++)
        {
            learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(45));
        }

        // Assert
        var predictions = learner.GetNextCommandPredictions("git add");
        Assert.Single(predictions);
        Assert.True(predictions[0].Confidence > 0.5); // Should have high confidence after 10 occurrences
    }

    [Fact]
    public void RecordTransition_BelowMinFrequency_ShouldNotPredict()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 5, minConfidence: 0.01);

        // Act - Record only 3 times (below threshold)
        for (int i = 0; i < 3; i++)
        {
            learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));
        }

        // Assert
        var predictions = learner.GetNextCommandPredictions("git add");
        Assert.Empty(predictions); // Should not predict (below min frequency)
    }

    [Fact]
    public void RecordTransition_AboveMaxTimeDelta_ShouldIgnore()
    {
        // Arrange
        var learner = new WorkflowLearner(maxTimeDeltaMinutes: 15);

        // Act - Record with 20 minute gap (above threshold)
        learner.RecordTransition("git add", "git commit", TimeSpan.FromMinutes(20));

        // Assert
        var (totalTransitions, _, _) = learner.GetDiagnostics();
        Assert.Equal(0, totalTransitions); // Should be ignored
    }

    [Fact]
    public void RecordTransition_SelfTransition_ShouldIgnore()
    {
        // Arrange
        var learner = new WorkflowLearner();

        // Act - Same command twice
        learner.RecordTransition("git status", "git status", TimeSpan.FromSeconds(5));

        // Assert
        var (totalTransitions, _, _) = learner.GetDiagnostics();
        Assert.Equal(0, totalTransitions); // Self-transitions ignored
    }

    [Fact]
    public void RecordTransition_NullOrEmpty_ShouldIgnore()
    {
        // Arrange
        var learner = new WorkflowLearner();

        // Act
        learner.RecordTransition(null!, "git commit", TimeSpan.FromSeconds(5));
        learner.RecordTransition("git add", null!, TimeSpan.FromSeconds(5));
        learner.RecordTransition("", "git commit", TimeSpan.FromSeconds(5));
        learner.RecordTransition("git add", "", TimeSpan.FromSeconds(5));

        // Assert
        var (totalTransitions, _, _) = learner.GetDiagnostics();
        Assert.Equal(0, totalTransitions); // All ignored
    }

    [Fact]
    public void GetNextCommandPredictions_MultipleTransitions_ShouldRankByFrequency()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);

        // Act - Record different frequencies
        for (int i = 0; i < 15; i++)
            learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));

        for (int i = 0; i < 5; i++)
            learner.RecordTransition("git add", "git status", TimeSpan.FromSeconds(10));

        // Assert
        var predictions = learner.GetNextCommandPredictions("git add");
        Assert.Equal(2, predictions.Count);
        Assert.Equal("git commit", predictions[0].Command); // Higher frequency first
        Assert.Equal("git status", predictions[1].Command);
    }

    [Fact]
    public void GetNextCommandPredictions_TimeSensitiveScoring_ShouldAdjustConfidence()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);

        // Act - Record transition with average time of 30 seconds
        for (int i = 0; i < 10; i++)
        {
            learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));
        }

        // Assert - Within expected timeframe (high boost)
        var predictions1 = learner.GetNextCommandPredictions("git add", TimeSpan.FromSeconds(30));
        var confidence1 = predictions1[0].Confidence;

        // Assert - Much later than expected (lower boost)
        var predictions2 = learner.GetNextCommandPredictions("git add", TimeSpan.FromHours(2));
        var confidence2 = predictions2[0].Confidence;

        Assert.True(confidence1 > confidence2, "Confidence should be higher for expected timing");
    }

    [Fact]
    public void GetNextCommandPredictions_BelowMinConfidence_ShouldFilter()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.9); // Very high threshold

        // Act - Record only once (low confidence)
        learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));

        // Assert
        var predictions = learner.GetNextCommandPredictions("git add");
        Assert.Empty(predictions); // Filtered out by confidence threshold
    }

    [Fact]
    public void GetNextCommandPredictions_MaxResults_ShouldLimit()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);

        // Act - Record 10 different transitions
        for (int i = 0; i < 10; i++)
        {
            learner.RecordTransition("git add", $"command{i}", TimeSpan.FromSeconds(30));
        }

        // Assert
        var predictions = learner.GetNextCommandPredictions("git add", maxResults: 3);
        Assert.Equal(3, predictions.Count); // Limited to 3
    }

    [Fact]
    public void GetNextCommandPredictions_UnknownCommand_ShouldReturnEmpty()
    {
        // Arrange
        var learner = new WorkflowLearner();

        // Act
        var predictions = learner.GetNextCommandPredictions("unknown-command");

        // Assert
        Assert.Empty(predictions);
    }

    [Fact]
    public void Initialize_LoadsWorkflowData_ShouldPopulateGraph()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);
        var workflows = new Dictionary<string, Dictionary<string, WorkflowTransition>>
        {
            ["git add"] = new Dictionary<string, WorkflowTransition>
            {
                ["git commit"] = new WorkflowTransition
                {
                    NextCommand = "git commit",
                    Frequency = 10,
                    TotalTimeDeltaMs = 300000, // 5 minutes total
                    FirstSeen = DateTime.UtcNow.AddDays(-7),
                    LastSeen = DateTime.UtcNow
                }
            }
        };

        // Act
        learner.Initialize(workflows);

        // Assert
        var predictions = learner.GetNextCommandPredictions("git add");
        Assert.Single(predictions);
        Assert.Equal("git commit", predictions[0].Command);
    }

    [Fact]
    public void GetDelta_AfterRecording_ShouldReturnChanges()
    {
        // Arrange
        var learner = new WorkflowLearner();

        // Act
        learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));

        var delta = learner.GetDelta();

        // Assert
        Assert.Single(delta);
        Assert.True(delta.ContainsKey("git add"));
        Assert.Single(delta["git add"]);
        Assert.True(delta["git add"].ContainsKey("git commit"));
    }

    [Fact]
    public void ClearDelta_AfterRecording_ShouldClearChanges()
    {
        // Arrange
        var learner = new WorkflowLearner();
        learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));

        // Act
        learner.ClearDelta();

        // Assert
        var delta = learner.GetDelta();
        Assert.Empty(delta);
    }

    [Fact]
    public void GetAllWorkflows_ShouldReturnAllTransitions()
    {
        // Arrange
        var learner = new WorkflowLearner();
        learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));
        learner.RecordTransition("git commit", "git push", TimeSpan.FromSeconds(45));

        // Act
        var workflows = learner.GetAllWorkflows();

        // Assert
        Assert.Equal(2, workflows.Count); // Two source commands
        Assert.True(workflows.ContainsKey("git add"));
        Assert.True(workflows.ContainsKey("git commit"));
    }

    [Fact]
    public void Clear_AfterRecording_ShouldClearAll()
    {
        // Arrange
        var learner = new WorkflowLearner();
        learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));
        learner.RecordTransition("git commit", "git push", TimeSpan.FromSeconds(45));

        // Act
        learner.Clear();

        // Assert
        var (totalTransitions, uniqueCommands, deltaTransitions) = learner.GetDiagnostics();
        Assert.Equal(0, totalTransitions);
        Assert.Equal(0, uniqueCommands);
        Assert.Equal(0, deltaTransitions);
    }

    [Fact]
    public void RecordTransition_CommandNormalization_ShouldExtractBaseCommand()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);

        // Act - Record with full command line (including arguments)
        learner.RecordTransition("git add .", "git commit -m \"test\"", TimeSpan.FromSeconds(30));

        // Assert - Should normalize to base command + subcommand
        var predictions = learner.GetNextCommandPredictions("git add");
        Assert.Single(predictions);
        Assert.Equal("git commit", predictions[0].Command);
    }

    [Fact]
    public void RecordTransition_SingleWordCommand_ShouldPreserve()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);

        // Act - Single word commands
        learner.RecordTransition("ls", "cd", TimeSpan.FromSeconds(5));

        // Assert
        var predictions = learner.GetNextCommandPredictions("ls");
        Assert.Single(predictions);
        Assert.Equal("cd", predictions[0].Command);
    }

    [Fact]
    public void RecordTransition_MemoryLimit_ShouldEvictLeastFrequent()
    {
        // Arrange
        var learner = new WorkflowLearner(minFrequency: 1, minConfidence: 0.0);

        // Act - Record 25 different transitions (exceeds max of 20)
        for (int i = 0; i < 25; i++)
        {
            // First transition gets higher frequency
            int frequency = i == 0 ? 10 : 1;
            for (int j = 0; j < frequency; j++)
            {
                learner.RecordTransition("git add", $"command{i}", TimeSpan.FromSeconds(30));
            }
        }

        // Assert - Should keep only top 20
        var workflows = learner.GetAllWorkflows();
        Assert.True(workflows["git add"].Count <= 20, "Should enforce memory limits");

        // Assert - Most frequent transition should still be there
        var predictions = learner.GetNextCommandPredictions("git add");
        Assert.Contains(predictions, p => p.Command == "command0");
    }

    [Fact]
    public void WorkflowTransition_GetConfidence_ShouldCalculateCorrectly()
    {
        // Arrange
        var transition = new WorkflowTransition
        {
            NextCommand = "git commit",
            Frequency = 10,
            TotalTimeDeltaMs = 300000,
            FirstSeen = DateTime.UtcNow.AddDays(-7),
            LastSeen = DateTime.UtcNow
        };

        // Act
        var confidence = transition.GetConfidence(minFrequency: 5, decayDays: 30);

        // Assert
        Assert.True(confidence > 0.0);
        Assert.True(confidence <= 1.0);
    }

    [Fact]
    public void WorkflowTransition_GetTimeSensitiveScore_WithinExpectedTime_ShouldBoost()
    {
        // Arrange
        var transition = new WorkflowTransition
        {
            NextCommand = "git commit",
            Frequency = 10,
            TotalTimeDeltaMs = 300000, // Average: 30 seconds
            FirstSeen = DateTime.UtcNow.AddDays(-7),
            LastSeen = DateTime.UtcNow
        };

        // Act
        var scoreWithinExpected = transition.GetTimeSensitiveScore(
            TimeSpan.FromSeconds(30), // Within expected
            minFrequency: 5
        );

        var scoreDelayed = transition.GetTimeSensitiveScore(
            TimeSpan.FromHours(2), // Much later
            minFrequency: 5
        );

        // Assert
        Assert.True(scoreWithinExpected > scoreDelayed, "Within expected time should have higher score");
    }

    [Fact]
    public void WorkflowTransition_AverageTimeDelta_ShouldCalculateCorrectly()
    {
        // Arrange
        var transition = new WorkflowTransition
        {
            NextCommand = "git commit",
            Frequency = 10,
            TotalTimeDeltaMs = 300000 // 5 minutes total
        };

        // Act
        var averageTimeDelta = transition.AverageTimeDelta;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), averageTimeDelta); // 300000ms / 10 = 30000ms = 30s
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var learner = new WorkflowLearner();
        learner.RecordTransition("git add", "git commit", TimeSpan.FromSeconds(30));

        // Act & Assert
        learner.Dispose();
        learner.Dispose(); // Double dispose should be safe
    }
}
