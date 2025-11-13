using System;

namespace PSCue.Module;

/// <summary>
/// Shared configuration for PCD (PowerShell Change Directory) functionality.
/// Reads environment variables and provides defaults for both tab completion and inline predictions.
/// </summary>
public static class PcdConfiguration
{
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
