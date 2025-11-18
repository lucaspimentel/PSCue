using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PSCue.Module;

/// <summary>
/// Enhanced completion engine for PCD (PowerShell Change Directory) command.
/// Provides fuzzy matching, frecency scoring, distance-based ranking, and recursive search.
/// </summary>
public class PcdCompletionEngine
{
    private readonly ArgumentGraph _graph;
    private readonly int _scoreDecayDays;
    private readonly double _frequencyWeight;
    private readonly double _recencyWeight;
    private readonly double _distanceWeight;
    private readonly int _maxRecursiveDepth;
    private readonly bool _enableRecursiveSearch;
    private readonly double _exactMatchBoost;
    private readonly double _fuzzyMinMatchPct;

    /// <summary>
    /// Well-known directory shortcuts that should be handled quickly.
    /// </summary>
    private static readonly Dictionary<string, string> WellKnownShortcuts = new(StringComparer.OrdinalIgnoreCase)
    {
        { "~", null! }, // Will be replaced with actual home directory
        { "..", null! }, // Will be replaced with parent directory
        { ".", null! }   // Will be replaced with current directory
    };

    /// <summary>
    /// Creates a new PCD completion engine.
    /// </summary>
    /// <param name="graph">The ArgumentGraph with learned directory data.</param>
    /// <param name="scoreDecayDays">Days for recency decay (default: 30).</param>
    /// <param name="frequencyWeight">Weight for frequency scoring (default: 0.5).</param>
    /// <param name="recencyWeight">Weight for recency scoring (default: 0.3).</param>
    /// <param name="distanceWeight">Weight for distance scoring (default: 0.2).</param>
    /// <param name="maxRecursiveDepth">Maximum depth for recursive search (default: 3).</param>
    /// <param name="enableRecursiveSearch">Enable recursive filesystem search (default: true).</param>
    /// <param name="exactMatchBoost">Multiplier for exact match scores (default: 100.0).</param>
    /// <param name="fuzzyMinMatchPct">Minimum match percentage for fuzzy matching (default: 0.7).</param>
    public PcdCompletionEngine(
        ArgumentGraph graph,
        int scoreDecayDays = 30,
        double frequencyWeight = 0.5,
        double recencyWeight = 0.3,
        double distanceWeight = 0.2,
        int maxRecursiveDepth = 3,
        bool enableRecursiveSearch = true,
        double exactMatchBoost = 100.0,
        double fuzzyMinMatchPct = 0.7)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _scoreDecayDays = scoreDecayDays;
        _frequencyWeight = frequencyWeight;
        _recencyWeight = recencyWeight;
        _distanceWeight = distanceWeight;
        _maxRecursiveDepth = maxRecursiveDepth;
        _enableRecursiveSearch = enableRecursiveSearch;
        _exactMatchBoost = exactMatchBoost;
        _fuzzyMinMatchPct = fuzzyMinMatchPct;
    }

    /// <summary>
    /// Gets directory suggestions with enhanced scoring and matching.
    /// </summary>
    /// <param name="wordToComplete">The partial directory path typed by the user.</param>
    /// <param name="currentDirectory">The current working directory.</param>
    /// <param name="maxResults">Maximum number of results to return (default: 20).</param>
    /// <returns>List of directory suggestions sorted by score (highest first).</returns>
    public List<PcdSuggestion> GetSuggestions(string? wordToComplete, string currentDirectory, int maxResults = 20)
    {
        wordToComplete ??= string.Empty;
        var suggestions = new List<PcdSuggestion>();

        // Stage 1: Check for well-known shortcuts
        var shortcutSuggestions = GetWellKnownShortcuts(wordToComplete, currentDirectory);
        suggestions.AddRange(shortcutSuggestions);

        // Stage 2: Get learned directories with enhanced scoring
        var learnedSuggestions = GetLearnedDirectories(wordToComplete, currentDirectory, maxResults);
        suggestions.AddRange(learnedSuggestions);

        // Stage 3a: Direct filesystem search for matching child directories (non-recursive, always enabled)
        if (!string.IsNullOrWhiteSpace(wordToComplete))
        {
            var directChildSuggestions = GetDirectChildMatches(wordToComplete, currentDirectory, maxResults);
            suggestions.AddRange(directChildSuggestions);
        }

        // Stage 3b: Recursive filesystem search (always enabled when configured, depth-controlled)
        if (_enableRecursiveSearch && !string.IsNullOrWhiteSpace(wordToComplete))
        {
            var recursiveSuggestions = GetRecursiveMatches(wordToComplete, currentDirectory, maxResults);
            suggestions.AddRange(recursiveSuggestions);
        }

        // Normalize paths: resolve symlinks and ensure DisplayPath has trailing separator for consistency
        foreach (var suggestion in suggestions)
        {
            // Resolve symlinks to real paths for proper deduplication
            var resolvedPath = ResolveSymlink(suggestion.DisplayPath);
            if (resolvedPath != null)
            {
                suggestion.DisplayPath = resolvedPath;
            }

            if (!suggestion.DisplayPath.EndsWith(Path.DirectorySeparatorChar) &&
                !suggestion.DisplayPath.EndsWith(Path.AltDirectorySeparatorChar))
            {
                suggestion.DisplayPath += Path.DirectorySeparatorChar;
            }
        }

        // Deduplicate by DisplayPath (normalized full path with symlinks resolved) and sort by score
        return suggestions
            .GroupBy(s => s.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(s => s.Score).First())
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.UsageCount)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Stage 1: Gets well-known shortcuts (~, .., .) if they match the input.
    /// These get highest priority for instant navigation.
    /// Only suggests shortcuts when user is typing relative paths.
    /// </summary>
    private List<PcdSuggestion> GetWellKnownShortcuts(string wordToComplete, string currentDirectory)
    {
        var suggestions = new List<PcdSuggestion>();

        // Skip shortcuts entirely if user is typing an absolute path
        // Example: "pcd D:\source\datadog\doc" should not suggest ".."
        if (Path.IsPathRooted(wordToComplete))
        {
            return suggestions;
        }

        // Home directory (~)
        if ("~".StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            // Only suggest home directory if we're not already there
            if (!string.IsNullOrEmpty(homeDir) &&
                Directory.Exists(homeDir) &&
                !homeDir.Equals(currentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new PcdSuggestion
                {
                    Path = "~",
                    DisplayPath = homeDir,
                    Score = 1000.0, // Highest priority
                    UsageCount = 0,
                    LastUsed = DateTime.MinValue,
                    MatchType = MatchType.WellKnown,
                    Tooltip = $"Home directory: {homeDir}"
                });
            }
        }

        // Parent directory (..)
        if ("..".StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var parentDir = Directory.GetParent(currentDirectory)?.FullName;
                // Parent directory is by definition different from current, but check it exists
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    suggestions.Add(new PcdSuggestion
                    {
                        Path = "..",
                        DisplayPath = parentDir,
                        Score = 999.0,
                        UsageCount = 0,
                        LastUsed = DateTime.MinValue,
                        MatchType = MatchType.WellKnown,
                        Tooltip = $"Parent directory: {parentDir}"
                    });
                }
            }
            catch
            {
                // Ignore errors (e.g., root directory has no parent)
            }
        }

        // Note: We don't suggest "." (current directory) as it's not useful in tab completion
        // Users can still type "pcd ." if they want, but it won't show in suggestions

        return suggestions;
    }

    /// <summary>
    /// Stage 2: Gets learned directories from ArgumentGraph with enhanced scoring.
    /// Combines frequency, recency, distance, and fuzzy matching.
    /// </summary>
    private List<PcdSuggestion> GetLearnedDirectories(string wordToComplete, string currentDirectory, int maxResults)
    {
        var learnedStats = _graph.GetSuggestions("cd", Array.Empty<string>(), maxResults * 2);
        var suggestions = new List<PcdSuggestion>();

        foreach (var stats in learnedStats)
        {
            var path = stats.Argument;

            // Skip paths that shouldn't appear in tab completion suggestions
            // "-" (previous directory) and "." (current directory) can still be typed but won't show in suggestions
            if (path == "-" || path == ".")
                continue;

            // Skip current directory paths (not useful in suggestions)
            if (path.Equals(currentDirectory, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip paths that don't exist on the filesystem
            if (!Directory.Exists(path))
                continue;

            // Filter out cache/metadata directories unless explicitly typed
            if (ShouldFilterDirectory(path, wordToComplete))
                continue;

            // Skip parent directories when user is typing an absolute path
            // Example: typing "pcd D:\source\datadog\doc" should not suggest "D:\source\datadog\"
            if (Path.IsPathRooted(wordToComplete) && !string.IsNullOrEmpty(wordToComplete))
            {
                try
                {
                    var inputDir = Path.GetDirectoryName(wordToComplete);
                    if (!string.IsNullOrEmpty(inputDir) &&
                        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Equals(inputDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                   StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip parent directory
                    }
                }
                catch
                {
                    // Ignore path parsing errors
                }
            }

            // Calculate match score
            var matchScore = CalculateMatchScore(path, wordToComplete);
            if (matchScore <= 0.0)
                continue; // No match

            // Calculate frecency score (frequency + recency)
            var frequencyScore = CalculateFrequencyScore(stats.UsageCount, learnedStats.Max(s => s.UsageCount));
            var recencyScore = CalculateRecencyScore(stats.LastUsed);

            // Calculate distance score (proximity to current directory)
            var distanceScore = CalculateDistanceScore(path, currentDirectory);

            // Combined score with configurable weights
            // Exact matches get massive boost to ensure they appear first
            var isExactMatch = IsExactMatch(path, wordToComplete);
            var exactMatchMultiplier = isExactMatch ? _exactMatchBoost : 1.0;
            var totalScore = (matchScore * 0.1 * exactMatchMultiplier) +
                           (_frequencyWeight * frequencyScore) +
                           (_recencyWeight * recencyScore) +
                           (_distanceWeight * distanceScore);

            // Convert to relative path when possible (for tab completion and predictor)
            var relativePath = ToRelativePath(path, currentDirectory);

            suggestions.Add(new PcdSuggestion
            {
                Path = relativePath,
                DisplayPath = path, // Keep full path for display/tooltips
                Score = totalScore,
                UsageCount = stats.UsageCount,
                LastUsed = stats.LastUsed,
                MatchType = DetermineMatchType(path, wordToComplete),
                Tooltip = $"{path} - Used {stats.UsageCount} times (last: {stats.LastUsed:yyyy-MM-dd})"
            });
        }

        return suggestions;
    }

    /// <summary>
    /// Stage 3a: Searches for matching child directories in the parent directory (non-recursive).
    /// Used for tab completion to show existing directories without expensive recursive search.
    /// </summary>
    private List<PcdSuggestion> GetDirectChildMatches(string wordToComplete, string currentDirectory, int maxResults)
    {
        var suggestions = new List<PcdSuggestion>();

        try
        {
            // Determine the parent directory to search
            string? parentDir = null;
            string searchPattern = wordToComplete;

            if (Path.IsPathRooted(wordToComplete))
            {
                // Absolute path: extract parent directory
                // Example: "D:\source\datadog\t" -> parent = "D:\source\datadog", pattern = "t"
                var directoryPart = Path.GetDirectoryName(wordToComplete);
                if (!string.IsNullOrEmpty(directoryPart) && Directory.Exists(directoryPart))
                {
                    parentDir = directoryPart;
                    searchPattern = Path.GetFileName(wordToComplete);
                }
            }
            else
            {
                // Relative path: use current directory as parent
                parentDir = currentDirectory;
            }

            // If we have a valid parent directory and search pattern, list matching directories
            if (parentDir != null && !string.IsNullOrWhiteSpace(searchPattern))
            {
                foreach (var subDir in Directory.GetDirectories(parentDir))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (string.IsNullOrEmpty(dirName))
                        continue;

                    // Filter out cache/metadata directories unless explicitly typed
                    if (ShouldFilterDirectory(subDir, wordToComplete))
                        continue;

                    // Check if directory name matches the search pattern (prefix or fuzzy match)
                    if (dirName.StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        var matchScore = 0.8; // High score for prefix match
                        var relativePath = ToRelativePath(subDir, currentDirectory);

                        suggestions.Add(new PcdSuggestion
                        {
                            Path = relativePath,
                            DisplayPath = subDir,
                            Score = matchScore,
                            UsageCount = 0,
                            LastUsed = DateTime.MinValue,
                            MatchType = MatchType.Filesystem,
                            Tooltip = $"{subDir} - Found in filesystem"
                        });
                    }
                    else if (dirName.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        var matchScore = 0.6; // Lower score for substring match
                        var relativePath = ToRelativePath(subDir, currentDirectory);

                        suggestions.Add(new PcdSuggestion
                        {
                            Path = relativePath,
                            DisplayPath = subDir,
                            Score = matchScore,
                            UsageCount = 0,
                            LastUsed = DateTime.MinValue,
                            MatchType = MatchType.Filesystem,
                            Tooltip = $"{subDir} - Found in filesystem"
                        });
                    }

                    if (suggestions.Count >= maxResults)
                        break;
                }
            }
        }
        catch
        {
            // Ignore filesystem access errors
        }

        return suggestions;
    }

    /// <summary>
    /// Stage 3b: Recursively searches filesystem for matching directories.
    /// Only used if enabled and learned suggestions are insufficient (best-match navigation).
    /// </summary>
    private List<PcdSuggestion> GetRecursiveMatches(string wordToComplete, string currentDirectory, int maxResults)
    {
        var suggestions = new List<PcdSuggestion>();

        // Extract directory name to search for
        var searchTerm = Path.GetFileName(wordToComplete.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(searchTerm))
            return suggestions;

        // Start from current directory or a reasonable root
        var searchRoot = currentDirectory;
        if (Path.IsPathRooted(wordToComplete))
        {
            var rootPart = Path.GetPathRoot(wordToComplete);
            if (!string.IsNullOrEmpty(rootPart))
                searchRoot = rootPart;
        }

        try
        {
            RecursiveSearch(searchRoot, searchTerm, currentDirectory, 0, suggestions, maxResults);
        }
        catch
        {
            // Ignore filesystem access errors
        }

        return suggestions;
    }

    /// <summary>
    /// Recursively searches directories for matches.
    /// </summary>
    private void RecursiveSearch(string directory, string searchTerm, string currentDirectory, int depth, List<PcdSuggestion> results, int maxResults)
    {
        if (depth > _maxRecursiveDepth || results.Count >= maxResults)
            return;

        try
        {
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                if (results.Count >= maxResults)
                    break;

                var dirName = Path.GetFileName(subDir);
                if (string.IsNullOrEmpty(dirName))
                    continue;

                // Filter out cache/metadata directories unless explicitly typed
                if (ShouldFilterDirectory(subDir, searchTerm))
                    continue;

                // Check if directory name matches
                var matchScore = CalculateFuzzyMatchScore(dirName, searchTerm);
                if (matchScore > 0.3) // Threshold for fuzzy match
                {
                    // Convert to relative path when possible
                    var relativePath = ToRelativePath(subDir, currentDirectory);

                    results.Add(new PcdSuggestion
                    {
                        Path = relativePath,
                        DisplayPath = subDir, // Keep full path for display/tooltips
                        Score = matchScore * 0.5, // Lower score than learned directories
                        UsageCount = 0,
                        LastUsed = DateTime.MinValue,
                        MatchType = MatchType.Filesystem,
                        Tooltip = $"{subDir} - Found in filesystem (depth: {depth})"
                    });
                }

                // Recurse into subdirectory
                RecursiveSearch(subDir, searchTerm, currentDirectory, depth + 1, results, maxResults);
            }
        }
        catch
        {
            // Ignore access denied and other errors
        }
    }

    /// <summary>
    /// Checks if a directory should be filtered out based on the blocklist.
    /// Directories are filtered if:
    /// 1. They match a blocklisted pattern, AND
    /// 2. The user didn't explicitly type the pattern (e.g., typing ".claude" allows .claude results)
    /// </summary>
    /// <param name="directoryPath">Full path to the directory.</param>
    /// <param name="userInput">The input string the user typed.</param>
    /// <returns>True if the directory should be filtered out (not shown).</returns>
    private static bool ShouldFilterDirectory(string directoryPath, string userInput)
    {
        var blocklist = PcdConfiguration.Blocklist;
        if (blocklist.Count == 0)
        {
            return false; // Filtering disabled
        }

        var dirName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(dirName))
        {
            return false;
        }

        // Check if directory matches any blocklisted pattern
        foreach (var pattern in blocklist)
        {
            if (dirName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                // Exception: Allow if user explicitly typed this pattern
                // e.g., typing ".claude" should show .claude directories
                if (!string.IsNullOrWhiteSpace(userInput) &&
                    userInput.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Don't filter - user explicitly wants this
                }

                return true; // Filter out
            }
        }

        return false; // Not blocklisted
    }

    /// <summary>
    /// Determines if a path is an exact match to the search term.
    /// Checks both full path equality and exact directory name match.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <param name="wordToComplete">The search term entered by the user.</param>
    /// <returns>True if the path exactly matches the search term.</returns>
    private bool IsExactMatch(string path, string wordToComplete)
    {
        if (string.IsNullOrWhiteSpace(wordToComplete))
            return false; // Empty input doesn't count as exact match

        // Exact full path match
        if (path.Equals(wordToComplete, StringComparison.OrdinalIgnoreCase))
            return true;

        // Extract directory name from both path and search term for comparison
        var pathDirName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var searchDirName = Path.GetFileName(wordToComplete.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        // Exact directory name match (e.g., "dd-trace-dotnet" matches "D:\source\datadog\dd-trace-dotnet")
        if (!string.IsNullOrEmpty(pathDirName) &&
            !string.IsNullOrEmpty(searchDirName) &&
            pathDirName.Equals(searchDirName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates match score based on how well the path matches the input.
    /// Returns 0.0-1.0 score (higher is better).
    /// </summary>
    private double CalculateMatchScore(string path, string wordToComplete)
    {
        if (string.IsNullOrWhiteSpace(wordToComplete))
            return 1.0; // Empty input matches everything

        // Exact match
        if (path.Equals(wordToComplete, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        // Starts with (prefix match)
        if (path.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            return 0.9;

        // Fuzzy match (substring or Levenshtein distance)
        return CalculateFuzzyMatchScore(path, wordToComplete);
    }

    /// <summary>
    /// Calculates fuzzy match score using substring matching and Levenshtein distance.
    /// Returns 0.0-0.8 score (capped below exact/prefix matches).
    /// Uses configurable minimum match percentage to prevent unrelated matches.
    /// </summary>
    private double CalculateFuzzyMatchScore(string target, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 0.0;

        // Substring match (case-insensitive)
        if (target.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            // For substring matches, 100% of the query is in the target
            // This always passes the minimum match percentage requirement
            // Score based on position (earlier is better)
            var index = target.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            var positionScore = 1.0 - ((double)index / target.Length);
            return 0.7 * positionScore;
        }

        // Levenshtein distance - only for typo tolerance
        var distance = CalculateLevenshteinDistance(target, query);
        var maxLength = Math.Max(target.Length, query.Length);
        var similarity = 1.0 - ((double)distance / maxLength);

        // Check minimum match percentage requirement (based on similarity)
        if (similarity < _fuzzyMinMatchPct)
            return 0.0;

        // Additional check: for long queries (>10 chars), require tighter matching
        // to prevent matches that only share a prefix
        if (query.Length > 10)
        {
            // Calculate longest common substring to ensure substantial overlap
            var lcs = CalculateLongestCommonSubstring(target.ToLowerInvariant(), query.ToLowerInvariant());
            var lcsPercentage = (double)lcs / query.Length;

            // Require at least 60% of the query to appear as a continuous substring
            if (lcsPercentage < 0.6)
                return 0.0;
        }

        // Threshold: only return score if similarity is reasonable
        return similarity > 0.5 ? similarity * 0.6 : 0.0;
    }

    /// <summary>
    /// Calculates the length of the longest common substring between two strings.
    /// Used to ensure substantial overlap for long query terms.
    /// </summary>
    private int CalculateLongestCommonSubstring(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0;

        var maxLength = 0;
        var table = new int[str1.Length + 1, str2.Length + 1];

        for (int i = 1; i <= str1.Length; i++)
        {
            for (int j = 1; j <= str2.Length; j++)
            {
                if (str1[i - 1] == str2[j - 1])
                {
                    table[i, j] = table[i - 1, j - 1] + 1;
                    maxLength = Math.Max(maxLength, table[i, j]);
                }
            }
        }

        return maxLength;
    }

    /// <summary>
    /// Calculates Levenshtein distance between two strings (edit distance).
    /// </summary>
    private int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        var n = source.Length;
        var m = target.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = char.ToLowerInvariant(source[i - 1]) == char.ToLowerInvariant(target[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[n, m];
    }

    /// <summary>
    /// Calculates frequency score (0.0-1.0) based on usage count.
    /// </summary>
    private double CalculateFrequencyScore(int usageCount, int maxUsageCount)
    {
        if (maxUsageCount == 0)
            return 0.0;

        return (double)usageCount / maxUsageCount;
    }

    /// <summary>
    /// Calculates recency score (0.0-1.0) with exponential decay.
    /// </summary>
    private double CalculateRecencyScore(DateTime lastUsed)
    {
        var age = DateTime.UtcNow - lastUsed;
        var ageDays = age.TotalDays;

        if (ageDays <= 0)
            return 1.0; // Used today

        // Exponential decay
        var decayFactor = Math.Exp(-ageDays / _scoreDecayDays);
        return Math.Max(0.0, Math.Min(1.0, decayFactor));
    }

    /// <summary>
    /// Calculates distance score (0.0-1.0) based on directory proximity.
    /// Closer directories get higher scores.
    /// </summary>
    private double CalculateDistanceScore(string path, string currentDirectory)
    {
        try
        {
            // Normalize paths
            var normalizedPath = Path.GetFullPath(path);
            var normalizedCurrent = Path.GetFullPath(currentDirectory);

            // Same directory
            if (normalizedPath.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            // Parent directory
            var parent = Directory.GetParent(normalizedCurrent)?.FullName;
            if (parent != null)
            {
                // Normalize both paths by trimming trailing separators before comparing
                var normalizedPathTrimmed = normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parentTrimmed = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (normalizedPathTrimmed.Equals(parentTrimmed, StringComparison.OrdinalIgnoreCase))
                    return 0.9;
            }

            // Child directory
            if (normalizedPath.StartsWith(normalizedCurrent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var depth = normalizedPath.Substring(normalizedCurrent.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;
                return Math.Max(0.5, 0.85 - (depth * 0.1));
            }

            // Sibling directory (same parent)
            var pathParent = Directory.GetParent(normalizedPath)?.FullName;
            if (pathParent != null && parent != null)
            {
                // Normalize both paths by trimming trailing separators before comparing
                var pathParentTrimmed = pathParent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parentTrimmed = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (pathParentTrimmed.Equals(parentTrimmed, StringComparison.OrdinalIgnoreCase))
                    return 0.7;
            }

            // Common ancestor (calculate depth difference)
            var commonAncestorDepth = GetCommonAncestorDepth(normalizedPath, normalizedCurrent);
            if (commonAncestorDepth > 0)
            {
                var maxDepth = Math.Max(GetPathDepth(normalizedPath), GetPathDepth(normalizedCurrent));
                return Math.Max(0.1, 0.6 - ((maxDepth - commonAncestorDepth) * 0.05));
            }

            // No relation
            return 0.1;
        }
        catch
        {
            return 0.1; // Error handling - return low score
        }
    }

    /// <summary>
    /// Gets the depth of a path (number of directory separators).
    /// </summary>
    private int GetPathDepth(string path)
    {
        return path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Finds the depth of the common ancestor between two paths.
    /// Returns 0 if no common ancestor.
    /// </summary>
    private int GetCommonAncestorDepth(string path1, string path2)
    {
        var parts1 = path1.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var parts2 = path2.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        var commonDepth = 0;
        var minLength = Math.Min(parts1.Length, parts2.Length);

        for (int i = 0; i < minLength; i++)
        {
            if (parts1[i].Equals(parts2[i], StringComparison.OrdinalIgnoreCase))
                commonDepth++;
            else
                break;
        }

        return commonDepth;
    }

    /// <summary>
    /// Converts an absolute path to a relative path when possible to reduce visual noise.
    /// Only returns relative paths that are valid from the current directory.
    /// </summary>
    /// <param name="absolutePath">The absolute path to convert.</param>
    /// <param name="currentDirectory">The current working directory.</param>
    /// <returns>Relative path if valid from current directory, otherwise absolute path.</returns>
    private string ToRelativePath(string absolutePath, string currentDirectory)
    {
        try
        {
            // Normalize paths for comparison
            var normAbsolute = Path.GetFullPath(absolutePath);
            var normCurrent = Path.GetFullPath(currentDirectory);

            // Check if paths are on the same drive (Windows) or root (Unix)
            var absoluteRoot = Path.GetPathRoot(normAbsolute);
            var currentRoot = Path.GetPathRoot(normCurrent);
            if (!string.Equals(absoluteRoot, currentRoot, StringComparison.OrdinalIgnoreCase))
            {
                // Different drives/roots - must use absolute path
                return normAbsolute;
            }

            // If it's the parent directory, use ".."
            var parent = Directory.GetParent(normCurrent)?.FullName;
            if (parent != null)
            {
                // Normalize both paths by trimming trailing separators before comparing
                // This handles cases where absolutePath has a trailing separator from learned data
                var normAbsoluteTrimmed = normAbsolute.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parentTrimmed = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (normAbsoluteTrimmed.Equals(parentTrimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return "..";
                }
            }

            // If it's a child directory, use relative path (without redundant .\ prefix)
            if (normAbsolute.StartsWith(normCurrent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var childPath = normAbsolute.Substring(normCurrent.Length + 1);
                // Remove trailing separator from relative path for cleaner display
                childPath = childPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                // Return bare relative path (no .\ prefix) - cleaner and less visual noise
                // Only use relative if it's shorter than absolute
                if (childPath.Length < normAbsolute.Length)
                {
                    return childPath;
                }
            }

            // If it's a sibling directory, use relative path via parent
            if (parent != null)
            {
                var pathParent = Directory.GetParent(normAbsolute)?.FullName;
                if (pathParent != null && pathParent.Equals(parent, StringComparison.OrdinalIgnoreCase))
                {
                    var dirName = Path.GetFileName(normAbsolute.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    var relativePath = ".." + Path.DirectorySeparatorChar + dirName;
                    // Only use relative if it's shorter than absolute
                    if (relativePath.Length < normAbsolute.Length)
                    {
                        return relativePath;
                    }
                }
            }

            // Otherwise use absolute path
            return normAbsolute;
        }
        catch
        {
            // If anything fails, return original path
            return absolutePath;
        }
    }

    /// <summary>
    /// Determines the match type based on how the path matches the input.
    /// </summary>
    private MatchType DetermineMatchType(string path, string wordToComplete)
    {
        if (string.IsNullOrWhiteSpace(wordToComplete))
            return MatchType.Learned;

        if (path.Equals(wordToComplete, StringComparison.OrdinalIgnoreCase))
            return MatchType.Exact;

        if (path.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            return MatchType.Prefix;

        if (path.Contains(wordToComplete, StringComparison.OrdinalIgnoreCase))
            return MatchType.Fuzzy;

        return MatchType.Learned;
    }

    /// <summary>
    /// Resolves symlinks, junctions, and directory links to their real target paths.
    /// Handles both the directory itself and any symlinked parent directories in the path.
    /// Returns the fully resolved path, or null if resolution fails.
    /// </summary>
    private static string? ResolveSymlink(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return null;

            // Use Path.GetFullPath to get the absolute path first
            var realPath = Path.GetFullPath(path);

            // Walk from the root down, resolving symlinks as we go
            // This ensures we resolve symlinks in parent directories
            var parts = realPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var currentPath = string.Empty;

            // Handle drive letter on Windows (e.g., "C:")
            if (parts.Length > 0 && parts[0].EndsWith(":"))
            {
                currentPath = parts[0] + Path.DirectorySeparatorChar;
                parts = parts.Skip(1).ToArray();
            }
            else if (realPath.StartsWith(Path.DirectorySeparatorChar.ToString()))
            {
                // Unix-style absolute path
                currentPath = Path.DirectorySeparatorChar.ToString();
            }

            foreach (var part in parts)
            {
                currentPath = Path.Combine(currentPath, part);

                if (Directory.Exists(currentPath))
                {
                    var dirInfo2 = new DirectoryInfo(currentPath);

                    // Check if this component is a symlink
                    if ((dirInfo2.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        var target = dirInfo2.LinkTarget;
                        if (!string.IsNullOrEmpty(target))
                        {
                            // Make target absolute if relative
                            if (!Path.IsPathRooted(target))
                            {
                                var parent = dirInfo2.Parent?.FullName;
                                if (!string.IsNullOrEmpty(parent))
                                {
                                    target = Path.GetFullPath(Path.Combine(parent, target));
                                }
                            }

                            // Replace current path with resolved target
                            currentPath = target;
                        }
                    }
                }
            }

            // Return the resolved path (different from original means symlink was resolved)
            return currentPath.Equals(realPath, StringComparison.OrdinalIgnoreCase) ? null : currentPath;
        }
        catch
        {
            // Resolution failed - caller will use original path
            return null;
        }
    }
}

/// <summary>
/// Represents a directory suggestion with enhanced metadata.
/// </summary>
public class PcdSuggestion
{
    /// <summary>
    /// The path to use for completion (may be relative like "~" or "..").
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The full display path to show in tooltips.
    /// </summary>
    public string DisplayPath { get; set; } = string.Empty;

    /// <summary>
    /// Combined score from frequency, recency, distance, and match quality.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Number of times this directory has been visited (0 for filesystem discoveries).
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// When this directory was last visited (DateTime.MinValue for filesystem discoveries).
    /// </summary>
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// Type of match (well-known, exact, prefix, fuzzy, etc.).
    /// </summary>
    public MatchType MatchType { get; set; }

    /// <summary>
    /// Tooltip text to display in completion UI.
    /// </summary>
    public string Tooltip { get; set; } = string.Empty;
}

/// <summary>
/// Type of match for a suggestion.
/// </summary>
public enum MatchType
{
    /// <summary>Well-known shortcut like ~, .., or .</summary>
    WellKnown,
    /// <summary>Exact match to input.</summary>
    Exact,
    /// <summary>Path starts with input (prefix match).</summary>
    Prefix,
    /// <summary>Fuzzy match (substring or Levenshtein distance).</summary>
    Fuzzy,
    /// <summary>Learned from history (no specific match criteria).</summary>
    Learned,
    /// <summary>Found via filesystem search.</summary>
    Filesystem
}
