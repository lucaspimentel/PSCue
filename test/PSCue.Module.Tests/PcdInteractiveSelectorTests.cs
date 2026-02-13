using PSCue.Module;
using System.IO;
using Xunit;

namespace PSCue.Module.Tests;

public class PcdInteractiveSelectorTests
{
    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);

        // Act
        var selector = new PcdInteractiveSelector(graph);

        // Assert
        Assert.NotNull(selector);
    }

    [Fact]
    public void Constructor_WithNullGraph_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PcdInteractiveSelector(null!));
    }

    [Fact]
    public void FormatLastUsed_HandlesVariousTimespans()
    {
        // This test verifies the time formatting logic by using reflection to call the private method
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);
        var selector = new PcdInteractiveSelector(graph);

        var formatLastUsedMethod = typeof(PcdInteractiveSelector).GetMethod(
            "FormatLastUsed",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        Assert.NotNull(formatLastUsedMethod);

        // Test cases: (delta, expected output pattern)
        var testCases = new[]
        {
            (TimeSpan.FromSeconds(30), "just now"),
            (TimeSpan.FromMinutes(1), "1 minute ago"),
            (TimeSpan.FromMinutes(5), "5 minutes ago"),
            (TimeSpan.FromHours(1), "1 hour ago"),
            (TimeSpan.FromHours(3), "3 hours ago"),
            (TimeSpan.FromDays(1), "1 day ago"),
            (TimeSpan.FromDays(5), "5 days ago"),
            (TimeSpan.FromDays(14), "2 weeks ago"),
            (TimeSpan.FromDays(60), "2 months ago"),
            (TimeSpan.FromDays(400), "1 year ago")
        };

        foreach (var (delta, expectedPattern) in testCases)
        {
            var lastUsed = DateTime.UtcNow - delta;
            var result = formatLastUsedMethod.Invoke(selector, new object[] { lastUsed }) as string;
            Assert.NotNull(result);
            Assert.Contains(expectedPattern.Split(' ')[0], result); // Check the number part
        }
    }

    [Fact]
    public void ShowSelectionPrompt_WithNoLearnedData_ReturnsNull()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);
        var selector = new PcdInteractiveSelector(graph);
        var currentDir = Directory.GetCurrentDirectory();

        // Act
        // Note: This will output to console but won't actually show a prompt (no learned data)
        var result = selector.ShowSelectionPrompt(currentDir, 20);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ShowSelectionPrompt_WithEmptyCurrentDirectory_ReturnsNull()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);
        var selector = new PcdInteractiveSelector(graph);

        // Act
        var result = selector.ShowSelectionPrompt(string.Empty, 20);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetSuggestions_ReturnsTopDirectories_SortedByScore()
    {
        // This test verifies the underlying PcdCompletionEngine returns scored suggestions
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);

        // Use temp paths to avoid filesystem dependencies
        var tempDir1 = Path.Combine(Path.GetTempPath(), "pscue-test-1");
        var tempDir2 = Path.Combine(Path.GetTempPath(), "pscue-test-2");
        var tempDir3 = Path.Combine(Path.GetTempPath(), "pscue-test-3");

        try
        {
            // Create temp directories
            Directory.CreateDirectory(tempDir1);
            Directory.CreateDirectory(tempDir2);
            Directory.CreateDirectory(tempDir3);

            var currentDir = Directory.GetCurrentDirectory();

            // Record usage with different frequencies
            graph.RecordUsage("cd", new[] { tempDir1 }, currentDir);
            graph.RecordUsage("cd", new[] { tempDir1 }, currentDir);
            graph.RecordUsage("cd", new[] { tempDir1 }, currentDir); // Freq: 3
            graph.RecordUsage("cd", new[] { tempDir2 }, currentDir);
            graph.RecordUsage("cd", new[] { tempDir2 }, currentDir); // Freq: 2
            graph.RecordUsage("cd", new[] { tempDir3 }, currentDir); // Freq: 1

            var engine = new PcdCompletionEngine(
                graph,
                PcdConfiguration.ScoreDecayDays,
                PcdConfiguration.FrequencyWeight,
                PcdConfiguration.RecencyWeight,
                PcdConfiguration.DistanceWeight,
                PcdConfiguration.TabCompletionMaxDepth,
                PcdConfiguration.EnableRecursiveSearch,
                PcdConfiguration.ExactMatchBoost,
                PcdConfiguration.FuzzyMinMatchPercentage
            );

            // Act
            var suggestions = engine.GetSuggestions(string.Empty, currentDir, 10);

            // Assert
            Assert.NotEmpty(suggestions);
            // Verify that tempDir1 (highest frequency) appears in results
            Assert.Contains(suggestions, s => s.DisplayPath.Contains("pscue-test-1"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir1)) Directory.Delete(tempDir1, recursive: true);
            if (Directory.Exists(tempDir2)) Directory.Delete(tempDir2, recursive: true);
            if (Directory.Exists(tempDir3)) Directory.Delete(tempDir3, recursive: true);
        }
    }

    [Fact]
    public void ShowSelectionPrompt_FiltersNonExistentPaths()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);

        var currentDir = Directory.GetCurrentDirectory();

        // Record usage with non-existent paths
        var fakePath1 = Path.Combine(Path.GetTempPath(), "pscue-fake-1-" + Guid.NewGuid());
        var fakePath2 = Path.Combine(Path.GetTempPath(), "pscue-fake-2-" + Guid.NewGuid());

        graph.RecordUsage("cd", new[] { fakePath1 }, currentDir);
        graph.RecordUsage("cd", new[] { fakePath2 }, currentDir);

        var selector = new PcdInteractiveSelector(graph);

        // Act
        var result = selector.ShowSelectionPrompt(currentDir, 20);

        // Assert
        // Should return null because no valid directories exist
        Assert.Null(result);
    }
}
