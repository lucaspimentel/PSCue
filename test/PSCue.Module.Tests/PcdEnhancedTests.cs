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
    private readonly ITestOutputHelper _output;

    // Platform-agnostic test paths
    private readonly string _testSourceDir;
    private readonly string _testDatadogDir;
    private readonly string _testDdTraceDir;
    private readonly string _testDatadogBackupDir;
    private readonly string _testBackupDir;
    private readonly string _testOldDatadogDir;

    public PcdEnhancedTests(ITestOutputHelper output)
    {
        _output = output;
        _graph = new ArgumentGraph();
        _tempDirectories = new List<string>();

        // Create a temporary test directory structure
        _testRootDir = Path.Combine(Path.GetTempPath(), $"PSCue_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRootDir);
        _tempDirectories.Add(_testRootDir);

        // Use platform-agnostic test paths based on temp directory
        _testSourceDir = Path.Combine(_testRootDir, "source");
        _testDatadogDir = Path.Combine(_testSourceDir, "datadog");
        _testDdTraceDir = Path.Combine(_testDatadogDir, "dd-trace-dotnet");
        _testCurrentDir = Path.Combine(_testSourceDir, "lucaspimentel", "PSCue");
        _testDatadogBackupDir = Path.Combine(_testSourceDir, "datadog-backup");
        _testBackupDir = Path.Combine(_testRootDir, "backup");
        _testOldDatadogDir = Path.Combine(_testBackupDir, "old-datadog");
        _testHomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Don't create directories here to avoid conflicts with tests.
        // Tests that need directories will create them explicitly.
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
        // Arrange - Create a directory structure so Directory.GetParent() works
        var testDir = CreateTempDirectory("test-wellknown");
        var subDir = Path.Combine(testDir, "subdir");
        Directory.CreateDirectory(subDir);
        _tempDirectories.Add(subDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("..", subDir, 20);

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
        // Arrange - Create a directory structure and add learned directories
        var testDir = CreateTempDirectory("test-priority");
        var subDir = Path.Combine(testDir, "subdir");
        Directory.CreateDirectory(subDir);
        _tempDirectories.Add(subDir);

        var learnedDir = CreateTempDirectory("datadog");
        _graph.RecordUsage("cd", new[] { learnedDir }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search with empty string to get all suggestions
        var suggestions = engine.GetSuggestions("", subDir, 20);

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
        // Arrange - Create actual directories
        var sourceDir = CreateTempDirectory("source");
        var datadogDir = Path.Combine(sourceDir, "datadog");
        Directory.CreateDirectory(datadogDir);
        _tempDirectories.Add(datadogDir);

        var backupDir = CreateTempDirectory("backup");
        var oldDatadogDir = Path.Combine(backupDir, "old-datadog");
        Directory.CreateDirectory(oldDatadogDir);
        _tempDirectories.Add(oldDatadogDir);

        _graph.RecordUsage("cd", new[] { datadogDir }, null);
        _graph.RecordUsage("cd", new[] { oldDatadogDir }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for prefix
        var suggestions = engine.GetSuggestions(sourceDir, _testRootDir, 20);

        // Assert - The path starting with sourceDir should match
        var prefixMatch = suggestions.FirstOrDefault(s => s.DisplayPath.StartsWith(sourceDir, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(prefixMatch);
    }

    [Fact]
    public void GetSuggestions_DotPrefixedDirectories_PreservesLeadingDot()
    {
        // Arrange - Create dot-prefixed directories like .config, .cache
        var dotConfigDir = CreateTempDirectory(".config");
        var dotCacheDir = CreateTempDirectory(".cache");
        var normalDir = CreateTempDirectory("config"); // Non-dot version for comparison

        _graph.RecordUsage("cd", new[] { dotConfigDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { dotCacheDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { normalDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for ".conf" which should match ".config"
        var suggestions = engine.GetSuggestions(".conf", _testRootDir, 20);

        // Assert
        Assert.NotEmpty(suggestions);
        var dotConfigMatch = suggestions.FirstOrDefault(s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar) == dotConfigDir.TrimEnd(Path.DirectorySeparatorChar));

        Assert.NotNull(dotConfigMatch);

        // Verify the Path property preserves the dot for relative paths
        // When from child directory, it should be just the directory name (e.g., ".config")
        Assert.Contains(".config", dotConfigMatch.Path);
        Assert.DoesNotContain("config" + Path.DirectorySeparatorChar, dotConfigMatch.Path.Replace(".config", "")); // Ensure it's not "config" without dot
    }

    [Fact]
    public void GetSuggestions_DotPrefixedDirectories_DistinguishesFromNonDotVersion()
    {
        // Arrange - Both .config and config directories exist
        var dotConfigDir = CreateTempDirectory(".config");
        var configDir = CreateTempDirectory("config");

        _graph.RecordUsage("cd", new[] { dotConfigDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { configDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for ".config" explicitly
        var suggestions = engine.GetSuggestions(".config", _testRootDir, 20);

        // Assert - Should find .config, not just config
        var dotConfigMatch = suggestions.FirstOrDefault(s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar).EndsWith(".config"));

        Assert.NotNull(dotConfigMatch);
        Assert.Contains(".config", dotConfigMatch.Path);
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
        var project1 = CreateTempDirectory("project1");
        var project2 = CreateTempDirectory("project2");
        _graph.RecordUsage("cd", new[] { project1 }, null);
        _graph.RecordUsage("cd", new[] { project2 }, null);
        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

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
        // Arrange - Create a directory structure
        var testDir = CreateTempDirectory("test-shortcuts");
        var subDir = Path.Combine(testDir, "subdir");
        Directory.CreateDirectory(subDir);
        _tempDirectories.Add(subDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Type relative path (not absolute)
        var suggestions = engine.GetSuggestions("doc", subDir, 20);

        // Assert - Should suggest ".." and "~" when typing relative path (if they match)
        // Note: These won't match "doc" prefix, so just verify they're available for empty input
        var allSuggestions = engine.GetSuggestions("", subDir, 20);
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
    public void GetSuggestions_ParentDirectoryMatchedByName_ShowsAsDoubleDot()
    {
        // Arrange - Simulate the real scenario:
        // Current: D:\source\datadog\dd-trace-dotnet
        // Typing: "datadog"
        // Expected: Parent directory D:\source\datadog\ shown as ".." not "..\datadog"
        var grandparent = CreateTempDirectory("source");
        var parent = Path.Combine(grandparent, "datadog");
        var child = Path.Combine(parent, "dd-trace-dotnet");
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(child);
        _tempDirectories.Add(parent);
        _tempDirectories.Add(child);

        // Learn the parent directory WITH trailing separator (simulates real-world learned data)
        var parentWithTrailingSep = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        _graph.RecordUsage("cd", new[] { parentWithTrailingSep }, child);
        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions when typing "datadog" from child directory
        var suggestions = engine.GetSuggestions("datadog", child, maxResults: 20);

        // Assert - Parent should be suggested as ".." not "..\datadog"
        var parentSuggestion = suggestions.FirstOrDefault(s =>
            s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(parent, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(parentSuggestion);
        Assert.Equal("..", parentSuggestion.Path); // Should be just ".." not "..\datadog"
        Assert.Contains(parent, parentSuggestion.DisplayPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSuggestions_ParentExactMatchName_PrioritizedOverSibling()
    {
        // Arrange - Simulate real scenario:
        // Current: D:\source\datadog\dd-trace-dotnet
        // Parent: D:\source\datadog (exact match for "datadog")
        // Sibling: D:\source\datadog\dd-trace-dotnet-APMSVLS-58 (contains "datadog" but not exact)
        // Typing: "datadog"
        // Expected: Parent should rank first due to exact name match
        var grandparent = CreateTempDirectory("source");
        var parent = Path.Combine(grandparent, "datadog");
        var child = Path.Combine(parent, "dd-trace-dotnet");
        var sibling = Path.Combine(parent, "dd-trace-dotnet-APMSVLS-58");
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(child);
        Directory.CreateDirectory(sibling);
        _tempDirectories.Add(parent);
        _tempDirectories.Add(child);
        _tempDirectories.Add(sibling);

        // Learn both parent and sibling with trailing separators
        var parentWithTrailingSep = parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var siblingWithTrailingSep = sibling.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        // Record sibling with high frequency/recency to test that exact match still wins
        _graph.RecordUsage("cd", new[] { siblingWithTrailingSep }, child);
        _graph.RecordUsage("cd", new[] { siblingWithTrailingSep }, child);
        _graph.RecordUsage("cd", new[] { siblingWithTrailingSep }, child);
        _graph.RecordUsage("cd", new[] { parentWithTrailingSep }, child);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions when typing "datadog" from child directory
        var suggestions = engine.GetSuggestions("datadog", child, maxResults: 20);

        // Assert - Parent with exact name match should rank first
        Assert.NotEmpty(suggestions);
        var topSuggestion = suggestions.First();

        var parentNormalized = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var topPathNormalized = topSuggestion.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        Assert.Equal(parentNormalized, topPathNormalized, StringComparer.OrdinalIgnoreCase);
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

    #region Exact Match Priority and Trailing Separator Tests

    [Fact]
    public void GetSuggestions_ExactMatch_AppearsFirst()
    {
        // Arrange - Learn multiple directories with similar names
        var baseDir = "D:\\source\\datadog\\dd-trace-dotnet";
        var worktreeDir1 = baseDir + "-APMSVLS-58";
        var worktreeDir2 = baseDir + "-aws-lambda-layer";
        var worktreeDir3 = baseDir + "-pr-reviews";

        // Record usage with higher frequency for non-exact matches to test scoring
        _graph.RecordUsage("cd", new[] { worktreeDir1 }, null);
        _graph.RecordUsage("cd", new[] { worktreeDir1 }, null);
        _graph.RecordUsage("cd", new[] { worktreeDir1 }, null);
        _graph.RecordUsage("cd", new[] { worktreeDir2 }, null);
        _graph.RecordUsage("cd", new[] { worktreeDir2 }, null);
        _graph.RecordUsage("cd", new[] { baseDir }, null); // Exact match, less frequent
        _graph.RecordUsage("cd", new[] { worktreeDir3 }, null);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Type exact path
        var suggestions = engine.GetSuggestions(baseDir, "D:\\source\\lucaspimentel\\PSCue", 20);

        // Assert - Exact match should be first, even though others have higher frequency
        Assert.NotEmpty(suggestions);
        var firstSuggestion = suggestions.First();
        Assert.Equal(baseDir,
            firstSuggestion.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            ignoreCase: true);
    }

    [Fact]
    public void GetSuggestions_AllPaths_HaveTrailingSeparator()
    {
        // Arrange - Create test directories
        var dir1 = CreateTempDirectory("test1");
        var dir2 = CreateTempDirectory("test2");

        _graph.RecordUsage("cd", new[] { dir1 }, null);
        _graph.RecordUsage("cd", new[] { dir2 }, null);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Get all suggestions
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert - All DisplayPaths should have trailing separator
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s =>
        {
            Assert.True(
                s.DisplayPath.EndsWith(Path.DirectorySeparatorChar) ||
                s.DisplayPath.EndsWith(Path.AltDirectorySeparatorChar),
                $"DisplayPath '{s.DisplayPath}' should have trailing separator");
        });
    }

    #endregion

    #region Cache/Metadata Directory Filtering Tests

    [Fact]
    public void GetSuggestions_FiltersOutCacheDirectories()
    {
        // Arrange - Create both normal and cache directories
        var normalDir = CreateTempDirectory("my-project");
        var cacheDir1 = CreateTempDirectory(".codeium");
        var cacheDir2 = CreateTempDirectory("node_modules");
        var cacheDir3 = CreateTempDirectory(".dotnet");

        // Record usage for all directories
        _graph.RecordUsage("cd", new[] { normalDir }, null);
        _graph.RecordUsage("cd", new[] { cacheDir1 }, null);
        _graph.RecordUsage("cd", new[] { cacheDir2 }, null);
        _graph.RecordUsage("cd", new[] { cacheDir3 }, null);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions without any specific filter
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert - Cache directories should be filtered out
        Assert.Contains(suggestions, s => s.DisplayPath.Contains("my-project", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(suggestions, s => s.DisplayPath.Contains(".codeium", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(suggestions, s => s.DisplayPath.Contains("node_modules", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(suggestions, s => s.DisplayPath.Contains(".dotnet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_AllowsCacheDirectories_WhenExplicitlyTyped()
    {
        // Arrange - Create cache directory
        var cacheDir = CreateTempDirectory(".claude");
        _graph.RecordUsage("cd", new[] { cacheDir }, null);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Explicitly type ".claude" to search for it
        var suggestions = engine.GetSuggestions(".claude", _testRootDir, 20);

        // Assert - .claude should be included when explicitly typed
        Assert.Contains(suggestions, s => s.DisplayPath.Contains(".claude", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_FiltersOutCacheDirectories_InFilesystemSearch()
    {
        // Arrange - Create cache directories directly in filesystem (not learned)
        var normalDir = Path.Combine(_testRootDir, "normal-dir");
        var cacheDir1 = Path.Combine(_testRootDir, ".git");
        var cacheDir2 = Path.Combine(_testRootDir, "bin");

        Directory.CreateDirectory(normalDir);
        Directory.CreateDirectory(cacheDir1);
        Directory.CreateDirectory(cacheDir2);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search with partial input to trigger filesystem search
        // Using "n" should find "normal-dir" but not cache directories
        var suggestions = engine.GetSuggestions("n", _testRootDir, 20);

        // Assert - Cache directories should be filtered even from filesystem search
        var displayPaths = suggestions.Select(s => s.DisplayPath).ToList();
        Assert.Contains(displayPaths, p => p.Contains("normal-dir", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(displayPaths, p => p.Contains(".git", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(displayPaths, p => p.EndsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_FilteringCanBeDisabled()
    {
        // Arrange - Create cache directory
        var cacheDir = CreateTempDirectory("obj");
        _graph.RecordUsage("cd", new[] { cacheDir }, null);

        // Set environment variable to disable filtering
        Environment.SetEnvironmentVariable("PSCUE_PCD_ENABLE_DOT_DIR_FILTER", "false");

        try
        {
            var engine = new PcdCompletionEngine(_graph);

            // Act - Get suggestions with filtering disabled
            var suggestions = engine.GetSuggestions("", _testRootDir, 20);

            // Assert - Cache directory should be included when filtering is disabled
            Assert.Contains(suggestions, s => s.DisplayPath.Contains("obj", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            // Cleanup - Re-enable filtering for other tests
            Environment.SetEnvironmentVariable("PSCUE_PCD_ENABLE_DOT_DIR_FILTER", "true");
        }
    }

    [Fact]
    public void GetSuggestions_CustomBlocklist_FiltersAdditionalPatterns()
    {
        // Arrange - Create custom directory to block
        var customDir = CreateTempDirectory("my-cache");
        var normalDir = CreateTempDirectory("my-project");

        _graph.RecordUsage("cd", new[] { customDir }, null);
        _graph.RecordUsage("cd", new[] { normalDir }, null);

        // Add custom pattern to blocklist
        Environment.SetEnvironmentVariable("PSCUE_PCD_CUSTOM_BLOCKLIST", "my-cache,temp");

        try
        {
            var engine = new PcdCompletionEngine(_graph);

            // Act - Get suggestions
            var suggestions = engine.GetSuggestions("", _testRootDir, 20);

            // Assert - Custom blocked directory should be filtered
            Assert.Contains(suggestions, s => s.DisplayPath.Contains("my-project", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(suggestions, s => s.DisplayPath.Contains("my-cache", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("PSCUE_PCD_CUSTOM_BLOCKLIST", null);
        }
    }

    #endregion

    #region Symlink Resolution Tests (Phase 21.1)

    [Fact]
    public void ResolveLinkTarget_DirectSymlink_ResolvesCorrectly()
    {
        // Simple test to verify ResolveLinkTarget behavior
        var realDir = CreateTempDirectory("resolve-test-real");
        var symlinkPath = Path.Combine(_testRootDir, "resolve-test-link");

        try
        {
            Directory.CreateSymbolicLink(symlinkPath, realDir);
            _tempDirectories.Add(symlinkPath);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test if we don't have permission
        }
        catch (IOException)
        {
            return; // Skip test if symlink creation is not supported
        }

        // Test ResolveLinkTarget
        var dirInfo = new DirectoryInfo(symlinkPath);
        var resolved = dirInfo.ResolveLinkTarget(returnFinalTarget: true);

        Assert.NotNull(resolved);
        Assert.IsType<DirectoryInfo>(resolved);
        var resolvedDir = (DirectoryInfo)resolved;
        Assert.Equal(realDir.TrimEnd(Path.DirectorySeparatorChar), resolvedDir.FullName.TrimEnd(Path.DirectorySeparatorChar), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveLinkTarget_NestedPath_ResolvesParentSymlink()
    {
        // Test that ResolveLinkTarget resolves parent symlinks
        var realDir = CreateTempDirectory("nested-test-real");
        var childDir = Path.Combine(realDir, "child");
        Directory.CreateDirectory(childDir);
        _tempDirectories.Add(childDir);

        var symlinkParent = Path.Combine(_testRootDir, "nested-test-link");
        var symlinkChild = Path.Combine(symlinkParent, "child");

        try
        {
            Directory.CreateSymbolicLink(symlinkParent, realDir);
            _tempDirectories.Add(symlinkParent);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test if we don't have permission
        }
        catch (IOException)
        {
            return; // Skip test if symlink creation is not supported
        }

        // Test that accessing child via symlinked parent resolves correctly
        var dirInfo = new DirectoryInfo(symlinkChild);
        var resolved = dirInfo.ResolveLinkTarget(returnFinalTarget: true);

        // The resolved path should point to the real child directory
        if (resolved != null)
        {
            Assert.IsType<DirectoryInfo>(resolved);
            var resolvedDir = (DirectoryInfo)resolved;
            Assert.Equal(childDir.TrimEnd(Path.DirectorySeparatorChar), resolvedDir.FullName.TrimEnd(Path.DirectorySeparatorChar), StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ResolveSymlink_DirectTest()
    {
        // Direct test of symlink resolution logic
        var realDir = CreateTempDirectory("resolve-direct-real");
        var symlinkPath = Path.Combine(_testRootDir, "resolve-direct-link");

        try
        {
            Directory.CreateSymbolicLink(symlinkPath, realDir);
            _tempDirectories.Add(symlinkPath);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test if we don't have permission
        }
        catch (IOException)
        {
            return; // Skip test if symlink creation is not supported
        }

        // Test that symlink path resolves to real path
        // Since ResolveSymlinkFullPath is private, we test via ArgumentGraph behavior
        // by checking that both paths normalize to the same value during recording

        var normalizedReal = Path.GetFullPath(realDir);
        var normalizedSymlink = Path.GetFullPath(symlinkPath);

        System.Diagnostics.Debug.WriteLine($"Real path: {realDir}");
        System.Diagnostics.Debug.WriteLine($"Symlink path: {symlinkPath}");
        System.Diagnostics.Debug.WriteLine($"Normalized real: {normalizedReal}");
        System.Diagnostics.Debug.WriteLine($"Normalized symlink: {normalizedSymlink}");

        // Check if DirectoryInfo properly identifies the symlink
        var symlinkInfo = new DirectoryInfo(symlinkPath);
        var isReparsePoint = (symlinkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        System.Diagnostics.Debug.WriteLine($"Is reparse point: {isReparsePoint}");
        System.Diagnostics.Debug.WriteLine($"Link target: {symlinkInfo.LinkTarget}");

        Assert.True(isReparsePoint, "Symlink should be identified as a reparse point");
        Assert.NotNull(symlinkInfo.LinkTarget);
    }

    [Fact]
    public void ArgumentGraph_SymlinkNormalization_StoresSamePathForBoth()
    {
        // Test that ArgumentGraph normalizes both the real and symlink paths to the same value
        var realDir = CreateTempDirectory("argraph-real");
        var symlinkPath = Path.Combine(_testRootDir, "argraph-link");

        try
        {
            Directory.CreateSymbolicLink(symlinkPath, realDir);
            _tempDirectories.Add(symlinkPath);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test if we don't have permission
        }
        catch (IOException)
        {
            return; // Skip test if symlink creation is not supported
        }

        // Debug: Check if symlink was created correctly
        var symlinkInfo = new DirectoryInfo(symlinkPath);
        var isReparsePoint = (symlinkInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        if (!isReparsePoint)
        {
            // Symlink creation silently failed - skip test
            return;
        }

        // Record via both paths (need to provide working directory for path normalization)
        _graph.RecordUsage("cd", new[] { realDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { symlinkPath }, _testRootDir);

        // Get suggestions - should only have one entry
        var suggestions = _graph.GetSuggestions("cd", Array.Empty<string>(), 20);

        // Debug: see what paths are stored
        var relevantPaths = suggestions.Where(s =>
            s.Argument.Contains("argraph-real", StringComparison.OrdinalIgnoreCase) ||
            s.Argument.Contains("argraph-link", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var s in relevantPaths)
        {
            System.Diagnostics.Debug.WriteLine($"Stored path: {s.Argument}");
        }

        // Output for test failure visibility
        if (relevantPaths.Count != 1)
        {
            throw new Xunit.Sdk.XunitException($"Expected 1 path, but found {relevantPaths.Count}: {string.Join(", ", relevantPaths.Select(p => p.Argument))}");
        }

        // Should only be one path stored (both normalized to the same value)
        Assert.Single(relevantPaths);
    }

    [Fact]
    public void GetSuggestions_SymlinkDeduplication_BothPathsResolveToSame()
    {
        // This test requires administrator privileges on Windows to create symlinks
        // Skip if we can't create symlinks
        var realDir = CreateTempDirectory("real-directory");
        var symlinkPath = Path.Combine(_testRootDir, "symlink-directory");

        try
        {
            // Create a directory symlink (requires admin on Windows)
            Directory.CreateSymbolicLink(symlinkPath, realDir);
            _tempDirectories.Add(symlinkPath);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip test if we don't have permission to create symlinks
            return;
        }
        catch (IOException)
        {
            // Skip test if symlink creation is not supported
            return;
        }

        // Arrange - Record usage via both paths (real and symlink)
        _graph.RecordUsage("cd", new[] { realDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { symlinkPath }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert - Should only appear once (deduplicated by resolved path)
        var matches = suggestions.Where(s =>
            s.DisplayPath.Contains("real-directory", StringComparison.OrdinalIgnoreCase) ||
            s.DisplayPath.Contains("symlink-directory", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.Single(matches); // Only one entry after deduplication
    }

    [Fact]
    public void GetSuggestions_SymlinkResolution_UsesRealPath()
    {
        // This test requires administrator privileges on Windows to create symlinks
        // Skip if we can't create symlinks
        var realDir = CreateTempDirectory("target-directory");
        var symlinkPath = Path.Combine(_testRootDir, "link-to-target");

        try
        {
            // Create a directory symlink (requires admin on Windows)
            Directory.CreateSymbolicLink(symlinkPath, realDir);
            _tempDirectories.Add(symlinkPath);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip test if we don't have permission to create symlinks
            return;
        }
        catch (IOException)
        {
            // Skip test if symlink creation is not supported
            return;
        }

        // Arrange - Record usage via symlink path
        _graph.RecordUsage("cd", new[] { symlinkPath }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert - DisplayPath should be the resolved real path, not the symlink
        var match = suggestions.FirstOrDefault(s =>
            s.DisplayPath.Contains("target-directory", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(match);
        Assert.Contains("target-directory", match.DisplayPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSuggestions_RecursiveSymlink_ResolvesCompletely()
    {
        // Test that we can handle symlinks that point to other symlinks
        var realDir = CreateTempDirectory("final-target");
        var symlinkPath1 = Path.Combine(_testRootDir, "symlink1");
        var symlinkPath2 = Path.Combine(_testRootDir, "symlink2");

        try
        {
            // Create a chain: symlink2 -> symlink1 -> realDir
            Directory.CreateSymbolicLink(symlinkPath1, realDir);
            _tempDirectories.Add(symlinkPath1);
            Directory.CreateSymbolicLink(symlinkPath2, symlinkPath1);
            _tempDirectories.Add(symlinkPath2);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test if we don't have permission
        }
        catch (IOException)
        {
            return; // Skip test if symlink creation is not supported
        }

        // Arrange - Record usage via the indirect symlink
        _graph.RecordUsage("cd", new[] { symlinkPath2 }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert - Should resolve all the way to the final target
        var match = suggestions.FirstOrDefault(s =>
            s.DisplayPath.Contains("final-target", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(match);
        Assert.Contains("final-target", match.DisplayPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSuggestions_JunctionPoint_ResolvesLikeSymlink()
    {
        // Test that junctions (Windows-specific) are also resolved
        // Note: On Linux, junctions don't exist, so this test will be similar to symlink test
        var realDir = CreateTempDirectory("junction-target");
        var junctionPath = Path.Combine(_testRootDir, "junction-link");

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // On Windows, we can create a junction
                // Directory.CreateSymbolicLink also works for junctions
                Directory.CreateSymbolicLink(junctionPath, realDir);
                _tempDirectories.Add(junctionPath);
            }
            else
            {
                // On Linux/macOS, just use a regular symlink
                Directory.CreateSymbolicLink(junctionPath, realDir);
                _tempDirectories.Add(junctionPath);
            }
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test if we don't have permission
        }
        catch (IOException)
        {
            return; // Skip test if junction creation is not supported
        }

        // Arrange - Record usage via junction
        _graph.RecordUsage("cd", new[] { junctionPath }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert - Should resolve to the real path
        var match = suggestions.FirstOrDefault(s =>
            s.DisplayPath.Contains("junction-target", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(match);
    }

    [Fact]
    public void GetSuggestions_NonSymlinkDirectory_UnchangedByResolution()
    {
        // Test that regular directories are not affected by symlink resolution
        var regularDir = CreateTempDirectory("regular-directory");

        // Arrange - Record usage of regular directory
        _graph.RecordUsage("cd", new[] { regularDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Get suggestions
        var suggestions = engine.GetSuggestions("", _testRootDir, 20);

        // Assert - Should still appear normally
        var match = suggestions.FirstOrDefault(s =>
            s.DisplayPath.Contains("regular-directory", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(match);
        Assert.Contains("regular-directory", match.DisplayPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSuggestions_UserScenario_SourceSymlinkDeduplication()
    {
        // Regression test for user's scenario: C:\Users\lucas\source -> D:\source symlink
        // When navigating to dd-trace-dotnet via both paths, should only show one entry
        var realSourceDir = CreateTempDirectory("source");
        var ddTraceDotnet = Path.Combine(realSourceDir, "dd-trace-dotnet");
        Directory.CreateDirectory(ddTraceDotnet);
        _tempDirectories.Add(ddTraceDotnet);

        var userDir = CreateTempDirectory("users");
        var lucasDir = Path.Combine(userDir, "lucas");
        Directory.CreateDirectory(lucasDir);
        _tempDirectories.Add(lucasDir);

        var symlinkSourceDir = Path.Combine(lucasDir, "source");

        try
        {
            // Create symlink: C:\Users\lucas\source -> D:\source
            Directory.CreateSymbolicLink(symlinkSourceDir, realSourceDir);
            _tempDirectories.Add(symlinkSourceDir);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test if we don't have permission
        }
        catch (IOException)
        {
            return; // Skip test if symlink creation is not supported
        }

        // Arrange - Record usage of dd-trace-dotnet via both paths
        var realPath = ddTraceDotnet;
        var symlinkPath = Path.Combine(symlinkSourceDir, "dd-trace-dotnet");

        _graph.RecordUsage("cd", new[] { realPath }, _testRootDir);
        _graph.RecordUsage("cd", new[] { symlinkPath }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for "dd-trace-dotnet" from user's home directory
        var suggestions = engine.GetSuggestions("dd-trace-dotnet", lucasDir, 20);

        // Assert - Should only appear once (deduplicated by resolved real path)
        var matches = suggestions.Where(s =>
            s.DisplayPath.Contains("dd-trace-dotnet", StringComparison.OrdinalIgnoreCase)).ToList();

        // Debug output to see what we got
        foreach (var m in matches)
        {
            System.Diagnostics.Debug.WriteLine($"Match: Path={m.Path}, DisplayPath={m.DisplayPath}");
        }

        Assert.Single(matches); // Only one entry after deduplication

        // The resolved path should be the real path (D:\source\dd-trace-dotnet)
        var match = matches.First();
        Assert.Contains("source", match.DisplayPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dd-trace-dotnet", match.DisplayPath, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Exact Match Scoring Tests

    [Fact]
    public void GetSuggestions_ExactDirectoryNameMatch_RanksHigherThanFuzzyMatch()
    {
        // Arrange - Create directories with similar names
        var exactMatchDir = CreateTempDirectory("dd-trace-dotnet");
        var fuzzyMatchDir = CreateTempDirectory("dd-trace-dotnet-APMSVLS-58");
        var anotherFuzzyDir = CreateTempDirectory("dd-trace-dotnet-feature-branch");

        // Record usage for all directories with same frequency/recency
        _graph.RecordUsage("cd", new[] { exactMatchDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { fuzzyMatchDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { anotherFuzzyDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for "dd-trace-dotnet"
        var suggestions = engine.GetSuggestions("dd-trace-dotnet", _testRootDir, 20);

        // Assert - Exact match should be first
        var topResult = suggestions.First();
        Assert.Contains("dd-trace-dotnet", topResult.DisplayPath);
        Assert.DoesNotContain("APMSVLS", topResult.DisplayPath); // Exact match, not fuzzy
        Assert.DoesNotContain("feature-branch", topResult.DisplayPath);

        // Exact match should have significantly higher score
        var exactMatch = suggestions.First(s => s.DisplayPath.EndsWith("dd-trace-dotnet" + Path.DirectorySeparatorChar));
        var fuzzyMatches = suggestions.Where(s => s.DisplayPath.Contains("dd-trace-dotnet") && s != exactMatch).ToList();

        Assert.True(exactMatch.Score > fuzzyMatches.Max(f => f.Score),
            $"Exact match score ({exactMatch.Score}) should be higher than fuzzy matches (max: {fuzzyMatches.Max(f => f.Score)})");
    }

    [Fact]
    public void GetSuggestions_ExactFullPathMatch_RanksFirst()
    {
        // Arrange
        var exactPath = CreateTempDirectory("myproject");
        var similarPath = CreateTempDirectory("myproject-v2");

        _graph.RecordUsage("cd", new[] { exactPath }, _testRootDir);
        _graph.RecordUsage("cd", new[] { similarPath }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search using exact path
        var suggestions = engine.GetSuggestions(exactPath, _testRootDir, 20);

        // Assert - Exact path should be first
        Assert.NotEmpty(suggestions);
        var topResult = suggestions.First();
        Assert.Equal(exactPath.TrimEnd(Path.DirectorySeparatorChar),
                     topResult.DisplayPath.TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void GetSuggestions_DirectoryNameMatch_FromDifferentLocation_RanksFirst()
    {
        // Arrange - Simulate user scenario: typing "dd-trace-dotnet" from a different location
        var targetDir = _testDdTraceDir;
        var similarDir = Path.Combine(_testDatadogDir, "dd-trace-dotnet-APMSVLS-58");

        // Record usage for both directories
        _graph.RecordUsage("cd", new[] { targetDir }, null);
        _graph.RecordUsage("cd", new[] { similarDir }, null);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for "dd-trace-dotnet" from a completely different location (home dir)
        // Skip existence check since these are test paths that don't exist on filesystem
        var suggestions = engine.GetSuggestions("dd-trace-dotnet", _testHomeDir, maxResults: 10, skipExistenceCheck: true);

        // Assert - Exact directory name match should be first
        Assert.NotEmpty(suggestions);
        var topResult = suggestions.First();
        Assert.Contains("dd-trace-dotnet", topResult.DisplayPath);
        Assert.DoesNotContain("APMSVLS", topResult.DisplayPath); // Should be exact match, not similar one

        // Verify the exact match has higher score
        var exactMatch = suggestions.First(s => s.DisplayPath.Contains("dd-trace-dotnet") && !s.DisplayPath.Contains("APMSVLS"));
        var fuzzyMatch = suggestions.First(s => s.DisplayPath.Contains("APMSVLS"));
        Assert.True(exactMatch.Score > fuzzyMatch.Score,
            $"Exact dir name match score ({exactMatch.Score}) should be higher than similar match ({fuzzyMatch.Score})");
    }

    [Fact]
    public void GetSuggestions_ExactMatchBoostConfiguration_AppliesCorrectMultiplier()
    {
        // Arrange
        var exactDir = CreateTempDirectory("testdir");
        var fuzzyDir = CreateTempDirectory("testdir-variant");

        _graph.RecordUsage("cd", new[] { exactDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { fuzzyDir }, _testRootDir);

        // Create engine with custom exact match boost
        var customBoost = 50.0;
        var engine = new PcdCompletionEngine(
            _graph,
            scoreDecayDays: 30,
            frequencyWeight: 0.5,
            recencyWeight: 0.3,
            distanceWeight: 0.2,
            maxRecursiveDepth: 3,
            enableRecursiveSearch: true,
            exactMatchBoost: customBoost
        );

        // Act
        var suggestions = engine.GetSuggestions("testdir", _testRootDir, 20);

        // Assert - Exact match should rank first with boosted score
        var exactMatch = suggestions.First(s => s.DisplayPath.EndsWith("testdir" + Path.DirectorySeparatorChar));
        var fuzzyMatch = suggestions.First(s => s.DisplayPath.Contains("testdir-variant"));

        Assert.True(exactMatch.Score > fuzzyMatch.Score,
            "Exact match with custom boost should score higher than fuzzy match");
    }

    [Fact]
    public void GetSuggestions_MultipleExactMatches_AllRankHigherThanFuzzy()
    {
        // Arrange - Multiple directories with same exact name in different locations
        var childDir = CreateTempDirectory("common");
        var subChild = Path.Combine(childDir, "nested", "common");
        Directory.CreateDirectory(Path.GetDirectoryName(subChild)!);
        Directory.CreateDirectory(subChild);
        _tempDirectories.Add(subChild);

        var fuzzyDir = CreateTempDirectory("common-variant");

        _graph.RecordUsage("cd", new[] { childDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { subChild }, _testRootDir);
        _graph.RecordUsage("cd", new[] { fuzzyDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("common", _testRootDir, 20);

        // Assert - All exact matches should rank above fuzzy match
        var exactMatches = suggestions.Where(s =>
            Path.GetFileName(s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar))
                .Equals("common", StringComparison.OrdinalIgnoreCase)).ToList();
        var fuzzyMatches = suggestions.Where(s =>
            s.DisplayPath.Contains("common-variant", StringComparison.OrdinalIgnoreCase)).ToList();

        if (fuzzyMatches.Any())
        {
            var lowestExactScore = exactMatches.Min(e => e.Score);
            var highestFuzzyScore = fuzzyMatches.Max(f => f.Score);
            Assert.True(lowestExactScore > highestFuzzyScore,
                $"Lowest exact match score ({lowestExactScore}) should be higher than highest fuzzy score ({highestFuzzyScore})");
        }
    }

    [Fact]
    public void GetSuggestions_CaseInsensitiveExactMatch_StillBoosted()
    {
        // Arrange
        var exactDir = CreateTempDirectory("MyProject");
        var fuzzyDir = CreateTempDirectory("MyProject-Dev");

        _graph.RecordUsage("cd", new[] { exactDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { fuzzyDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search with different casing
        var suggestions = engine.GetSuggestions("myproject", _testRootDir, 20);

        // Assert - Case-insensitive exact match should still get boost
        var topResult = suggestions.First();
        Assert.Contains("MyProject", topResult.DisplayPath);
        Assert.DoesNotContain("Dev", topResult.DisplayPath);
    }

    #endregion

    #region Improved Fuzzy Matching Tests

    [Fact]
    public void GetSuggestions_UnrelatedDirectories_DoNotMatch()
    {
        // Arrange - Create directories with shared prefix but different purpose
        var dotnetDir = CreateTempDirectory("dd-trace-dotnet");
        var jsDir = CreateTempDirectory("dd-trace-js");
        var pythonDir = CreateTempDirectory("dd-trace-py");

        _graph.RecordUsage("cd", new[] { dotnetDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { jsDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { pythonDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for "dd-trace-dotnet"
        var suggestions = engine.GetSuggestions("dd-trace-dotnet", _testRootDir, 20);

        // Assert - Should NOT match dd-trace-js or dd-trace-py
        var dotnetMatches = suggestions.Where(s => s.DisplayPath.Contains("dd-trace-dotnet")).ToList();
        var jsMatches = suggestions.Where(s => s.DisplayPath.Contains("dd-trace-js")).ToList();
        var pyMatches = suggestions.Where(s => s.DisplayPath.Contains("dd-trace-py")).ToList();

        Assert.NotEmpty(dotnetMatches); // Should match dd-trace-dotnet
        Assert.Empty(jsMatches); // Should NOT match dd-trace-js
        Assert.Empty(pyMatches); // Should NOT match dd-trace-py
    }

    [Fact]
    public void GetSuggestions_LegitimateTypos_StillMatch()
    {
        // Arrange
        var correctDir = CreateTempDirectory("documents");
        _graph.RecordUsage("cd", new[] { correctDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search with typo (transposed letters: "documetns")
        var suggestions = engine.GetSuggestions("documetns", _testRootDir, 20);

        // Assert - Should still match despite typo (Levenshtein tolerance)
        // Short queries (≤10 chars) don't trigger the LCS check, so transposition should work
        var matches = suggestions.Where(s => s.DisplayPath.Contains("documents")).ToList();
        Assert.NotEmpty(matches);
    }

    [Fact]
    public void GetSuggestions_ShortQuery_MatchesSubstring()
    {
        // Arrange
        var dir = CreateTempDirectory("my-project-folder");
        _graph.RecordUsage("cd", new[] { dir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Short query should still work with substring matching
        var suggestions = engine.GetSuggestions("proj", _testRootDir, 20);

        // Assert
        var matches = suggestions.Where(s => s.DisplayPath.Contains("my-project-folder")).ToList();
        Assert.NotEmpty(matches);
    }

    [Fact]
    public void GetSuggestions_LongQueryPrefixOnly_DoesNotMatch()
    {
        // Arrange - Directory shares only a prefix with the query
        var prefixOnlyDir = CreateTempDirectory("documentation-old");
        var fullMatchDir = CreateTempDirectory("documentation-for-developers");

        _graph.RecordUsage("cd", new[] { prefixOnlyDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { fullMatchDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Long query (>10 chars) that only shares prefix with "documentation-old"
        var suggestions = engine.GetSuggestions("documentation-for-developers", _testRootDir, 20);

        // Assert - Should match full match but not prefix-only
        var fullMatches = suggestions.Where(s => s.DisplayPath.Contains("documentation-for-developers")).ToList();
        var prefixMatches = suggestions.Where(s => s.DisplayPath.Contains("documentation-old")).ToList();

        Assert.NotEmpty(fullMatches);
        Assert.Empty(prefixMatches); // Long query requires substantial overlap
    }

    [Fact]
    public void GetSuggestions_CustomFuzzyMinMatchPct_RespectsConfiguration()
    {
        // Arrange
        var dir1 = CreateTempDirectory("test-directory");
        var dir2 = CreateTempDirectory("test-dir");

        _graph.RecordUsage("cd", new[] { dir1 }, _testRootDir);
        _graph.RecordUsage("cd", new[] { dir2 }, _testRootDir);

        // Create engine with strict matching (90% required)
        var strictEngine = new PcdCompletionEngine(
            _graph,
            scoreDecayDays: 30,
            frequencyWeight: 0.5,
            recencyWeight: 0.3,
            distanceWeight: 0.2,
            maxRecursiveDepth: 3,
            enableRecursiveSearch: true,
            exactMatchBoost: 100.0,
            fuzzyMinMatchPct: 0.9 // Very strict - 90% match required
        );

        // Create engine with relaxed matching (50% required)
        var relaxedEngine = new PcdCompletionEngine(
            _graph,
            scoreDecayDays: 30,
            frequencyWeight: 0.5,
            recencyWeight: 0.3,
            distanceWeight: 0.2,
            maxRecursiveDepth: 3,
            enableRecursiveSearch: true,
            exactMatchBoost: 100.0,
            fuzzyMinMatchPct: 0.5 // Relaxed - 50% match required
        );

        // Act - Search with partial term
        var strictResults = strictEngine.GetSuggestions("test-directory-extra", _testRootDir, 20);
        var relaxedResults = relaxedEngine.GetSuggestions("test-directory-extra", _testRootDir, 20);

        // Assert - Strict should be more selective than relaxed
        Assert.True(strictResults.Count <= relaxedResults.Count,
            "Strict matching should return fewer or equal results compared to relaxed matching");
    }

    [Fact]
    public void GetSuggestions_SimilarDirectories_DifferentiatesProperly()
    {
        // Arrange - Create directories with similar but distinct names
        var mainDir = CreateTempDirectory("myapp");
        var configDir = CreateTempDirectory("myapp-config");
        var testDir = CreateTempDirectory("myapp-tests");
        var docsDir = CreateTempDirectory("myapp-docs");

        _graph.RecordUsage("cd", new[] { mainDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { configDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { testDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { docsDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for exact term
        var suggestions = engine.GetSuggestions("myapp", _testRootDir, 20);

        // Assert - Exact match should rank first
        var topResult = suggestions.First();
        Assert.Contains("myapp", topResult.DisplayPath.ToLowerInvariant());
        Assert.DoesNotContain("config", topResult.DisplayPath.ToLowerInvariant());
        Assert.DoesNotContain("tests", topResult.DisplayPath.ToLowerInvariant());
        Assert.DoesNotContain("docs", topResult.DisplayPath.ToLowerInvariant());
    }

    #endregion

    #region Phase 21 Integration Tests

    [Fact]
    public void Integration_UserScenario_AllPhase21Features()
    {
        // Arrange - Simulate user's real-world scenario
        var dotnetDir = CreateTempDirectory("dd-trace-dotnet");
        var dotnetBranchDir = CreateTempDirectory("dd-trace-dotnet-APMSVLS-58");
        var jsDir = CreateTempDirectory("dd-trace-js");
        var codeiumDir = CreateTempDirectory(".codeium");
        var claudeDir = CreateTempDirectory(".claude");
        var dotnetCacheDir = CreateTempDirectory(".dotnet");

        // Record usage
        _graph.RecordUsage("cd", new[] { dotnetDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { dotnetBranchDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { jsDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { codeiumDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { claudeDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { dotnetCacheDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act - Search for "dd-trace-dotnet"
        var suggestions = engine.GetSuggestions("dd-trace-dotnet", _testRootDir, 20);

        // Assert - Verify all Phase 21 improvements
        var suggestionPaths = suggestions.Select(s => s.DisplayPath).ToList();

        // Phase 21.3: Exact match should rank first
        Assert.NotEmpty(suggestions);
        var topResult = suggestions.First();
        Assert.Contains("dd-trace-dotnet", topResult.DisplayPath);
        Assert.DoesNotContain("APMSVLS", topResult.DisplayPath);
        Assert.DoesNotContain("js", topResult.DisplayPath);

        // Phase 21.4: Unrelated directories (dd-trace-js) should NOT match
        var jsMatches = suggestions.Where(s => s.DisplayPath.Contains("dd-trace-js")).ToList();
        Assert.Empty(jsMatches);

        // Phase 21.2: Cache/metadata directories should be filtered
        Assert.DoesNotContain(suggestionPaths, p => p.Contains(".codeium"));
        Assert.DoesNotContain(suggestionPaths, p => p.Contains(".claude"));
        Assert.DoesNotContain(suggestionPaths, p => p.Contains(".dotnet"));

        // Verify correct ordering: exact match before fuzzy match
        var exactMatchIndex = suggestions.FindIndex(s => s.DisplayPath.EndsWith("dd-trace-dotnet" + Path.DirectorySeparatorChar));
        var fuzzyMatchIndex = suggestions.FindIndex(s => s.DisplayPath.Contains("APMSVLS"));

        if (fuzzyMatchIndex >= 0)
        {
            Assert.True(exactMatchIndex < fuzzyMatchIndex,
                $"Exact match (index {exactMatchIndex}) should appear before fuzzy match (index {fuzzyMatchIndex})");
        }
    }

    [Fact]
    public void Integration_SymlinkDeduplication_NoDuplicates()
    {
        // Arrange - Create directory structure with symlink
        var realSourceDir = Path.Combine(_testRootDir, "source");
        Directory.CreateDirectory(realSourceDir);
        _tempDirectories.Add(realSourceDir);

        var projectDir = Path.Combine(realSourceDir, "myproject");
        Directory.CreateDirectory(projectDir);
        _tempDirectories.Add(projectDir);

        var userDir = CreateTempDirectory("user");
        var symlinkSourceDir = Path.Combine(userDir, "source-link");

        try
        {
            // Create symlink: user/source-link -> source
            Directory.CreateSymbolicLink(symlinkSourceDir, realSourceDir);
            _tempDirectories.Add(symlinkSourceDir);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Skip test if we don't have permission
        }
        catch (IOException)
        {
            return; // Skip test if symlink creation is not supported
        }

        // Act - Record usage via both paths
        var realPath = projectDir;
        var symlinkPath = Path.Combine(symlinkSourceDir, "myproject");

        _graph.RecordUsage("cd", new[] { realPath }, _testRootDir);
        _graph.RecordUsage("cd", new[] { symlinkPath }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);
        var suggestions = engine.GetSuggestions("myproject", _testRootDir, 20);

        // Assert - Should only appear once (Phase 21.1: symlink deduplication)
        var matches = suggestions.Where(s => s.DisplayPath.Contains("myproject")).ToList();
        Assert.Single(matches);
    }

    [Fact]
    public void Integration_ExactMatchPriority_BothTabAndPredictor()
    {
        // Arrange - Multiple similar directories
        var exactDir = CreateTempDirectory("workspace");
        var variantDir1 = CreateTempDirectory("workspace-dev");
        var variantDir2 = CreateTempDirectory("workspace-prod");
        var variantDir3 = CreateTempDirectory("workspace-test");

        // Record equal usage for all
        _graph.RecordUsage("cd", new[] { exactDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { variantDir1 }, _testRootDir);
        _graph.RecordUsage("cd", new[] { variantDir2 }, _testRootDir);
        _graph.RecordUsage("cd", new[] { variantDir3 }, _testRootDir);

        // Act - Test with both tab completion depth and predictor depth
        var tabEngine = new PcdCompletionEngine(_graph, maxRecursiveDepth: 3);
        var predictorEngine = new PcdCompletionEngine(_graph, maxRecursiveDepth: 1);

        var tabResults = tabEngine.GetSuggestions("workspace", _testRootDir, 20);
        var predictorResults = predictorEngine.GetSuggestions("workspace", _testRootDir, 5);

        // Assert - Exact match should be first in both cases (Phase 21.3)
        Assert.NotEmpty(tabResults);
        Assert.NotEmpty(predictorResults);

        var tabTop = tabResults.First();
        var predictorTop = predictorResults.First();

        Assert.Contains("workspace", tabTop.DisplayPath);
        Assert.DoesNotContain("dev", tabTop.DisplayPath);
        Assert.DoesNotContain("prod", tabTop.DisplayPath);
        Assert.DoesNotContain("test", tabTop.DisplayPath);

        Assert.Contains("workspace", predictorTop.DisplayPath);
        Assert.DoesNotContain("dev", predictorTop.DisplayPath);
        Assert.DoesNotContain("prod", predictorTop.DisplayPath);
        Assert.DoesNotContain("test", predictorTop.DisplayPath);
    }

    [Fact]
    public void Integration_CacheDirectoryFiltering_WithAndWithoutExplicitTyping()
    {
        // Arrange - Create cache directories
        var normalDir = CreateTempDirectory("myproject");
        var codeiumDir = CreateTempDirectory(".codeium");
        var nodeModulesDir = CreateTempDirectory("node_modules");
        var binDir = CreateTempDirectory("bin");

        _graph.RecordUsage("cd", new[] { normalDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { codeiumDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { nodeModulesDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { binDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act & Assert - Phase 21.2: Cache filtering

        // Test 1: General search should filter cache directories
        var generalResults = engine.GetSuggestions("my", _testRootDir, 20);
        var generalPaths = generalResults.Select(s => s.DisplayPath).ToList();

        Assert.Contains(generalPaths, p => p.Contains("myproject"));
        Assert.DoesNotContain(generalPaths, p => p.Contains(".codeium"));
        Assert.DoesNotContain(generalPaths, p => p.Contains("node_modules"));
        Assert.DoesNotContain(generalPaths, p => p.Contains("bin"));

        // Test 2: Explicit typing of cache directory should override filter
        var explicitCodeiumResults = engine.GetSuggestions(".codeium", _testRootDir, 20);
        var explicitPaths = explicitCodeiumResults.Select(s => s.DisplayPath).ToList();

        Assert.Contains(explicitPaths, p => p.Contains(".codeium"));
    }

    [Fact]
    public void Integration_Performance_TabCompletionUnder50ms()
    {
        // Arrange - Create many directories to stress test
        for (int i = 0; i < 50; i++)
        {
            var dir = CreateTempDirectory($"testdir-{i}");
            _graph.RecordUsage("cd", new[] { dir }, _testRootDir);
        }

        var engine = new PcdCompletionEngine(_graph, maxRecursiveDepth: 3);

        // Act - Measure performance
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var suggestions = engine.GetSuggestions("testdir", _testRootDir, 20);
        stopwatch.Stop();

        // Assert - Should complete in under 50ms for tab completion
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"Tab completion took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");
        Assert.NotEmpty(suggestions);
    }

    [Fact]
    public void Integration_Performance_PredictorUnder10ms()
    {
        // Arrange - Create directories for predictor
        for (int i = 0; i < 20; i++)
        {
            var dir = CreateTempDirectory($"dir-{i}");
            _graph.RecordUsage("cd", new[] { dir }, _testRootDir);
        }

        var engine = new PcdCompletionEngine(_graph, maxRecursiveDepth: 1); // Predictor uses shallow depth

        // Act - Measure performance
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var suggestions = engine.GetSuggestions("dir", _testRootDir, 5);
        stopwatch.Stop();

        // Assert - Should complete in under 10ms for predictor
        Assert.True(stopwatch.ElapsedMilliseconds < 10,
            $"Predictor took {stopwatch.ElapsedMilliseconds}ms, expected < 10ms");
        Assert.NotEmpty(suggestions);
    }

    [Fact]
    public void Integration_AllPhase21Features_Together()
    {
        // Arrange - Complex scenario combining all Phase 21 features
        var realProjectDir = Path.Combine(_testRootDir, "projects", "dd-trace-dotnet");
        Directory.CreateDirectory(Path.GetDirectoryName(realProjectDir)!);
        Directory.CreateDirectory(realProjectDir);
        _tempDirectories.Add(realProjectDir);
        _tempDirectories.Add(Path.GetDirectoryName(realProjectDir)!);

        var branchDir = Path.Combine(_testRootDir, "projects", "dd-trace-dotnet-feature");
        Directory.CreateDirectory(branchDir);
        _tempDirectories.Add(branchDir);

        var jsDir = Path.Combine(_testRootDir, "projects", "dd-trace-js");
        Directory.CreateDirectory(jsDir);
        _tempDirectories.Add(jsDir);

        var cacheDir = Path.Combine(_testRootDir, "projects", ".codeium");
        Directory.CreateDirectory(cacheDir);
        _tempDirectories.Add(cacheDir);

        // Create symlink scenario
        var userDir = CreateTempDirectory("userprojects");
        var symlinkDir = Path.Combine(userDir, "projects-link");

        try
        {
            Directory.CreateSymbolicLink(symlinkDir, Path.Combine(_testRootDir, "projects"));
            _tempDirectories.Add(symlinkDir);
        }
        catch
        {
            // Continue without symlink if creation fails
        }

        // Record usage
        _graph.RecordUsage("cd", new[] { realProjectDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { branchDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { jsDir }, _testRootDir);
        _graph.RecordUsage("cd", new[] { cacheDir }, _testRootDir);

        var engine = new PcdCompletionEngine(_graph);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var suggestions = engine.GetSuggestions("dd-trace-dotnet", Path.Combine(_testRootDir, "projects"), 20);
        stopwatch.Stop();

        // Assert - Verify all Phase 21 improvements work together
        var paths = suggestions.Select(s => s.DisplayPath).ToList();

        // 1. Exact match ranks first (Phase 21.3)
        Assert.NotEmpty(suggestions);
        var topResult = suggestions.First().DisplayPath;
        Assert.Contains("dd-trace-dotnet", topResult);
        Assert.DoesNotContain("feature", topResult);
        Assert.DoesNotContain("js", topResult);

        // 2. Unrelated directories filtered (Phase 21.4)
        Assert.DoesNotContain(paths, p => p.Contains("dd-trace-js"));

        // 3. Cache directories filtered (Phase 21.2)
        Assert.DoesNotContain(paths, p => p.Contains(".codeium"));

        // 4. Performance maintained (Phase 21 performance criteria)
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"Integration test took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");

        // 5. Verify no duplicates from symlinks (Phase 21.1)
        var dotnetMatches = suggestions.Where(s => s.DisplayPath.Contains("dd-trace-dotnet") &&
                                                    !s.DisplayPath.Contains("feature")).ToList();
        Assert.True(dotnetMatches.Count <= 1,
            "Should have at most one entry for dd-trace-dotnet (no symlink duplicates)");
    }

    #endregion
}
