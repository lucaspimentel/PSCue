using System.Text.Json;

namespace PSCue.Shared.Completions.Json;

public static class JsonCompletionLoader
{
    public static Command? LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public static Command? LoadFromJson(string json)
    {
        var definition = JsonSerializer.Deserialize(json, CompletionJsonContext.Default.CommandDefinition);
        return definition is null ? null : MapCommand(definition);
    }

    private static Command MapCommand(CommandDefinition def) =>
        new(def.CompletionText, def.Tooltip)
        {
            Alias = def.Alias,
            SubCommands = def.SubCommands is { Length: > 0 }
                ? Array.ConvertAll(def.SubCommands, MapCommand)
                : [],
            Parameters = def.Parameters is { Length: > 0 }
                ? Array.ConvertAll(def.Parameters, MapParameter)
                : []
        };

    private static CommandParameter MapParameter(ParameterDefinition def) =>
        new(def.CompletionText, def.Tooltip)
        {
            Alias = def.Alias,
            RequiresValue = def.RequiresValue,
            StaticArguments = def.StaticArguments is { Length: > 0 }
                ? Array.ConvertAll(def.StaticArguments, MapArgument)
                : []
        };

    private static StaticArgument MapArgument(ArgumentDefinition def) =>
        new(def.CompletionText, def.Tooltip);
}
