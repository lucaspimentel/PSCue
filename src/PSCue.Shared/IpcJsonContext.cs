using System.Text.Json.Serialization;

namespace PSCue.Shared;

/// <summary>
/// JSON source generation context for IPC protocol types.
/// Required for NativeAOT compilation in ArgumentCompleter.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(IpcRequest))]
[JsonSerializable(typeof(IpcResponse))]
[JsonSerializable(typeof(CompletionItem))]
[JsonSerializable(typeof(IpcDebugRequest))]
[JsonSerializable(typeof(IpcDebugResponse))]
[JsonSerializable(typeof(CacheStats))]
[JsonSerializable(typeof(CacheEntryInfo))]
public partial class IpcJsonContext : JsonSerializerContext
{
}
