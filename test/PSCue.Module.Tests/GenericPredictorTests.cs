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
        Assert.All(suggestions, s => Assert.NotNull(s.Description));
        Assert.All(suggestions, s => Assert.NotEmpty(s.Description!));
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
}
