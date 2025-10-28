using System.Management.Automation.Subsystem.Prediction;
using PSCue.Shared;
using PSCue.Shared.Completions;

namespace PSCue.Module;

/// <summary>
/// Phase 11: Hybrid predictor that combines known completions with generic learning.
/// - For supported commands: Blends explicit completions with learned patterns
/// - For unsupported commands: Uses generic learning only
/// </summary>
public class CommandPredictor : ICommandPredictor
{
    private readonly GenericPredictor? _genericPredictor;
    private readonly bool _enableGenericLearning;

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
    public string Description => "A PowerShell predictor that combines explicit completions with generic learning.";

    /// <summary>
    /// Creates a new CommandPredictor.
    /// </summary>
    /// <param name="genericPredictor">Optional generic predictor for learning-based suggestions.</param>
    /// <param name="enableGenericLearning">Whether to enable generic learning (default: true).</param>
    public CommandPredictor(GenericPredictor? genericPredictor = null, bool enableGenericLearning = true)
    {
        _genericPredictor = genericPredictor;
        _enableGenericLearning = enableGenericLearning && genericPredictor != null;
    }

    public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
    {
        var input = context.InputAst.Extent.Text.AsSpan().TrimEnd();

        if (input.Length == 0)
        {
            return default;
        }

        var inputString = input.ToString();
        var command = ExtractCommand(inputString);

        // Get known completions (for supported commands)
        // Skip dynamic arguments (git branches, scoop packages, etc.) for fast predictor responses
        var knownCompletions = CommandCompleter.GetCompletions(input, default, includeDynamicArguments: false).ToList();

        // Get generic learned suggestions if enabled
        List<PredictionSuggestion>? genericSuggestions = null;
        if (_enableGenericLearning && _genericPredictor != null)
        {
            try
            {
                genericSuggestions = _genericPredictor.GetSuggestions(inputString, maxResults: 10);
            }
            catch
            {
                // Silently ignore errors in generic predictor
            }
        }

        // Merge suggestions
        var mergedSuggestions = MergeSuggestions(input, knownCompletions, genericSuggestions);

        if (mergedSuggestions.Count == 0)
        {
            return default;
        }

        return new SuggestionPackage(mergedSuggestions);
    }

    /// <summary>
    /// Merges known completions with generic learned suggestions.
    /// Deduplicates and ranks by score.
    /// </summary>
    private List<PredictiveSuggestion> MergeSuggestions(
        ReadOnlySpan<char> input,
        List<ICompletion> knownCompletions,
        List<PredictionSuggestion>? genericSuggestions)
    {
        var suggestions = new Dictionary<string, (string fullText, string? tooltip, double score)>(StringComparer.OrdinalIgnoreCase);

        // Add known completions (priority score based on order)
        for (int i = 0; i < knownCompletions.Count; i++)
        {
            var c = knownCompletions[i];
            var fullText = Combine(input, c.CompletionText);

            // Score: higher for earlier items (assuming they're pre-sorted by relevance)
            var score = 1.0 - (i * 0.05); // First item = 1.0, second = 0.95, etc.

            suggestions[c.CompletionText] = (fullText, c.Tooltip, score);
        }

        // Merge in generic suggestions
        if (genericSuggestions != null)
        {
            foreach (var g in genericSuggestions)
            {
                var fullText = Combine(input, g.Text);

                if (suggestions.TryGetValue(g.Text, out var existing))
                {
                    // Already exists from known completions - boost the score
                    var boostedScore = Math.Max(existing.score, g.Score) * 1.2;
                    boostedScore = Math.Min(1.0, boostedScore);

                    // Keep better tooltip
                    var tooltip = !string.IsNullOrEmpty(existing.tooltip) ? existing.tooltip : g.Description;

                    suggestions[g.Text] = (fullText, tooltip, boostedScore);
                }
                else
                {
                    // New suggestion from generic learning
                    suggestions[g.Text] = (fullText, g.Description, g.Score);
                }
            }
        }

        // Sort by score and return
        return suggestions.Values
            .OrderByDescending(s => s.score)
            .Take(10) // Top 10 suggestions
            .Select(s => new PredictiveSuggestion(s.fullText, s.tooltip))
            .ToList();
    }

    /// <summary>
    /// Extracts the command name from input (e.g., "git" from "git commit").
    /// </summary>
    private static string ExtractCommand(string input)
    {
        var firstSpace = input.IndexOf(' ');
        return firstSpace > 0 ? input.Substring(0, firstSpace) : input;
    }

    internal static string Combine(ReadOnlySpan<char> input, string completionText)
    {
        // find overlap between the end of 'input' and the start of 'completionText' and "fold" them together
        // combine them like this:
        // "sco" + "scoop" => "scoop"
        // "scoop" + "alias" => "scoop alias"
        // "scoop al" + "alias" => "scoop alias"
        // "scoop update" + "*" => "scoop update *"
        // e.g. "scoop al" + "alias" => "scoop alias"

        // Find the last word boundary (space) in input
        var lastSpaceIndex = input.LastIndexOf(' ');

        // Only look for overlaps starting from the last word (after the last space)
        // This prevents false matches like "claude plugin" + "install" => "claude pluginstall"
        var startIndex = lastSpaceIndex >= 0 ? lastSpaceIndex + 1 : 0;

        // Only match if the completionText starts with the ENTIRE last word
        // Don't match partial words like "in" from "plugin"
        var lastWord = input[startIndex..];
        if (completionText.AsSpan().StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(input[..startIndex], completionText);
        }

        return $"{input} {completionText}";
    }
}
