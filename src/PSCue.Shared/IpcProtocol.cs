using System.Text.Json.Serialization;

namespace PSCue.Shared;

/// <summary>
/// IPC request sent from ArgumentCompleter to Predictor
/// </summary>
public class IpcRequest
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("commandLine")]
    public string CommandLine { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public string[] Args { get; set; } = Array.Empty<string>();

    [JsonPropertyName("wordToComplete")]
    public string WordToComplete { get; set; } = string.Empty;

    [JsonPropertyName("cursorPosition")]
    public int CursorPosition { get; set; }

    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = string.Empty;
}

/// <summary>
/// IPC response sent from Predictor to ArgumentCompleter
/// </summary>
public class IpcResponse
{
    [JsonPropertyName("completions")]
    public CompletionItem[] Completions { get; set; } = Array.Empty<CompletionItem>();

    [JsonPropertyName("cached")]
    public bool Cached { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

/// <summary>
/// Represents a single completion suggestion
/// </summary>
public class CompletionItem
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }
}

/// <summary>
/// IPC request for debug/stats operations
/// </summary>
public class IpcDebugRequest
{
    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = string.Empty; // "ping", "stats", "cache"

    [JsonPropertyName("filter")]
    public string? Filter { get; set; } // Optional filter for cache inspection
}

/// <summary>
/// IPC response for debug/stats operations
/// </summary>
public class IpcDebugResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("stats")]
    public CacheStats? Stats { get; set; }

    [JsonPropertyName("cacheEntries")]
    public CacheEntryInfo[]? CacheEntries { get; set; }
}

/// <summary>
/// Cache statistics for debugging
/// </summary>
public class CacheStats
{
    [JsonPropertyName("entryCount")]
    public int EntryCount { get; set; }

    [JsonPropertyName("totalHits")]
    public int TotalHits { get; set; }

    [JsonPropertyName("oldestEntryAge")]
    public string OldestEntryAge { get; set; } = string.Empty;
}

/// <summary>
/// Information about a single cache entry
/// </summary>
public class CacheEntryInfo
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("completionCount")]
    public int CompletionCount { get; set; }

    [JsonPropertyName("hitCount")]
    public int HitCount { get; set; }

    [JsonPropertyName("age")]
    public string Age { get; set; } = string.Empty;

    [JsonPropertyName("topCompletions")]
    public CompletionItem[] TopCompletions { get; set; } = Array.Empty<CompletionItem>();
}

/// <summary>
/// IPC protocol constants and utilities.
/// </summary>
public static class IpcProtocol
{
    /// <summary>
    /// Named pipe name prefix. Full pipe name: PSCue-{ProcessId}
    /// </summary>
    public const string PipeNamePrefix = "PSCue";

    /// <summary>
    /// Maximum time to wait for IPC connection (milliseconds)
    /// </summary>
    public const int ConnectionTimeoutMs = 5;

    /// <summary>
    /// Maximum time to wait for IPC response (milliseconds)
    /// </summary>
    public const int ResponseTimeoutMs = 50;

    /// <summary>
    /// Get the pipe name for a specific PowerShell process
    /// </summary>
    public static string GetPipeName(int processId) => $"{PipeNamePrefix}-{processId}";

    /// <summary>
    /// Get the pipe name for the current PowerShell process.
    /// First checks PSCUE_PID environment variable, otherwise uses current process ID.
    /// </summary>
    public static string GetCurrentPipeName()
    {
        // Check if PSCUE_PID environment variable is set (set by PSCue.psm1)
        var pscuePid = Environment.GetEnvironmentVariable("PSCUE_PID");
        if (pscuePid != null && int.TryParse(pscuePid, out var pid))
        {
            return GetPipeName(pid);
        }

        // Fallback to current process ID (for Debug tool or when running in-process)
        return GetPipeName(Environment.ProcessId);
    }
}
