namespace PSCue.Shared;

/// <summary>
/// IPC request sent from ArgumentCompleter to Predictor
/// </summary>
public class IpcRequest
{
    public string Command { get; set; } = string.Empty;
    public string[] Args { get; set; } = Array.Empty<string>();
    public string WordToComplete { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
}

/// <summary>
/// IPC response sent from Predictor to ArgumentCompleter
/// </summary>
public class IpcResponse
{
    public CompletionItem[] Completions { get; set; } = Array.Empty<CompletionItem>();
    public bool Cached { get; set; }
}

/// <summary>
/// Represents a single completion suggestion
/// </summary>
public class CompletionItem
{
    public string Text { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Score { get; set; }
}
