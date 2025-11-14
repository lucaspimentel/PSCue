namespace PSCue.Shared.Completions;

public sealed class CommandParameter(string completionText, string? tooltip = null)
    : ICompletionWithChildren
{
    public string CompletionText { get; } = completionText;
    public string? Tooltip { get; } = tooltip;
    public string? Alias { get; init; }
    public StaticArgument[] StaticArguments { get; init; } = [];
    public DynamicArgumentsFactory? DynamicArguments { get; init; }

    /// <summary>
    /// Indicates whether this parameter requires a value (e.g., "-f net6.0").
    /// If true, the parser will treat the next argument as a value bound to this parameter.
    /// </summary>
    public bool RequiresValue { get; init; }

    public ICompletion? FindNode(ReadOnlySpan<char> wordToComplete)
    {
        // Check if the word matches either the main completion text or the alias
        if (Helpers.Equals(CompletionText, wordToComplete) ||
            (Alias is not null && Helpers.Equals(Alias, wordToComplete)))
        {
            return this;
        }

        var completion = Helpers.FindEquals(StaticArguments, wordToComplete);

        if (completion is null && DynamicArguments?.Invoke() is { } arguments)
        {
            completion = Helpers.FindEquals(arguments, wordToComplete);
        }

        return completion;
    }

    public List<ICompletion> GetCompletions(ReadOnlySpan<char> wordToComplete, bool includeDynamicArguments)
    {
        var results = new List<ICompletion>();

        Helpers.AddWhereStartsWith(StaticArguments, results, wordToComplete);

        // Only include dynamic arguments if requested
        if (includeDynamicArguments && DynamicArguments?.Invoke() is { } arguments)
        {
            Helpers.AddWhereStartsWith(arguments, results, wordToComplete);
        }

        return results;
    }

    public override string ToString()
    {
        return CompletionText;
    }
}
