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

    [JsonPropertyName("includeDynamicArguments")]
    public bool IncludeDynamicArguments { get; set; } = true;
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
    /// Get the pipe name for the current PowerShell process
    /// </summary>
    public static string GetCurrentPipeName() => GetPipeName(Environment.ProcessId);
}
