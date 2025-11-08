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
    /// <param name="enableRecursiveSearch">Enable recursive filesystem search (default: false).</param>
    public PcdCompletionEngine(
        ArgumentGraph graph,
        int scoreDecayDays = 30,
        double frequencyWeight = 0.5,
        double recencyWeight = 0.3,
        double distanceWeight = 0.2,
        int maxRecursiveDepth = 3,
        bool enableRecursiveSearch = false)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _scoreDecayDays = scoreDecayDays;
        _frequencyWeight = frequencyWeight;
        _recencyWeight = recencyWeight;
        _distanceWeight = distanceWeight;
        _maxRecursiveDepth = maxRecursiveDepth;
        _enableRecursiveSearch = enableRecursiveSearch;
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

        // Stage 3: Recursive filesystem search (if enabled and needed)
        if (_enableRecursiveSearch && suggestions.Count < maxResults / 2 && !string.IsNullOrWhiteSpace(wordToComplete))
        {
            var recursiveSuggestions = GetRecursiveMatches(wordToComplete, currentDirectory, maxResults);
            suggestions.AddRange(recursiveSuggestions);
        }

        // Deduplicate and sort by score
        return suggestions
            .GroupBy(s => s.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(s => s.Score).First())
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.UsageCount)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Stage 1: Gets well-known shortcuts (~, .., .) if they match the input.
    /// These get highest priority for instant navigation.
    /// </summary>
    private List<PcdSuggestion> GetWellKnownShortcuts(string wordToComplete, string currentDirectory)
    {
        var suggestions = new List<PcdSuggestion>();

        // Home directory (~)
        if ("~".StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(homeDir) && Directory.Exists(homeDir))
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

        // Current directory (.)
        if (".".StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase) && wordToComplete.Length < 2)
        {
            if (Directory.Exists(currentDirectory))
            {
                suggestions.Add(new PcdSuggestion
                {
                    Path = ".",
                    DisplayPath = currentDirectory,
                    Score = 998.0,
                    UsageCount = 0,
                    LastUsed = DateTime.MinValue,
                    MatchType = MatchType.WellKnown,
                    Tooltip = $"Current directory: {currentDirectory}"
                });
            }
        }

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
            var totalScore = (matchScore * 0.1) +
                           (_frequencyWeight * frequencyScore) +
                           (_recencyWeight * recencyScore) +
                           (_distanceWeight * distanceScore);

            suggestions.Add(new PcdSuggestion
            {
                Path = path,
                DisplayPath = path,
                Score = totalScore,
                UsageCount = stats.UsageCount,
                LastUsed = stats.LastUsed,
                MatchType = DetermineMatchType(path, wordToComplete),
                Tooltip = $"Used {stats.UsageCount} times (last: {stats.LastUsed:yyyy-MM-dd})"
            });
        }

        return suggestions;
    }

    /// <summary>
    /// Stage 3: Recursively searches filesystem for matching directories.
    /// Only used if enabled and learned suggestions are insufficient.
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
            RecursiveSearch(searchRoot, searchTerm, 0, suggestions, maxResults);
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
    private void RecursiveSearch(string directory, string searchTerm, int depth, List<PcdSuggestion> results, int maxResults)
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

                // Check if directory name matches
                var matchScore = CalculateFuzzyMatchScore(dirName, searchTerm);
                if (matchScore > 0.3) // Threshold for fuzzy match
                {
                    results.Add(new PcdSuggestion
                    {
                        Path = subDir,
                        DisplayPath = subDir,
                        Score = matchScore * 0.5, // Lower score than learned directories
                        UsageCount = 0,
                        LastUsed = DateTime.MinValue,
                        MatchType = MatchType.Filesystem,
                        Tooltip = $"Found in filesystem (depth: {depth})"
                    });
                }

                // Recurse into subdirectory
                RecursiveSearch(subDir, searchTerm, depth + 1, results, maxResults);
            }
        }
        catch
        {
            // Ignore access denied and other errors
        }
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
    /// </summary>
    private double CalculateFuzzyMatchScore(string target, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 0.0;

        // Substring match (case-insensitive)
        if (target.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            // Score based on position (earlier is better)
            var index = target.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            var positionScore = 1.0 - ((double)index / target.Length);
            return 0.7 * positionScore;
        }

        // Levenshtein distance
        var distance = CalculateLevenshteinDistance(target, query);
        var maxLength = Math.Max(target.Length, query.Length);
        var similarity = 1.0 - ((double)distance / maxLength);

        // Threshold: only return score if similarity is reasonable
        return similarity > 0.5 ? similarity * 0.6 : 0.0;
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
            if (parent != null && normalizedPath.Equals(parent, StringComparison.OrdinalIgnoreCase))
                return 0.9;

            // Child directory
            if (normalizedPath.StartsWith(normalizedCurrent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var depth = normalizedPath.Substring(normalizedCurrent.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;
                return Math.Max(0.5, 0.85 - (depth * 0.1));
            }

            // Sibling directory (same parent)
            var pathParent = Directory.GetParent(normalizedPath)?.FullName;
            if (pathParent != null && parent != null && pathParent.Equals(parent, StringComparison.OrdinalIgnoreCase))
                return 0.7;

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
