namespace PSCue.Shared.Completions.Json;

public sealed class CommandDefinition
{
    public string CompletionText { get; set; } = "";
    public string? Tooltip { get; set; }
    public string? Alias { get; set; }
    public CommandDefinition[]? SubCommands { get; set; }
    public ParameterDefinition[]? Parameters { get; set; }
}

public sealed class ParameterDefinition
{
    public string CompletionText { get; set; } = "";
    public string? Tooltip { get; set; }
    public string? Alias { get; set; }
    public bool RequiresValue { get; set; }
    public ArgumentDefinition[]? StaticArguments { get; set; }
}

public sealed class ArgumentDefinition
{
    public string CompletionText { get; set; } = "";
    public string? Tooltip { get; set; }
}
