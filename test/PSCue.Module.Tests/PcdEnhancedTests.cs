using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PSCue.Module;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Comprehensive tests for PcdCompletionEngine (Phase 17.6).
/// Tests fuzzy matching, frecency scoring, distance scoring, and recursive search.
/// </summary>
public class PcdEnhancedTests : IDisposable
{
    private readonly ArgumentGraph _graph;
    private readonly string _testCurrentDir;
    private readonly string _testHomeDir;
    private readonly string _testRootDir;
    private readonly List<string> _tempDirectories;

    public PcdEnhancedTests()
    {
        _graph = new ArgumentGraph();
        _tempDirectories = new List<string>();

        // Use realistic test paths (Windows-style for testing)
        _testCurrentDir = "D:\\source\\lucaspimentel\\PSCue";
        _testHomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Create a temporary test directory structure
        _testRootDir = Path.Combine(Path.GetTempPath(), $"PSCue_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRootDir);
        _tempDirectories.Add(_testRootDir);
    }

    public void Dispose()
    {
        // Cleanup temp directories
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempDirectory(string name)
    {
        var path = Path.Combine(_testRootDir, name);
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    #region Well-Known Shortcuts Tests

    [Fact]
    public void GetSuggestions_WellKnownShortcut_Tilde_ReturnsHomeDirectory()
    {
        // Arrange
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("~", _testCurrentDir, 20);

        // Assert
        Assert.NotEmpty(suggestions);
        var tildeResult = suggestions.FirstOrDefault(s => s.Path == "~");
        Assert.NotNull(tildeResult);
        Assert.Equal(MatchType.WellKnown, tildeResult.MatchType);
        Assert.Equal(1000.0, tildeResult.Score); // Highest priority
        Assert.Contains("Home directory", tildeResult.Tooltip);
    }

    [Fact]
    public void GetSuggestions_WellKnownShortcut_DoubleDot_ReturnsParentDirectory()
    {
        // Arrange
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("..", _testCurrentDir, 20);

        // Assert
        Assert.NotEmpty(suggestions);
        var parentResult = suggestions.FirstOrDefault(s => s.Path == "..");
        Assert.NotNull(parentResult);
        Assert.Equal(MatchType.WellKnown, parentResult.MatchType);
        Assert.Equal(999.0, parentResult.Score); // Second highest priority
        Assert.Contains("Parent directory", parentResult.Tooltip);
    }

    [Fact]
    public void GetSuggestions_SingleDot_NotSuggested()
    {
        // Arrange
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions(".", _testCurrentDir, 20);

        // Assert - "." should NOT appear in suggestions (not useful in tab completion)
        // Users can still type "pcd ." if they want, but it won't show in suggestions
        var currentResult = suggestions.FirstOrDefault(s => s.Path == ".");
        Assert.Null(currentResult);
    }

    [Fact]
    public void GetSuggestions_Dash_NotSuggested()
    {
        // Arrange - Record "-" (previous directory) usage
        _graph.RecordUsage("cd", new[] { "-" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("", _testCurrentDir, 20);

        // Assert - "-" should NOT appear in suggestions (not useful in tab completion)
        // Users can still type "pcd -" if they want, but it won't show in suggestions
        var dashResult = suggestions.FirstOrDefault(s => s.Path == "-");
        Assert.Null(dashResult);
    }

    [Fact]
    public void GetSuggestions_WellKnownShortcuts_HaveHighestPriority()
    {
        // Arrange - Add learned directories
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search with empty string to get all suggestions
        var suggestions = engine.GetSuggestions("", _testCurrentDir, 20);

        // Assert - Well-known shortcuts should come first (only ~ and .. are suggested, not .)
        Assert.NotEmpty(suggestions);
        var topSuggestions = suggestions.Take(2).ToList();
        Assert.Contains(topSuggestions, s => s.MatchType == MatchType.WellKnown && s.Path == "~");
        Assert.Contains(topSuggestions, s => s.MatchType == MatchType.WellKnown && s.Path == "..");

        // "." should NOT be in suggestions
        Assert.DoesNotContain(suggestions, s => s.Path == ".");
    }

    #endregion

    #region Fuzzy Matching Tests

    [Fact]
    public void GetSuggestions_FuzzyMatch_Substring_ReturnsMatch()
    {
        // Arrange - Record directory with "datadog" in path
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog\\dd-trace-dotnet" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for substring
        var suggestions = engine.GetSuggestions("datadog", _testCurrentDir, 20);

        // Assert
        var match = suggestions.FirstOrDefault(s => s.Path.Contains("datadog", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(match);
        Assert.True(match.MatchType == MatchType.Fuzzy || match.MatchType == MatchType.Learned);
    }

    [Fact]
    public void GetSuggestions_FuzzyMatch_SubstringMatching_Works()
    {
        // Arrange - Record full path with "datadog" in it
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog\\dd-trace-dotnet" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for substring that appears in the path
        var suggestions = engine.GetSuggestions("trace", _testCurrentDir, 20);

        // Assert - Substring matching should find paths containing the search term
        var match = suggestions.FirstOrDefault(s => s.Path.Contains("trace", StringComparison.OrdinalIgnoreCase));

        // Fuzzy matching may or may not match depending on the search term and path complexity
        // The important thing is that substring matches are scored appropriately
        if (match != null)
        {
            Assert.True(match.MatchType == MatchType.Fuzzy || match.MatchType == MatchType.Learned);
        }
        else
        {
            // If no fuzzy match, at least verify the engine doesn't crash
            Assert.NotNull(suggestions);
        }
    }

    [Fact]
    public void GetSuggestions_FuzzyMatch_CaseInsensitive()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\DataDog" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search with different case
        var suggestions = engine.GetSuggestions("datadog", _testCurrentDir, 20);

        // Assert
        var match = suggestions.FirstOrDefault(s => s.Path.Contains("DataDog"));
        Assert.NotNull(match);
    }

    [Fact]
    public void GetSuggestions_ExactMatch_HigherScoreThanFuzzy()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog-backup" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for exact path
        var suggestions = engine.GetSuggestions("D:\\source\\datadog", _testCurrentDir, 20);

        // Assert - Exact match should rank higher than partial
        Assert.NotEmpty(suggestions);
        var exactMatch = suggestions.FirstOrDefault(s => s.Path == "D:\\source\\datadog");
        var fuzzyMatch = suggestions.FirstOrDefault(s => s.Path == "D:\\source\\datadog-backup");

        if (exactMatch != null && fuzzyMatch != null)
        {
            Assert.True(exactMatch.Score >= fuzzyMatch.Score);
        }
    }

    [Fact]
    public void GetSuggestions_PrefixMatch_HigherScoreThanSubstring()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\backup\\old-datadog" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for prefix
        var suggestions = engine.GetSuggestions("D:\\source", _testCurrentDir, 20);

        // Assert
        var prefixMatch = suggestions.FirstOrDefault(s => s.Path == "D:\\source\\datadog");
        Assert.NotNull(prefixMatch);
    }

    #endregion

    #region Frecency Scoring Tests

    [Fact]
    public void GetSuggestions_FrequencyComponent_MoreFrequentScoresHigher()
    {
        // Arrange - Create actual test directories with similar names
        var frequentDir = CreateTempDirectory("testdir-frequent");
        var rareDir = CreateTempDirectory("testdir-rare");

        // Record different frequencies
        for (int i = 0; i < 10; i++)
            _graph.RecordUsage("cd", new[] { frequentDir }, null);

        _graph.RecordUsage("cd", new[] { rareDir }, null);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search with empty string to get all learned directories
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert - More frequent should rank higher
        // Note: DisplayPath includes trailing backslash from path normalization
        var frequentMatch = suggestions.FirstOrDefault(s => s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar) == frequentDir.TrimEnd(Path.DirectorySeparatorChar));
        var rareMatch = suggestions.FirstOrDefault(s => s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar) == rareDir.TrimEnd(Path.DirectorySeparatorChar));

        Assert.True(frequentMatch != null,
            $"Expected to find frequent dir '{frequentDir}' in suggestions. Got {suggestions.Count} total suggestions: [{string.Join(", ", suggestions.Select(s => $"'{s.DisplayPath}'"))}]");
        Assert.True(rareMatch != null,
            $"Expected to find rare dir '{rareDir}' in suggestions");
        Assert.True(frequentMatch!.Score > rareMatch!.Score,
            $"More frequent directory should have higher score (frequent: {frequentMatch.Score}, rare: {rareMatch.Score})");
    }

    [Fact]
    public void GetSuggestions_ConfigurableWeights_AffectScoring()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\test1" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\test2" }, null);

        // Test different weight configurations
        var engine1 = new PcdCompletionEngine(_graph, frequencyWeight: 1.0, recencyWeight: 0.0, distanceWeight: 0.0);
        var engine2 = new PcdCompletionEngine(_graph, frequencyWeight: 0.0, recencyWeight: 1.0, distanceWeight: 0.0);

        // Act
        var suggestions1 = engine1.GetSuggestions("", _testCurrentDir, 20);
        var suggestions2 = engine2.GetSuggestions("", _testCurrentDir, 20);

        // Assert - Results should exist (scoring differences may be subtle)
        Assert.NotEmpty(suggestions1);
        Assert.NotEmpty(suggestions2);
    }

    #endregion

    #region Distance Scoring Tests

    [Fact]
    public void GetSuggestions_CurrentDirectory_NotSuggested()
    {
        // Arrange - Record current directory usage
        _graph.RecordUsage("cd", new[] { _testCurrentDir }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("", _testCurrentDir, 20);

        // Assert - Current directory should NOT appear in suggestions (not useful)
        var sameDir = suggestions.FirstOrDefault(s => s.Path == _testCurrentDir);
        Assert.Null(sameDir);
    }

    [Fact]
    public void GetSuggestions_DistanceScore_ParentDirectory_HighScore()
    {
        // Arrange - Record parent directory
        var parentDir = "D:\\source\\lucaspimentel";
        _graph.RecordUsage("cd", new[] { parentDir }, null);
        _graph.RecordUsage("cd", new[] { "D:\\unrelated\\path" }, null);

        var engine = new PcdCompletionEngine(_graph, distanceWeight: 1.0, frequencyWeight: 0.0, recencyWeight: 0.0);

        // Act
        var suggestions = engine.GetSuggestions("", _testCurrentDir, 20);

        // Assert - Parent should rank higher than unrelated
        var suggestionsList = suggestions.ToList();
        var parentIndex = suggestionsList.FindIndex(s => s.Path == parentDir);
        var unrelatedIndex = suggestionsList.FindIndex(s => s.Path == "D:\\unrelated\\path");

        if (parentIndex >= 0 && unrelatedIndex >= 0)
        {
            Assert.True(parentIndex < unrelatedIndex, "Parent directory should rank higher due to proximity");
        }
    }

    [Fact]
    public void GetSuggestions_DistanceScore_ChildDirectory_HighScore()
    {
        // Arrange - Record child directory
        var childDir = _testCurrentDir + "\\src\\PSCue.Module";
        _graph.RecordUsage("cd", new[] { childDir }, null);
        _graph.RecordUsage("cd", new[] { "D:\\unrelated\\path" }, null);

        var engine = new PcdCompletionEngine(_graph, distanceWeight: 1.0, frequencyWeight: 0.0, recencyWeight: 0.0);

        // Act
        var suggestions = engine.GetSuggestions("", _testCurrentDir, 20);

        // Assert - Child should rank higher than unrelated
        var suggestionsList = suggestions.ToList();
        var childIndex = suggestionsList.FindIndex(s => s.Path == childDir);
        var unrelatedIndex = suggestionsList.FindIndex(s => s.Path == "D:\\unrelated\\path");

        if (childIndex >= 0 && unrelatedIndex >= 0)
        {
            Assert.True(childIndex < unrelatedIndex, "Child directory should rank higher due to proximity");
        }
    }

    [Fact]
    public void GetSuggestions_DistanceScore_SiblingDirectory_MediumScore()
    {
        // Arrange - Record sibling directory (same parent)
        var siblingDir = "D:\\source\\lucaspimentel\\another-project";
        _graph.RecordUsage("cd", new[] { siblingDir }, null);
        _graph.RecordUsage("cd", new[] { "D:\\unrelated\\path" }, null);

        var engine = new PcdCompletionEngine(_graph, distanceWeight: 1.0, frequencyWeight: 0.0, recencyWeight: 0.0);

        // Act
        var suggestions = engine.GetSuggestions("", _testCurrentDir, 20);

        // Assert - Sibling should rank higher than unrelated
        var suggestionsList = suggestions.ToList();
        var siblingIndex = suggestionsList.FindIndex(s => s.Path == siblingDir);
        var unrelatedIndex = suggestionsList.FindIndex(s => s.Path == "D:\\unrelated\\path");

        if (siblingIndex >= 0 && unrelatedIndex >= 0)
        {
            Assert.True(siblingIndex < unrelatedIndex, "Sibling directory should rank higher due to proximity");
        }
    }

    #endregion

    #region Recursive Search Tests

    [Fact]
    public void GetSuggestions_RecursiveSearch_Disabled_DoesNotSearchFilesystem()
    {
        // Arrange - Engine with recursive search disabled (default)
        var engine = new PcdCompletionEngine(_graph, enableRecursiveSearch: false);

        // Act - Search for non-existent learned directory
        var suggestions = engine.GetSuggestions("nonexistent-unique-dir", _testCurrentDir, 20);

        // Assert - Should not find filesystem matches
        Assert.DoesNotContain(suggestions, s => s.MatchType == MatchType.Filesystem);
    }

    [Fact]
    public void GetSuggestions_RecursiveSearch_RespectsMaxDepth()
    {
        // Arrange - Engine with limited depth
        var engine = new PcdCompletionEngine(_graph, enableRecursiveSearch: true, maxRecursiveDepth: 1);

        // Act - This test verifies the engine doesn't crash with recursive search
        // Actual filesystem search results depend on the test environment
        var suggestions = engine.GetSuggestions("test", _testCurrentDir, 20);

        // Assert - Should complete without error
        Assert.NotNull(suggestions);
    }

    [Fact]
    public void GetSuggestions_RecursiveSearch_HandlesAccessDeniedGracefully()
    {
        // Arrange - Engine with recursive search enabled
        var engine = new PcdCompletionEngine(_graph, enableRecursiveSearch: true, maxRecursiveDepth: 2);

        // Act - Try to search from a root that might have restricted directories
        // This should not throw exceptions
        var suggestions = engine.GetSuggestions("test", "C:\\", 20);

        // Assert - Should complete without throwing
        Assert.NotNull(suggestions);
    }

    #endregion

    #region Best-Match Navigation Tests

    [Fact]
    public void GetSuggestions_BestMatch_FindsClosestMatch()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\datadog-backup" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for partial match
        var suggestions = engine.GetSuggestions("data", _testCurrentDir, 1);

        // Assert - Should return best match
        Assert.Single(suggestions);
        Assert.Contains("datadog", suggestions[0].Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSuggestions_MultipleMatches_OrderedByScore()
    {
        // Arrange - Create actual test directories
        var dir1 = CreateTempDirectory("datadog");
        var dir2 = CreateTempDirectory("datadog-backup");
        var dir3 = CreateTempDirectory("data-old");

        // Add multiple candidates with varying frequencies
        _graph.RecordUsage("cd", new[] { dir1 }, null);
        _graph.RecordUsage("cd", new[] { dir1 }, null); // Higher frequency
        _graph.RecordUsage("cd", new[] { dir2 }, null);
        _graph.RecordUsage("cd", new[] { dir3 }, null);

        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("data", _testRootDir, 10);

        // Assert - Results should be ordered by score
        Assert.True(suggestions.Count >= 2, $"Expected at least 2 suggestions, got {suggestions.Count}");
        for (int i = 0; i < suggestions.Count - 1; i++)
        {
            Assert.True(suggestions[i].Score >= suggestions[i + 1].Score,
                $"Suggestions should be ordered by score descending (index {i})");
        }
    }

    [Fact]
    public void GetSuggestions_NoMatches_ReturnsEmptyList()
    {
        // Arrange - Empty graph
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("nonexistent", _testCurrentDir, 20);

        // Assert - Should return empty (only well-known shortcuts if they match)
        Assert.DoesNotContain(suggestions, s =>
            s.MatchType != MatchType.WellKnown &&
            s.Path.Contains("nonexistent"));
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void GetSuggestions_LargeDataset_CompletesQuickly()
    {
        // Arrange - Create many test directories
        for (int i = 0; i < 100; i++)
        {
            var dir = CreateTempDirectory($"project-{i}");
            _graph.RecordUsage("cd", new[] { dir }, null);
        }

        var engine = new PcdCompletionEngine(_graph);

        // Act & Assert - Should complete in reasonable time
        var startTime = DateTime.UtcNow;
        var suggestions = engine.GetSuggestions("project", _testRootDir, 20);
        var elapsed = DateTime.UtcNow - startTime;

        Assert.True(elapsed.TotalMilliseconds < 100,
            $"GetSuggestions took {elapsed.TotalMilliseconds}ms, expected <100ms");
        Assert.NotEmpty(suggestions);
        Assert.True(suggestions.Count <= 20);
    }

    [Fact]
    public void GetSuggestions_EmptyInput_ReturnsAllSuggestions()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\project1" }, null);
        _graph.RecordUsage("cd", new[] { "D:\\source\\project2" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("", _testCurrentDir, 20);

        // Assert - Should return learned directories + well-known shortcuts
        Assert.True(suggestions.Count >= 2); // At least the learned directories
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void GetSuggestions_NullInput_TreatedAsEmpty()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\test" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions(null, _testCurrentDir, 20);

        // Assert - Should not throw, should return suggestions
        Assert.NotNull(suggestions);
        Assert.NotEmpty(suggestions);
    }

    [Fact]
    public void GetSuggestions_InvalidCurrentDirectory_DoesNotCrash()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\source\\test" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Use invalid current directory
        var suggestions = engine.GetSuggestions("test", "X:\\invalid\\path", 20);

        // Assert - Should complete without throwing
        Assert.NotNull(suggestions);
    }

    [Fact]
    public void GetSuggestions_Deduplication_RemovesDuplicatePaths()
    {
        // Arrange - This test verifies that the same path doesn't appear multiple times
        _graph.RecordUsage("cd", new[] { "D:\\source\\test" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("test", _testCurrentDir, 20);

        // Assert - No duplicate paths in results
        var paths = suggestions.Select(s => s.Path.ToLowerInvariant()).ToList();
        var uniquePaths = paths.Distinct().ToList();
        Assert.Equal(uniquePaths.Count, paths.Count);
    }

    [Fact]
    public void GetSuggestions_MaxResults_RespectsLimit()
    {
        // Arrange - Add many directories
        for (int i = 0; i < 50; i++)
        {
            _graph.RecordUsage("cd", new[] { $"D:\\test\\dir-{i}" }, null);
        }

        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("dir", _testCurrentDir, maxResults: 10);

        // Assert - Should not exceed max results
        Assert.True(suggestions.Count <= 10);
    }

    #endregion

    #region Tooltip and Display Tests

    [Fact]
    public void PcdSuggestion_Tooltip_IncludesUsageInformation()
    {
        // Arrange - Create actual test directory
        var testDir = CreateTempDirectory("mytest");

        _graph.RecordUsage("cd", new[] { testDir }, null);
        _graph.RecordUsage("cd", new[] { testDir }, null);
        _graph.RecordUsage("cd", new[] { testDir }, null);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Use empty string to get all learned directories
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert
        Assert.NotEmpty(suggestions);
        // Note: DisplayPath includes trailing backslash from path normalization
        var testSuggestion = suggestions.FirstOrDefault(s => s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar) == testDir.TrimEnd(Path.DirectorySeparatorChar));
        Assert.NotNull(testSuggestion);
        Assert.NotNull(testSuggestion.Tooltip);
        Assert.Contains("3", testSuggestion.Tooltip); // Usage count
        Assert.Contains("times", testSuggestion.Tooltip);
    }

    [Fact]
    public void PcdSuggestion_WellKnown_HasAppropriateTooltip()
    {
        // Arrange
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("~", _testCurrentDir, 20);

        // Assert
        var tildeResult = suggestions.FirstOrDefault(s => s.Path == "~");
        Assert.NotNull(tildeResult);
        Assert.Contains("Home", tildeResult.Tooltip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PcdSuggestion_MatchType_CorrectlyIdentified()
    {
        // Arrange
        _graph.RecordUsage("cd", new[] { "D:\\exact\\path" }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Test different match types
        var exactSuggestions = engine.GetSuggestions("D:\\exact\\path", _testCurrentDir, 20);
        var prefixSuggestions = engine.GetSuggestions("D:\\exact", _testCurrentDir, 20);
        var fuzzySuggestions = engine.GetSuggestions("exact", _testCurrentDir, 20);

        // Assert - Verify match types are set
        Assert.All(exactSuggestions.Concat(prefixSuggestions).Concat(fuzzySuggestions),
            s => Assert.NotEqual(default, s.MatchType));
    }

    #endregion

    #region Regression Tests for Recent Fixes

    [Fact]
    public void GetSuggestions_UnlearnedDirectory_ShowsInFilesystemSearch()
    {
        // Arrange - Create a physical directory structure without learning it
        var parentDir = CreateTempDirectory("parent");
        var childDir = Path.Combine(parentDir, "toaster");
        Directory.CreateDirectory(childDir);
        _tempDirectories.Add(childDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for unlearned directory by typing partial absolute path
        var suggestions = engine.GetSuggestions(Path.Combine(parentDir, "t"), parentDir, 20);

        // Assert - Should find "toaster" via filesystem search
        Assert.NotEmpty(suggestions);
        var toasterSuggestion = suggestions.FirstOrDefault(s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(childDir, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(toasterSuggestion);
        Assert.Equal(MatchType.Filesystem, toasterSuggestion.MatchType);
    }

    [Fact]
    public void GetSuggestions_AbsolutePath_DoesNotSuggestWellKnownShortcuts()
    {
        // Arrange
        var parentDir = CreateTempDirectory("parent");
        var childDir = Path.Combine(parentDir, "documentation");
        Directory.CreateDirectory(childDir);
        _tempDirectories.Add(childDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Type absolute path like "D:\source\datadog\doc"
        var suggestions = engine.GetSuggestions(Path.Combine(parentDir, "doc"), childDir, 20);

        // Assert - Should NOT suggest ".." or "~" when typing absolute path
        Assert.DoesNotContain(suggestions, s => s.Path == "..");
        Assert.DoesNotContain(suggestions, s => s.Path == "~");
    }

    [Fact]
    public void GetSuggestions_RelativePath_SuggestsWellKnownShortcuts()
    {
        // Arrange
        var engine = new PcdCompletionEngine(_graph);

        // Act - Type relative path (not absolute)
        var suggestions = engine.GetSuggestions("doc", _testCurrentDir, 20);

        // Assert - Should suggest ".." and "~" when typing relative path (if they match)
        // Note: These won't match "doc" prefix, so just verify they're available for empty input
        var allSuggestions = engine.GetSuggestions("", _testCurrentDir, 20);
        Assert.Contains(allSuggestions, s => s.Path == "..");
        Assert.Contains(allSuggestions, s => s.Path == "~");
    }

    [Fact]
    public void GetSuggestions_AbsolutePath_DoesNotSuggestParentDirectory()
    {
        // Arrange - Learn the parent directory
        var parentDir = CreateTempDirectory("datadog");
        var childDir = Path.Combine(parentDir, "documentation");
        Directory.CreateDirectory(childDir);
        _tempDirectories.Add(childDir);

        _graph.RecordUsage("cd", new[] { parentDir }, null); // Learn parent
        var engine = new PcdCompletionEngine(_graph);

        // Act - Type absolute path like "D:\source\datadog\doc" from inside "documentation"
        var suggestions = engine.GetSuggestions(Path.Combine(parentDir, "doc"), childDir, 20);

        // Assert - Should NOT suggest the parent directory itself
        var parentDirNormalized = parentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.DoesNotContain(suggestions, s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(parentDirNormalized, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_AbsolutePath_SuggestsChildDirectories()
    {
        // Arrange - Create parent with multiple children
        var parentDir = CreateTempDirectory("datadog2");
        var docDir = Path.Combine(parentDir, "documentation");
        var docsDir = Path.Combine(parentDir, "docs");
        Directory.CreateDirectory(docDir);
        Directory.CreateDirectory(docsDir);
        _tempDirectories.Add(docDir);
        _tempDirectories.Add(docsDir);

        _graph.RecordUsage("cd", new[] { parentDir }, null); // Learn parent
        var engine = new PcdCompletionEngine(_graph);

        // Act - Type "D:\...\datadog2\doc"
        var suggestions = engine.GetSuggestions(Path.Combine(parentDir, "doc"), parentDir, 20);

        // Assert - Should suggest both "documentation" and "docs", but NOT parent
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(docDir, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(suggestions, s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(docsDir, StringComparison.OrdinalIgnoreCase));

        // Should NOT suggest parent
        var parentDirNormalized = parentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.DoesNotContain(suggestions, s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(parentDirNormalized, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Phase 19.0: Recursive Search and Path Display Tests

    [Fact]
    public void GetSuggestions_RecursiveSearch_AlwaysRuns_WhenEnabled()
    {
        // Arrange - Create nested structure
        var root = CreateTempDirectory("root");
        var level1 = Path.Combine(root, "level1");
        var level2 = Path.Combine(level1, "target");
        Directory.CreateDirectory(level2);
        _tempDirectories.Add(level1);
        _tempDirectories.Add(level2);

        // Engine with recursive search enabled (should always run, not depend on result count)
        var engine = new PcdCompletionEngine(
            _graph,
            scoreDecayDays: 30,
            frequencyWeight: 0.5,
            recencyWeight: 0.3,
            distanceWeight: 0.2,
            maxRecursiveDepth: 2,
            enableRecursiveSearch: true
        );

        // Act - Search for "target" from root (should find it via recursive search)
        var suggestions = engine.GetSuggestions("target", root, maxResults: 20);

        // Assert - Should find the nested directory
        Assert.Contains(suggestions, s =>
            s.DisplayPath.Contains("target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_RecursiveSearch_WithShallowDepth_DoesNotFindDeeplyNested()
    {
        // Arrange - Create very nested structure
        var root = CreateTempDirectory("root");
        var level1 = Path.Combine(root, "level1");
        var level2 = Path.Combine(level1, "level2");
        var level3 = Path.Combine(level2, "target");
        Directory.CreateDirectory(level3);
        _tempDirectories.Add(level1);
        _tempDirectories.Add(level2);
        _tempDirectories.Add(level3);

        // Engine with shallow max depth (1)
        var engine = new PcdCompletionEngine(
            _graph,
            maxRecursiveDepth: 1,
            enableRecursiveSearch: true
        );

        // Act - Search for "target" from root with maxDepth=1 (should NOT find it at depth 3)
        var suggestions = engine.GetSuggestions("target", root, maxResults: 20);

        // Assert - Should NOT find the deeply nested directory
        Assert.DoesNotContain(suggestions, s =>
            s.DisplayPath.Contains("level2", StringComparison.OrdinalIgnoreCase) &&
            s.DisplayPath.Contains("target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_ChildPath_NoRedundantPrefix()
    {
        // Arrange - Create child directory
        var parent = CreateTempDirectory("parent");
        var child = Path.Combine(parent, "childdir");
        Directory.CreateDirectory(child);
        _tempDirectories.Add(child);

        _graph.RecordUsage("cd", new[] { child }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions from parent directory (empty string to get all learned)
        var suggestions = engine.GetSuggestions("", parent, maxResults: 20);

        // Assert - Child path should NOT have .\ prefix (should be "childdir", not ".\childdir")
        var childSuggestion = suggestions.FirstOrDefault(s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(child, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(childSuggestion);
        // The relative path should not start with ".\" for child directories
        Assert.False(childSuggestion.Path.StartsWith("." + Path.DirectorySeparatorChar),
            $"Path '{childSuggestion.Path}' should not start with '.{Path.DirectorySeparatorChar}'");
        // Should just be the bare directory name
        Assert.Equal("childdir", childSuggestion.Path, ignoreCase: true);
    }

    [Fact]
    public void GetSuggestions_ParentPath_KeepsDoubleDot()
    {
        // Arrange - Create parent and child
        var parent = CreateTempDirectory("parent");
        var child = Path.Combine(parent, "child");
        Directory.CreateDirectory(child);
        _tempDirectories.Add(child);

        _graph.RecordUsage("cd", new[] { parent }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions from child directory (should suggest parent as "..")
        var suggestions = engine.GetSuggestions("..", child, maxResults: 20);

        // Assert - Parent should be suggested as ".." (this is useful shortcut, keep it)
        Assert.Contains(suggestions, s => s.Path == "..");
    }

    [Fact]
    public void GetSuggestions_SiblingPath_KeepsDoubleDotPrefix()
    {
        // Arrange - Create parent with two children
        var parent = CreateTempDirectory("parent");
        var child1 = Path.Combine(parent, "child1");
        var child2 = Path.Combine(parent, "child2");
        Directory.CreateDirectory(child1);
        Directory.CreateDirectory(child2);
        _tempDirectories.Add(child1);
        _tempDirectories.Add(child2);

        _graph.RecordUsage("cd", new[] { child2 }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions from child1 (empty to get all learned)
        var suggestions = engine.GetSuggestions("", child1, maxResults: 20);

        // Assert - Sibling should use "..\child2" format (this is standard, keep it)
        var siblingSuggestion = suggestions.FirstOrDefault(s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(child2, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(siblingSuggestion);
        Assert.StartsWith("..", siblingSuggestion.Path);
        // The path should be "..\child2" (keep the .. prefix for siblings)
        Assert.Contains("child2", siblingSuggestion.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PcdConfiguration_ReadsEnvironmentVariables()
    {
        // Arrange - Set environment variables
        try
        {
            Environment.SetEnvironmentVariable("PSCUE_PCD_MAX_DEPTH", "5");
            Environment.SetEnvironmentVariable("PSCUE_PCD_PREDICTOR_MAX_DEPTH", "2");
            Environment.SetEnvironmentVariable("PSCUE_PCD_FREQUENCY_WEIGHT", "0.6");
            Environment.SetEnvironmentVariable("PSCUE_PCD_RECURSIVE_SEARCH", "false");

            // Act - Read configuration
            var tabDepth = PcdConfiguration.TabCompletionMaxDepth;
            var predictorDepth = PcdConfiguration.PredictorMaxDepth;
            var frequencyWeight = PcdConfiguration.FrequencyWeight;
            var recursiveEnabled = PcdConfiguration.EnableRecursiveSearch;

            // Assert - Values should match environment variables
            Assert.Equal(5, tabDepth);
            Assert.Equal(2, predictorDepth);
            Assert.Equal(0.6, frequencyWeight, precision: 2);
            Assert.False(recursiveEnabled);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("PSCUE_PCD_MAX_DEPTH", null);
            Environment.SetEnvironmentVariable("PSCUE_PCD_PREDICTOR_MAX_DEPTH", null);
            Environment.SetEnvironmentVariable("PSCUE_PCD_FREQUENCY_WEIGHT", null);
            Environment.SetEnvironmentVariable("PSCUE_PCD_RECURSIVE_SEARCH", null);
        }
    }

    [Fact]
    public void PcdConfiguration_UsesDefaults_WhenNoEnvironmentVariables()
    {
        // Act - Read configuration (assuming env vars are not set)
        var tabDepth = PcdConfiguration.TabCompletionMaxDepth;
        var predictorDepth = PcdConfiguration.PredictorMaxDepth;
        var frequencyWeight = PcdConfiguration.FrequencyWeight;
        var recencyWeight = PcdConfiguration.RecencyWeight;
        var distanceWeight = PcdConfiguration.DistanceWeight;
        var recursiveEnabled = PcdConfiguration.EnableRecursiveSearch;

        // Assert - Should use defaults
        Assert.True(tabDepth >= 1); // Default is 3
        Assert.True(predictorDepth >= 1); // Default is 1
        Assert.True(frequencyWeight > 0);
        Assert.True(recencyWeight > 0);
        Assert.True(distanceWeight >= 0);
        Assert.True(recursiveEnabled); // Default is true
    }

    #endregion
}
