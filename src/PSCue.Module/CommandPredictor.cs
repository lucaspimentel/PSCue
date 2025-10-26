using System.Management.Automation.Subsystem.Prediction;
using PSCue.Shared;

namespace PSCue.Module;

public class CommandPredictor : ICommandPredictor
{
    /// <summary>
    /// Gets the unique identifier for a subsystem implementation.
    /// </summary>
    public Guid Id { get; } = new("01a1e2c5-fbc1-4cf3-8178-ac2e55232434");

    /// <summary>
    /// Gets the name of a subsystem implementation.
    /// </summary>
    public string Name => "PSCue";

    /// <summary>
    /// Gets the description of a subsystem implementation.
    /// </summary>
    public string Description => "A PowerShell predictor that uses an auto-completer.";

    public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
    {
        var input = context.InputAst.Extent.Text.AsSpan().TrimEnd();

        if (input.Length == 0)
        {
            return default;
        }

        // Skip dynamic arguments (git branches, scoop packages, etc.) for fast predictor responses
        var completions = CommandCompleter.GetCompletions(input, default, includeDynamicArguments: false).ToList();

        if (completions.Count == 0)
        {
            return default;
        }

        var suggestions = new List<PredictiveSuggestion>(completions.Count);

        foreach (var c in completions)
        {
            suggestions.Add(new PredictiveSuggestion(Combine(input, c.CompletionText), c.Tooltip));
        }

        return new SuggestionPackage(suggestions);
    }

    private static string Combine(ReadOnlySpan<char> input, string completionText)
    {
        // find overlap between the end of 'input' and the start of 'completionText' and "fold" them together
        // combine them like this:
        // "sco" + "scoop" => "scoop"
        // "scoop" + "alias" => "scoop alias"
        // "scoop al" + "alias" => "scoop alias"
        // "scoop update" + "*" => "scoop update *"
        // e.g. "scoop al" + "alias" => "scoop alias"

        for (int i = 0; i < input.Length; i++)
        {
            var substring = input[i..];

            if (completionText.AsSpan().StartsWith(substring, StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(input[..i], completionText);
            }
        }

        return $"{input} {completionText}";
    }
}
