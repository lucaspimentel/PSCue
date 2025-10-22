namespace PSCue.Shared.Completions;

public delegate IEnumerable<DynamicArgument> DynamicArgumentsFactory();

public sealed class Command(string completionText, string? tooltip = null)
    : ICompletionWithChildren
{
    public string CompletionText { get; } = completionText;
    public string? Tooltip { get; } = tooltip;
    public Command[] SubCommands { get; init; } = [];
    public CommandParameter[] Parameters { get; init; } = [];
    public DynamicArgumentsFactory? DynamicArguments { get; init; }

    public ICompletion? FindNode(ReadOnlySpan<char> wordToComplete)
    {
        var completion = Helpers.ThisIfEquals(this, wordToComplete) ??
                         Helpers.FindEquals(SubCommands, wordToComplete) ??
                         Helpers.FindEquals(Parameters, wordToComplete);

        if (completion is null && DynamicArguments?.Invoke() is { } arguments)
        {
            completion = Helpers.FindEquals(arguments, wordToComplete);
        }

        return completion;
    }

    public List<ICompletion> GetCompletions(ReadOnlySpan<char> wordToComplete, bool includeDynamicArguments)
    {
        var results = new List<ICompletion>();

        Helpers.AddWhereStartsWith(SubCommands, results, wordToComplete);

        // Add parameters that match the search, including by alias
        foreach (var param in Parameters)
        {
            // Check if the long form starts with the search text
            var longFormMatches = Helpers.StartsWith(param.CompletionText, wordToComplete);

            // Check if the alias (short form) starts with the search text
            var aliasMatches = param.Alias is not null && Helpers.StartsWith(param.Alias, wordToComplete);

            if (longFormMatches || aliasMatches)
            {
                results.Add(param);
            }
        }

        // Only include dynamic arguments (git branches, scoop packages, etc.) if requested
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
