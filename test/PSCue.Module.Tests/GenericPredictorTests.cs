using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

public class GenericPredictorTests
{
    private GenericPredictor CreatePredictor(out CommandHistory history, out ArgumentGraph graph)
    {
        history = new CommandHistory();
        graph = new ArgumentGraph();
        var analyzer = new ContextAnalyzer();
        return new GenericPredictor(history, graph, analyzer);
    }

    [Fact]
    public void GetSuggestions_EmptyHistory_ReturnsEmpty()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out _);

        // Act
        var suggestions = predictor.GetSuggestions("git commit");

        // Assert
        Assert.Empty(suggestions);
    }

    [Fact]
    public void GetSuggestions_WithLearnedData_ReturnsSuggestions()
    {
        // Arrange
        var predictor = CreatePredictor(out var history, out var graph);
        graph.RecordUsage("git", new[] { "commit", "-m" });
        graph.RecordUsage("git", new[] { "commit", "-a" });
        graph.RecordUsage("git", new[] { "commit", "-m" });

        // Act
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Text == "commit");
        Assert.Contains(suggestions, s => s.Text == "-m");
    }

    [Fact]
    public void GetSuggestions_SortsByScore()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        // "commit" used 3 times
        graph.RecordUsage("git", new[] { "commit" });
        graph.RecordUsage("git", new[] { "commit" });
        graph.RecordUsage("git", new[] { "commit" });
        // "push" used 1 time
        graph.RecordUsage("git", new[] { "push" });
        // "status" used 2 times
        graph.RecordUsage("git", new[] { "status" });
        graph.RecordUsage("git", new[] { "status" });

        // Act
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        Assert.NotEmpty(suggestions);
        // Should be sorted by frequency: commit (3) > status (2) > push (1)
        Assert.Equal("commit", suggestions[0].Text);
        Assert.Equal("status", suggestions[1].Text);
        Assert.Equal("push", suggestions[2].Text);
    }

    [Fact]
    public void GetSuggestions_AppliesContextBoost()
    {
        // Arrange
        var predictor = CreatePredictor(out var history, out var graph);
        // Record git add (should boost commit flags)
        history.Add("git", "git add .", new[] { "add", "." }, success: true);
        graph.RecordUsage("git", new[] { "add", "." });
        graph.RecordUsage("git", new[] { "commit", "-m" });
        graph.RecordUsage("git", new[] { "push" });

        // Act
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        Assert.NotEmpty(suggestions);
        // After "git add", commit-related suggestions should be boosted
        var commitSuggestion = suggestions.FirstOrDefault(s => s.Text == "commit");
        Assert.NotNull(commitSuggestion);
        // Should have higher score due to context boost
        Assert.True(commitSuggestion.Score > 0.5);
    }

    [Fact]
    public void GetSuggestions_IncludesDescription()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        graph.RecordUsage("git", new[] { "commit", "-m" });

        // Act
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s =>
        {
            Assert.NotNull(s.Description);
            Assert.NotEmpty(s.Description);
        });
    }

    [Fact]
    public void GetSuggestions_IdentifiesFlags()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        graph.RecordUsage("git", new[] { "commit", "-m", "--amend" });

        // Act
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        var commitSuggestion = suggestions.First(s => s.Text == "commit");
        var mFlagSuggestion = suggestions.First(s => s.Text == "-m");

        Assert.False(commitSuggestion.IsFlag);
        Assert.True(mFlagSuggestion.IsFlag);
    }

    [Fact]
    public void GetSuggestions_LimitsResults()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        for (int i = 0; i < 20; i++)
        {
            graph.RecordUsage("test", new[] { $"arg{i}" });
        }

        // Act
        var suggestions = predictor.GetSuggestions("test", maxResults: 5);

        // Assert
        Assert.Equal(5, suggestions.Count);
    }

    [Fact]
    public void GetSuggestions_WorksForUnsupportedCommand()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        graph.RecordUsage("kubectl", new[] { "get", "pods" });
        graph.RecordUsage("kubectl", new[] { "get", "services" });
        graph.RecordUsage("kubectl", new[] { "describe", "pod" });

        // Act
        var suggestions = predictor.GetSuggestions("kubectl");

        // Assert
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Text == "get");
        Assert.Contains(suggestions, s => s.Text == "describe");
    }

    [Fact]
    public void GetSuggestions_EmptyCommandLine_ReturnsEmpty()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out _);

        // Act
        var suggestions = predictor.GetSuggestions("");

        // Assert
        Assert.Empty(suggestions);
    }

    [Fact]
    public void GetSuggestions_ContextSequences_BoostScore()
    {
        // Arrange
        var predictor = CreatePredictor(out var history, out var graph);
        // Simulate git workflow: add -> commit
        history.Add("git", "git add .", new[] { "add", "." }, success: true);
        history.Add("git", "git commit -m 'test'", new[] { "commit", "-m", "'test'" }, success: true);

        graph.RecordUsage("git", new[] { "add", "." });
        graph.RecordUsage("git", new[] { "commit", "-m", "'test'" });
        graph.RecordUsage("git", new[] { "push" });

        // Act - user types "git" after commit
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        // "push" should be suggested as part of the workflow
        var pushSuggestion = suggestions.FirstOrDefault(s => s.Text == "push");
        Assert.NotNull(pushSuggestion);
    }

    [Fact]
    public void GetSuggestions_RecencyMatters()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        // Old usage
        graph.RecordUsage("git", new[] { "old-subcommand" });
        Thread.Sleep(10);
        // Recent usage (multiple times)
        graph.RecordUsage("git", new[] { "recent-subcommand" });
        graph.RecordUsage("git", new[] { "recent-subcommand" });

        // Act
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        Assert.NotEmpty(suggestions);
        // Recent should be ranked higher despite same frequency
        var recentIndex = suggestions.FindIndex(s => s.Text == "recent-subcommand");
        var oldIndex = suggestions.FindIndex(s => s.Text == "old-subcommand");
        Assert.True(recentIndex < oldIndex, "Recent suggestion should rank higher");
    }

    [Fact]
    public void GetStatistics_ReturnsAccurateData()
    {
        // Arrange
        var predictor = CreatePredictor(out var history, out var graph);
        history.Add("git", "git commit", new[] { "commit" }, success: true);
        history.Add("npm", "npm install", new[] { "install" }, success: true);
        history.Add("docker", "docker ps", new[] { "ps" }, success: false);

        graph.RecordUsage("git", new[] { "commit", "-m" });
        graph.RecordUsage("npm", new[] { "install" });

        // Act
        var stats = predictor.GetStatistics();

        // Assert
        Assert.Equal(3, stats.TotalCommandsTracked);
        Assert.Equal(2, stats.UniqueCommandsLearned);
        Assert.True(stats.TotalArgumentsLearned > 0);
        Assert.True(stats.SuccessRate > 0);
        Assert.NotNull(stats.MostCommonCommand);
    }

    [Fact]
    public void GetSuggestions_MultiWordCommand_ParsesCorrectly()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        graph.RecordUsage("git", new[] { "commit", "-m", "message" });

        // Act
        var suggestions = predictor.GetSuggestions("git commit");

        // Assert
        Assert.NotEmpty(suggestions);
        // Should suggest arguments for "git commit", not "commit" as a separate command
    }

    [Fact]
    public void GetSuggestions_CoOccurrenceDescription()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        // Use -m and -a together multiple times
        graph.RecordUsage("git", new[] { "commit", "-m", "-a" });
        graph.RecordUsage("git", new[] { "commit", "-m", "-a" });

        // Act
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        var mFlagSuggestion = suggestions.FirstOrDefault(s => s.Text == "-m");
        Assert.NotNull(mFlagSuggestion);
        Assert.NotNull(mFlagSuggestion.Description);
        // Description should mention co-occurrence with -a
        Assert.Contains("-a", mFlagSuggestion.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSuggestions_SourceField_IndicatesOrigin()
    {
        // Arrange
        var predictor = CreatePredictor(out var history, out var graph);
        history.Add("git", "git add .", new[] { "add", "." }, success: true);
        graph.RecordUsage("git", new[] { "commit", "-m" });

        // Act
        var suggestions = predictor.GetSuggestions("git");

        // Assert
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.NotNull(s.Source));
        // Should have mix of "learned" and potentially "context" sources
        Assert.Contains(suggestions, s => s.Source == "learned");
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var analyzer = new ContextAnalyzer();

        Assert.Throws<ArgumentNullException>(() => new GenericPredictor(null!, graph, analyzer));
        Assert.Throws<ArgumentNullException>(() => new GenericPredictor(history, null!, analyzer));
        Assert.Throws<ArgumentNullException>(() => new GenericPredictor(history, graph, null!));
    }

    [Fact]
    public void GetSuggestions_CdCommand_FiltersOutCurrentDirectory()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        var currentDir = Directory.GetCurrentDirectory();

        // Record the current directory (should be filtered out)
        graph.RecordUsage("cd", new[] { currentDir }, currentDir);

        // Record other directories (should appear)
        var otherDir = Path.Combine(Path.GetTempPath(), "pscue-test-other");
        Directory.CreateDirectory(otherDir);

        try
        {
            graph.RecordUsage("cd", new[] { otherDir }, currentDir);

            // Act
            var suggestions = predictor.GetSuggestions("cd ");

            // Assert - should not suggest "." (current directory shortcut)
            Assert.DoesNotContain(suggestions, s => s.Text == ".");

            // Assert - should not suggest learned current directory as absolute path
            Assert.DoesNotContain(suggestions, s =>
                s.Source == "learned" && s.Text.Equals(currentDir, StringComparison.OrdinalIgnoreCase));

            // Should suggest other directory (check Text field for absolute path with trailing separator)
            var otherDirWithSeparator = otherDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.Contains(suggestions, s => s.Text.Equals(otherDirWithSeparator, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(otherDir); } catch { }
        }
    }

    [Fact]
    public void GetSuggestions_CdCommand_BoostsFrequentlyVisitedPaths()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        var workingDir = Path.GetTempPath();
        var frequentDir = Path.Combine(workingDir, "frequent");
        var rareDir = Path.Combine(workingDir, "rare");

        Directory.CreateDirectory(frequentDir);
        Directory.CreateDirectory(rareDir);

        try
        {
            // Record frequent directory 10 times
            for (int i = 0; i < 10; i++)
            {
                graph.RecordUsage("cd", new[] { frequentDir }, workingDir);
            }

            // Record rare directory once
            graph.RecordUsage("cd", new[] { rareDir }, workingDir);

            // Act
            var suggestions = predictor.GetSuggestions("cd ");

            // Assert - frequent should have higher score than rare
            var frequentSuggestion = suggestions.FirstOrDefault(s => s.Text.Contains("frequent", StringComparison.OrdinalIgnoreCase));
            var rareSuggestion = suggestions.FirstOrDefault(s => s.Text.Contains("rare", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(frequentSuggestion);
            Assert.NotNull(rareSuggestion);
            Assert.True(frequentSuggestion.Score > rareSuggestion.Score,
                $"Frequent dir score ({frequentSuggestion.Score}) should be higher than rare dir score ({rareSuggestion.Score})");

            // Frequent should appear first when sorted
            var sortedSuggestions = suggestions.OrderByDescending(s => s.Score).ToList();
            var frequentIndex = sortedSuggestions.FindIndex(s => s.Text.Contains("frequent", StringComparison.OrdinalIgnoreCase));
            var rareIndex = sortedSuggestions.FindIndex(s => s.Text.Contains("rare", StringComparison.OrdinalIgnoreCase));

            Assert.True(frequentIndex >= 0 && rareIndex >= 0);
            Assert.True(frequentIndex < rareIndex, "Frequent directory should appear before rare directory");
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(frequentDir); } catch { }
            try { Directory.Delete(rareDir); } catch { }
        }
    }

    [Fact]
    public void GetSuggestions_SetLocationCommand_NormalizesAndFilters()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        var currentDir = Directory.GetCurrentDirectory();
        var targetDir = Path.Combine(Path.GetTempPath(), "test-sl");

        Directory.CreateDirectory(targetDir);

        try
        {
            // Record Set-Location to target directory
            graph.RecordUsage("Set-Location", new[] { targetDir }, currentDir);

            // Record Set-Location to current directory (should be filtered)
            graph.RecordUsage("Set-Location", new[] { currentDir }, currentDir);

            // Act
            var suggestions = predictor.GetSuggestions("Set-Location ");

            // Assert - should not suggest "." (current directory shortcut)
            Assert.DoesNotContain(suggestions, s => s.Text == ".");

            // Assert - should not suggest learned current directory as absolute path
            Assert.DoesNotContain(suggestions, s =>
                s.Source == "learned" && s.Text.Equals(currentDir, StringComparison.OrdinalIgnoreCase));

            // Should suggest target directory (check Text field for absolute path with trailing separator)
            var targetDirWithSeparator = targetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.Contains(suggestions, s => s.Text.Equals(targetDirWithSeparator, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(targetDir); } catch { }
        }
    }

    [Fact]
    public void GetSuggestions_CdCommand_ShowsVisitCount()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        var workingDir = Path.GetTempPath();
        var targetDir = Path.Combine(workingDir, "test-visits");

        Directory.CreateDirectory(targetDir);

        try
        {
            // Record directory 5 times
            for (int i = 0; i < 5; i++)
            {
                graph.RecordUsage("cd", new[] { targetDir }, workingDir);
            }

            // Act
            var suggestions = predictor.GetSuggestions("cd ");

            // Assert - description should show visit count
            var targetDirWithSeparator = targetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var suggestion = suggestions.FirstOrDefault(s => s.Text.Equals(targetDirWithSeparator, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(suggestion);
            Assert.Contains("visited 5x", suggestion.Description, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(targetDir); } catch { }
        }
    }

    [Fact]
    public void GetSuggestions_PartialSubcommand_FiltersToMatchingOnly()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        // Learn several git commands
        graph.RecordUsage("git", new[] { "checkout" }, null);
        graph.RecordUsage("git", new[] { "cherry-pick" }, null);
        graph.RecordUsage("git", new[] { "commit" }, null);
        graph.RecordUsage("git", new[] { "status" }, null);
        graph.RecordUsage("git", new[] { "pull" }, null);

        // Act - user types "git che" (partial word)
        var suggestions = predictor.GetSuggestions("git che");

        // Assert - should only show subcommands starting with "che"
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Text == "checkout");
        Assert.Contains(suggestions, s => s.Text == "cherry-pick");
        // Should NOT suggest unrelated commands
        Assert.DoesNotContain(suggestions, s => s.Text == "commit");
        Assert.DoesNotContain(suggestions, s => s.Text == "status");
        Assert.DoesNotContain(suggestions, s => s.Text == "pull");
    }

    [Fact]
    public void GetSuggestions_PartialSubcommand_DoesNotAppendExtraArguments()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        // Learn git checkout with additional arguments
        graph.RecordUsage("git", new[] { "checkout", "main" }, null);
        graph.RecordUsage("git", new[] { "checkout", "-b", "feature" }, null);
        // Learn other git commands
        graph.RecordUsage("git", new[] { "pull" }, null);
        graph.RecordUsage("git", new[] { "status" }, null);

        // Act - user types "git che" (partial word)
        var suggestions = predictor.GetSuggestions("git che");

        // Assert - should only suggest subcommands starting with "che", not additional arguments
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s =>
        {
            // All suggestions should start with "che"
            Assert.StartsWith("che", s.Text, StringComparison.OrdinalIgnoreCase);
        });

        // Should NOT contain "pull", "status", "main", "-b", "feature"
        Assert.DoesNotContain(suggestions, s => s.Text == "pull");
        Assert.DoesNotContain(suggestions, s => s.Text == "status");
        Assert.DoesNotContain(suggestions, s => s.Text == "main");
        Assert.DoesNotContain(suggestions, s => s.Text == "-b");
        Assert.DoesNotContain(suggestions, s => s.Text == "feature");
    }

    [Fact]
    public void GetSuggestions_CompleteSubcommandWithSpace_ShowsArguments()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        graph.RecordUsage("git", new[] { "checkout", "main" }, null);
        graph.RecordUsage("git", new[] { "checkout", "-b" }, null);

        // Act - user types "git checkout " (complete word with trailing space)
        var suggestions = predictor.GetSuggestions("git checkout ");

        // Assert - should suggest arguments for checkout
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Text == "main");
        Assert.Contains(suggestions, s => s.Text == "-b");
    }

    [Fact]
    public void GetSuggestions_PartialFlag_FiltersToMatchingFlags()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        graph.RecordUsage("git", new[] { "commit", "-m", "message" }, null);
        graph.RecordUsage("git", new[] { "commit", "-a" }, null);
        graph.RecordUsage("git", new[] { "commit", "--amend" }, null);

        // Act - user types "git commit -" (partial flag)
        var suggestions = predictor.GetSuggestions("git commit -");

        // Assert - should only show flags starting with "-"
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s =>
        {
            Assert.StartsWith("-", s.Text);
        });
        // Should not contain "message" (it's not a flag)
        Assert.DoesNotContain(suggestions, s => s.Text == "message");
    }

    [Fact]
    public void GetSuggestions_PartialArgument_FiltersToMatchingArguments()
    {
        // Arrange
        var predictor = CreatePredictor(out _, out var graph);
        graph.RecordUsage("git", new[] { "checkout", "main" }, null);
        graph.RecordUsage("git", new[] { "checkout", "master" }, null);
        graph.RecordUsage("git", new[] { "checkout", "feature" }, null);

        // Act - user types "git checkout ma" (partial branch name)
        var suggestions = predictor.GetSuggestions("git checkout ma");

        // Assert - should only show branches starting with "ma"
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Text == "main");
        Assert.Contains(suggestions, s => s.Text == "master");
        Assert.DoesNotContain(suggestions, s => s.Text == "feature");
    }

    [Fact]
    public void GetSuggestions_MLPredictions_FilteredByPartialSubcommand()
    {
        // Arrange
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var analyzer = new ContextAnalyzer();
        var sequencePredictor = new SequencePredictor(ngramOrder: 2);
        var predictor = new GenericPredictor(history, graph, analyzer, sequencePredictor);

        // Record sequence: git checkout -> git commit
        sequencePredictor.RecordSequence(new[] { "git checkout", "git commit" });
        sequencePredictor.RecordSequence(new[] { "git checkout", "git commit" });
        sequencePredictor.RecordSequence(new[] { "git checkout", "git commit" });

        graph.RecordUsage("git", new[] { "checkout" }, null);
        graph.RecordUsage("git", new[] { "commit" }, null);
        graph.RecordUsage("git", new[] { "cherry-pick" }, null);

        // Act - user types "git che" after checkout (partial word)
        history.Add("git", "git checkout main", new[] { "checkout", "main" }, success: true);
        var suggestions = predictor.GetSuggestions("git che");

        // Assert - ML predictions should also be filtered by "che"
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s =>
        {
            Assert.StartsWith("che", s.Text, StringComparison.OrdinalIgnoreCase);
        });
        // Should NOT suggest "commit" even though ML would predict it
        Assert.DoesNotContain(suggestions, s => s.Text == "commit");
    }
}
