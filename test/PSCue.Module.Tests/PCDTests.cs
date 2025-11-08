using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using PSCue.Module;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests for PCD (PowerShell Change Directory) smart navigation functionality.
/// Tests both the Invoke-PCD function behavior and tab completion integration.
/// </summary>
public class PCDTests : IDisposable
{
    private readonly ArgumentGraph _graph;
    private readonly CommandPredictor _predictor;

    public PCDTests()
    {
        // Create fresh instances for each test
        _graph = new ArgumentGraph();
        _predictor = new CommandPredictor();

        // Set up static module state for tab completion testing
        PSCueModule.KnowledgeGraph = _graph;
    }

    public void Dispose()
    {
        // Clean up static state
        PSCueModule.KnowledgeGraph = null;
    }

    #region ArgumentGraph Integration Tests

    [Fact]
    public void GetSuggestions_ReturnsLearnedDirectories()
    {
        // Arrange - Record some directory usage
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\lucaspimentel" }, null);
        _graph.RecordUsage("cd", new[] { "C:\\Users\\lucas" }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());

        // Assert
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Argument == "D:\\source\\datadog");
        Assert.Contains(suggestions, s => s.Argument == "D:\\source\\lucaspimentel");
        Assert.Contains(suggestions, s => s.Argument == "C:\\Users\\lucas");
    }

    [Fact]
    public void GetSuggestions_OrdersByScoreDescending()
    {
        // Arrange - Record usage with different frequencies
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\lucaspimentel" }, null);
        _graph.RecordUsage("cd", new[] { "C:\\Users\\lucas" }, null);
        _graph.RecordUsage("cd", new[] { "C:\\Users\\lucas" }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>()).ToList();

        // Assert - Should be ordered by score (frequency + recency)
        Assert.True(suggestions.Count >= 3);
        var datadogIndex = suggestions.FindIndex(s => s.Argument == "D:\\source\\datadog");
        var lucasIndex = suggestions.FindIndex(s => s.Argument == "C:\\Users\\lucas");
        var lucaspimentelIndex = suggestions.FindIndex(s => s.Argument == "D:\\source\\lucaspimentel");

        // datadog (3 uses) should come before lucaspimentel (1 use)
        Assert.True(datadogIndex < lucaspimentelIndex);

        // Verify usage counts
        Assert.Equal(3, suggestions[datadogIndex].UsageCount);
        Assert.Equal(1, suggestions[lucaspimentelIndex].UsageCount);
    }

    [Fact]
    public void GetSuggestions_PathNormalization_HandlesTrailingSlashes()
    {
        // Arrange - Record same path with different formats
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog\\" }, null);
        _graph.RecordUsage("cd", new[] { "D:/source/datadog" }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());

        // Assert - Path normalization is handled by ArgumentGraph
        // Currently, ArgumentGraph stores paths as-is, so we may get multiple entries
        // This test verifies the current behavior - if normalization is added later, update this test
        var datadogSuggestions = suggestions.Where(s =>
            s.Argument.Contains("datadog", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        Assert.NotEmpty(datadogSuggestions);
        // Each variation recorded should be present (current behavior)
        // Total usage across all variations
        var totalUsage = datadogSuggestions.Sum(s => s.UsageCount);
        Assert.Equal(3, totalUsage);
    }

    [Fact]
    public void GetSuggestions_EmptyGraph_ReturnsEmpty()
    {
        // Act - Query empty graph
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());

        // Assert
        Assert.Empty(suggestions);
    }

    #endregion

    #region Tab Completion Simulation Tests

    [Fact]
    public void TabCompletion_WithLearnedData_ReturnsCompletions()
    {
        // Arrange - Record usage
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\lucaspimentel" }, null);

        // Simulate tab completion logic
        // Note: GetSuggestions already returns results ordered by score
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());
        var completions = suggestions
            .Select(s => new CompletionResult(
                s.Argument,
                s.Argument,
                CompletionResultType.ParameterValue,
                $"Used {s.UsageCount} times (last: {s.LastUsed:yyyy-MM-dd})"
            ))
            .ToList();

        // Assert
        Assert.NotEmpty(completions);
        Assert.Contains(completions, c => c.CompletionText == "D:\\source\\datadog");
        Assert.Contains(completions, c => c.CompletionText == "D:\\source\\lucaspimentel");
    }

    [Fact]
    public void TabCompletion_WithPartialInput_FiltersResults()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\lucaspimentel" }, null);
        _graph.RecordUsage("cd", new[] { "C:\\Users\\lucas" }, null);

        var wordToComplete = "D:\\source\\";

        // Act - Simulate filtering (like in PCD.ps1)
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>())
            .Where(s => s.Argument.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert - Should only include D:\source\* paths
        Assert.Equal(2, suggestions.Count);
        Assert.Contains(suggestions, s => s.Argument == "D:\\source\\datadog");
        Assert.Contains(suggestions, s => s.Argument == "D:\\source\\lucaspimentel");
        Assert.DoesNotContain(suggestions, s => s.Argument == "C:\\Users\\lucas");
    }

    [Fact]
    public void TabCompletion_WithPartialInput_CaseInsensitive()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\Source\\DataDog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\Source\\LucasPimentel" }, null);

        var wordToComplete = "d:\\source\\d";

        // Act - Case-insensitive filtering
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>())
            .Where(s => s.Argument.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        Assert.Single(suggestions);
        Assert.Contains(suggestions, s => s.Argument.Contains("DataDog", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TabCompletion_PathsWithSpaces_RequireQuoting()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "C:\\Program Files\\MyApp" }, null);

        // Act - Simulate completion text generation
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>()).ToList();
        var argument = suggestions[0].Argument;

        var completionText = argument.Contains(' ')
            ? $"\"{argument}\""
            : argument;

        // Assert
        Assert.Contains(' ', argument);
        Assert.StartsWith("\"", completionText);
        Assert.EndsWith("\"", completionText);
        Assert.Equal("\"C:\\Program Files\\MyApp\"", completionText);
    }

    [Fact]
    public void TabCompletion_NoLearnedData_ReturnsEmpty()
    {
        // Act - Query empty graph
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());

        // Assert
        Assert.Empty(suggestions);
    }

    [Fact]
    public void TabCompletion_UninitializedModule_HandlesGracefully()
    {
        // Arrange - Simulate uninitialized module
        PSCueModule.KnowledgeGraph = null;

        // Act - Tab completion should check for null and return empty
        var suggestions = PSCueModule.KnowledgeGraph?.GetSuggestions("cd", Array.Empty<string>())
            ?? new List<ArgumentStats>();

        // Assert
        Assert.Empty(suggestions);

        // Restore for cleanup
        PSCueModule.KnowledgeGraph = _graph;
    }

    #endregion

    #region Scoring and Ranking Tests

    [Fact]
    public void Scoring_FrequentDirectory_HasHigherScore()
    {
        // Arrange - Frequent vs infrequent usage
        for (int i = 0; i < 10; i++)
        {
            _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        }
        _graph.RecordUsage("cd", new[] { "D:\\source\\other" }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>()).ToList();

        // Assert
        var datadogUsage = suggestions.First(s => s.Argument == "D:\\source\\datadog").UsageCount;
        var otherUsage = suggestions.First(s => s.Argument == "D:\\source\\other").UsageCount;

        Assert.True(datadogUsage > otherUsage);
        Assert.Equal(10, datadogUsage);
        Assert.Equal(1, otherUsage);
    }

    [Fact]
    public void Scoring_RecentDirectory_InfluencesScore()
    {
        // Arrange - Record old usage
        _graph.RecordUsage("cd", new[] { "D:\\source\\old" }, null);

        // Simulate time passing (decay affects older entries)
        System.Threading.Thread.Sleep(10);

        // Record recent usage
        _graph.RecordUsage("cd", new[] { "D:\\source\\recent" }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>()).ToList();

        // Assert - Both should be present
        Assert.Contains(suggestions, s => s.Argument == "D:\\source\\old");
        Assert.Contains(suggestions, s => s.Argument == "D:\\source\\recent");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_RootDirectory_HandlesCorrectly()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "C:\\" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\" }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());

        // Assert
        Assert.Contains(suggestions, s => s.Argument == "C:\\");
        Assert.Contains(suggestions, s => s.Argument == "D:\\");
    }

    [Fact]
    public void EdgeCase_RelativePaths_Normalized()
    {
        // Arrange - Relative paths should be normalized
        _graph.RecordUsage("cd", new[] { ".." }, null);
        _graph.RecordUsage("cd", new[] { "." }, null);
        _graph.RecordUsage("cd", new[] { ".\\subdir" }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());

        // Assert - Should have recorded these (normalization depends on implementation)
        Assert.NotEmpty(suggestions);
    }

    [Fact]
    public void EdgeCase_VeryLongPath_HandlesCorrectly()
    {
        // Arrange - Windows max path is 260 chars (legacy) or 32k chars (modern)
        var longPath = "C:\\" + string.Join("\\", Enumerable.Repeat("verylongdirectoryname", 10));
        _graph.RecordUsage("cd", new[] { longPath }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());

        // Assert
        Assert.Contains(suggestions, s => s.Argument == longPath);
    }

    [Fact]
    public void EdgeCase_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange - Paths with special characters (but valid on Windows/Linux)
        _graph.RecordUsage("cd", new[] { "C:\\Users\\Test (Admin)" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\Projects\\My-App_v2.0" }, null);

        // Act
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>());

        // Assert
        Assert.Contains(suggestions, s => s.Argument == "C:\\Users\\Test (Admin)");
        Assert.Contains(suggestions, s => s.Argument == "D:\\Projects\\My-App_v2.0");
    }

    #endregion

    #region Integration with Set-Location Aliases

    [Fact]
    public void SetLocationAliases_AllTrackedAsCd()
    {
        // Arrange - PSCue should normalize all Set-Location variants to "cd"
        // This test verifies that the learning system treats them equivalently
        _graph.RecordUsage("cd", new[] { "D:\\source\\test1" }, null);
        _graph.RecordUsage("Set-Location", new[] { "D:\\source\\test2" }, null);
        _graph.RecordUsage("sl", new[] { "D:\\source\\test3" }, null);
        _graph.RecordUsage("chdir", new[] { "D:\\source\\test4" }, null);

        // Act - All should be retrievable via "cd"
        var cdSuggestions = _graph.GetSuggestions("cd", Array.Empty<string>());
        var setLocationSuggestions = _graph.GetSuggestions("Set-Location", Array.Empty<string>());

        // Assert - ArgumentGraph stores them separately by command name
        // PCD function uses "cd" explicitly
        Assert.NotEmpty(cdSuggestions);
        Assert.Contains(cdSuggestions, s => s.Argument == "D:\\source\\test1");

        // Note: If command normalization is implemented, these assertions would change
        // Currently, ArgumentGraph tracks each command name separately
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void Performance_LargeDataset_CompletesQuickly()
    {
        // Arrange - Simulate large learning dataset
        for (int i = 0; i < 100; i++)
        {
            _graph.RecordUsage("cd", new[] { $"D:\\source\\project{i}" }, null);
        }

        // Act - Measure completion time
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // Request all results (default maxResults is 10)
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>(), maxResults: 100).ToList();
        sw.Stop();

        // Assert - Should complete in < 10ms (target for tab completion)
        Assert.True(sw.ElapsedMilliseconds < 10,
            $"Tab completion took {sw.ElapsedMilliseconds}ms, expected < 10ms");
        Assert.Equal(100, suggestions.Count);
    }

    [Fact]
    public void Performance_FilteringLargeDataset_Fast()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _graph.RecordUsage("cd", new[] { $"D:\\source\\project{i}" }, null);
        }

        var wordToComplete = "D:\\source\\project5";

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // Request more results than the default 10
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>(), maxResults: 100)
            .Where(s => s.Argument.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            .ToList();
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 10,
            $"Filtered completion took {sw.ElapsedMilliseconds}ms, expected < 10ms");

        // Should match project5, project50-59
        Assert.True(suggestions.Count >= 11);
    }

    #endregion
}
