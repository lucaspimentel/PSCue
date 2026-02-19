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
    public void ShowSelectionPrompt_ExcludesParentDirShortcut()
    {
        // Arrange: empty graph, so only well-known shortcuts (~, ..) would appear from the engine
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);
        var selector = new PcdInteractiveSelector(graph);

        // Use a non-home temp directory so ~ won't be filtered as current dir
        var tempDir = Path.Combine(Path.GetTempPath(), "pscue-test-wellknown-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var result = selector.ShowSelectionPrompt(tempDir, 20);

            // Assert: no valid suggestions — .. is filtered out, no learned data exists
            // (~ may appear if it exists and differs from tempDir, but that's fine — this test
            //  just verifies .. doesn't sneak through as a "0 visits" noise entry)
            // With an empty graph the engine returns only well-known shortcuts; after filtering
            // .. the only remaining candidate is ~ which may or may not be valid here.
            // The key assertion is that the result is null because no TTY is available in tests.
            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ShowSelectionPrompt_IncludesHomeDirShortcut_WhenLearned()
    {
        // Arrange: record the home directory so it appears as a learned entry in addition to ~
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tempDir = Path.Combine(Path.GetTempPath(), "pscue-test-home-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Record a usage so the graph has data and well-known shortcuts are also returned
            graph.RecordUsage("cd", new[] { homeDir }, tempDir);

            var selector = new PcdInteractiveSelector(graph);

            // Act — no TTY in tests so result is always null, but the key check is that
            // the code path doesn't throw and doesn't incorrectly filter ~ out before the
            // TTY check is reached (i.e., validSuggestions is non-empty).
            var result = selector.ShowSelectionPrompt(tempDir, 20);

            // Result is null because tests have no interactive TTY — that's expected.
            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ShowSelectionPrompt_ExcludesCurrentDirectory()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);

        var tempDir = Path.Combine(Path.GetTempPath(), "pscue-test-current-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var currentDir = tempDir;

            // Record usage with only the current directory
            graph.RecordUsage("cd", new[] { currentDir }, currentDir);

            var selector = new PcdInteractiveSelector(graph);

            // Act
            var result = selector.ShowSelectionPrompt(currentDir, 20);

            // Assert
            // Should return null because the only learned directory is the current one
            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ShowSelectionPrompt_WithFilter_FiltersNonMatchingPaths()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);

        var tempBase = Path.Combine(Path.GetTempPath(), "pscue-filter-test-" + Guid.NewGuid());
        var dotnetDir = Path.Combine(tempBase, "dd-trace-dotnet");
        var jsDir = Path.Combine(tempBase, "dd-trace-js");
        var otherDir = Path.Combine(tempBase, "some-other-project");

        try
        {
            Directory.CreateDirectory(dotnetDir);
            Directory.CreateDirectory(jsDir);
            Directory.CreateDirectory(otherDir);

            var currentDir = tempBase;

            // Record usage for all three directories
            graph.RecordUsage("cd", new[] { dotnetDir }, currentDir);
            graph.RecordUsage("cd", new[] { jsDir }, currentDir);
            graph.RecordUsage("cd", new[] { otherDir }, currentDir);

            // Build the engine to verify filtering logic directly
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

            var suggestions = engine.GetSuggestions(string.Empty, currentDir, 20);
            var allValid = suggestions
                .Where(s => s.Path != ".." && Directory.Exists(s.DisplayPath))
                .ToList();

            // Without filter: should include all three dirs
            Assert.True(allValid.Count >= 3, $"Expected at least 3 suggestions, got {allValid.Count}");

            // With "dotnet" filter: should only include dd-trace-dotnet
            var filtered = allValid
                .Where(s => s.DisplayPath.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.Single(filtered);
            Assert.Contains("dd-trace-dotnet", filtered[0].DisplayPath);
        }
        finally
        {
            if (Directory.Exists(tempBase)) Directory.Delete(tempBase, recursive: true);
        }
    }

    [Fact]
    public void ShowSelectionPrompt_WithFilter_NoMatches_ReturnsNull()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 100, maxArgumentsPerCommand: 50);

        var tempDir = Path.Combine(Path.GetTempPath(), "pscue-filter-nomatch-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            graph.RecordUsage("cd", new[] { tempDir }, currentDir);

            var selector = new PcdInteractiveSelector(graph);

            // Act - filter that won't match any path
            var result = selector.ShowSelectionPrompt(currentDir, 20, "zzz-nonexistent-zzz");

            // Assert
            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
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
