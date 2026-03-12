using System.Text.Json.Serialization;

namespace PSCue.Shared.Completions.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CommandDefinition))]
internal partial class CompletionJsonContext : JsonSerializerContext;
