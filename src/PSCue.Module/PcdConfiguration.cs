using System;
using System.Collections.Generic;
using System.Linq;

namespace PSCue.Module;

/// <summary>
/// Shared configuration for PCD (PowerShell Change Directory) functionality.
/// Reads environment variables and provides defaults for both tab completion and inline predictions.
/// </summary>
public static class PcdConfiguration
{
    /// <summary>
    /// Default blocklist of cache/metadata directories to filter out.
    /// These directories clutter results and are rarely navigation targets.
    /// </summary>
    private static readonly string[] DefaultBlocklist =
    {
        ".codeium",     // Codeium cache
        ".claude",      // Claude AI metadata
        ".dotnet",      // .NET tools cache
        ".nuget",       // NuGet package cache
        ".git",         // Git internals (but .github is allowed)
        ".vs",          // Visual Studio cache
        ".vscode",      // VSCode cache (but .vscode/extensions might be useful)
        ".idea",        // JetBrains IDE cache
        "node_modules", // NPM packages
        "bin",          // Build output
        "obj",          // Build intermediate files
        "target",       // Build output (Rust, Java)
        "__pycache__",  // Python cache
        ".pytest_cache" // Pytest cache
    };

    /// <summary>
    /// Gets the frequency weight for frecency scoring.
    /// Environment variable: PSCUE_PCD_FREQUENCY_WEIGHT
    /// Default: 0.5
    /// </summary>
    public static double FrequencyWeight => GetEnvDouble("PSCUE_PCD_FREQUENCY_WEIGHT", 0.5);

    /// <summary>
    /// Gets the recency weight for frecency scoring.
    /// Environment variable: PSCUE_PCD_RECENCY_WEIGHT
    /// Default: 0.3
    /// </summary>
    public static double RecencyWeight => GetEnvDouble("PSCUE_PCD_RECENCY_WEIGHT", 0.3);

    /// <summary>
    /// Gets the distance weight for proximity scoring.
    /// Environment variable: PSCUE_PCD_DISTANCE_WEIGHT
    /// Default: 0.2
    /// </summary>
    public static double DistanceWeight => GetEnvDouble("PSCUE_PCD_DISTANCE_WEIGHT", 0.2);

    /// <summary>
    /// Gets the maximum depth for recursive search in tab completion.
    /// Environment variable: PSCUE_PCD_MAX_DEPTH
    /// Default: 3
    /// </summary>
    public static int TabCompletionMaxDepth => GetEnvInt("PSCUE_PCD_MAX_DEPTH", 3);

    /// <summary>
    /// Gets the maximum depth for recursive search in inline predictions (predictor).
    /// Environment variable: PSCUE_PCD_PREDICTOR_MAX_DEPTH
    /// Default: 1
    /// </summary>
    public static int PredictorMaxDepth => GetEnvInt("PSCUE_PCD_PREDICTOR_MAX_DEPTH", 1);

    /// <summary>
    /// Gets whether recursive search is enabled.
    /// Environment variable: PSCUE_PCD_RECURSIVE_SEARCH
    /// Default: true
    /// </summary>
    public static bool EnableRecursiveSearch => GetEnvBool("PSCUE_PCD_RECURSIVE_SEARCH", true);

    /// <summary>
    /// Gets the score decay period in days.
    /// Default: 30
    /// </summary>
    public static int ScoreDecayDays => 30;

    /// <summary>
    /// Gets whether partial command predictions are enabled.
    /// Environment variable: PSCUE_PARTIAL_COMMAND_PREDICTIONS
    /// Default: true
    /// </summary>
    public static bool EnablePartialCommandPredictions => GetEnvBool("PSCUE_PARTIAL_COMMAND_PREDICTIONS", true);

    /// <summary>
    /// Gets whether cache/metadata directory filtering is enabled.
    /// Environment variable: PSCUE_PCD_ENABLE_DOT_DIR_FILTER
    /// Default: true
    /// </summary>
    public static bool EnableDotDirFilter => GetEnvBool("PSCUE_PCD_ENABLE_DOT_DIR_FILTER", true);

    /// <summary>
    /// Gets the exact match score boost multiplier.
    /// When a directory name or path exactly matches the search term,
    /// the match score is multiplied by this factor to ensure exact matches
    /// always rank higher than fuzzy matches.
    /// Environment variable: PSCUE_PCD_EXACT_MATCH_BOOST
    /// Default: 100.0
    /// </summary>
    public static double ExactMatchBoost => GetEnvDouble("PSCUE_PCD_EXACT_MATCH_BOOST", 100.0);

    /// <summary>
    /// Gets the combined blocklist (default + custom patterns).
    /// Custom patterns can be specified via PSCUE_PCD_CUSTOM_BLOCKLIST (comma-separated).
    /// </summary>
    public static IReadOnlyList<string> Blocklist
    {
        get
        {
            if (!EnableDotDirFilter)
            {
                return Array.Empty<string>();
            }

            var customBlocklist = Environment.GetEnvironmentVariable("PSCUE_PCD_CUSTOM_BLOCKLIST");
            if (string.IsNullOrWhiteSpace(customBlocklist))
            {
                return DefaultBlocklist;
            }

            // Combine default + custom patterns
            var customPatterns = customBlocklist.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return DefaultBlocklist.Concat(customPatterns).ToArray();
        }
    }

    /// <summary>
    /// Helper to read double from environment variable.
    /// </summary>
    private static double GetEnvDouble(string key, double defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return double.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Helper to read int from environment variable.
    /// </summary>
    private static int GetEnvInt(string key, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Helper to read bool from environment variable.
    /// </summary>
    private static bool GetEnvBool(string key, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? defaultValue;
    }
}
