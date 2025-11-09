using System.Management.Automation.Subsystem.Prediction;
using PSCue.Shared;
using PSCue.Shared.Completions;

namespace PSCue.Module;

/// <summary>
/// Hybrid predictor that combines known completions with generic learning.
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
            // Empty input - show workflow predictions for next command
            return GetWorkflowPredictions();
        }

        var inputString = input.ToString();
        var command = ExtractCommand(inputString);

        // Special handling for 'pcd' command - use PcdCompletionEngine
        if (command.Equals("pcd", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("Invoke-PCD", StringComparison.OrdinalIgnoreCase))
        {
            return GetPcdSuggestions(inputString);
        }

        // Check if user is starting to type a new command (no arguments yet)
        // If so, include workflow predictions in suggestions
        var hasArguments = inputString.Contains(' ');
        List<WorkflowSuggestion>? workflowSuggestions = null;

        if (!hasArguments && _enableGenericLearning)
        {
            workflowSuggestions = GetWorkflowSuggestionsForCommand(inputString);
        }

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

        // Merge suggestions (including workflow predictions)
        var mergedSuggestions = MergeSuggestions(input, knownCompletions, genericSuggestions, workflowSuggestions);

        if (mergedSuggestions.Count == 0)
        {
            return default;
        }

        return new SuggestionPackage(mergedSuggestions);
    }

    /// <summary>
    /// Gets directory suggestions for the pcd command using PcdCompletionEngine.
    /// </summary>
    private SuggestionPackage GetPcdSuggestions(string inputString)
    {
        try
        {
            var knowledgeGraph = PSCueModule.KnowledgeGraph;
            if (knowledgeGraph == null)
            {
                return default;
            }

            // Extract the partial path after "pcd "
            var pcdPrefix = inputString.StartsWith("Invoke-PCD", StringComparison.OrdinalIgnoreCase) ? "Invoke-PCD" : "pcd";
            var wordToComplete = string.Empty;

            if (inputString.Length > pcdPrefix.Length)
            {
                // Get everything after "pcd " or "Invoke-PCD "
                wordToComplete = inputString.Substring(pcdPrefix.Length).TrimStart();
            }

            // Get current directory
            var currentDir = Environment.CurrentDirectory;

            // Read configuration from environment variables
            var frequencyWeight = GetEnvDouble("PSCUE_PCD_FREQUENCY_WEIGHT", 0.5);
            var recencyWeight = GetEnvDouble("PSCUE_PCD_RECENCY_WEIGHT", 0.3);
            var distanceWeight = GetEnvDouble("PSCUE_PCD_DISTANCE_WEIGHT", 0.2);
            var maxDepth = GetEnvInt("PSCUE_PCD_MAX_DEPTH", 3);
            var enableRecursive = GetEnvBool("PSCUE_PCD_RECURSIVE_SEARCH", false);

            // Create PcdCompletionEngine
            var engine = new PcdCompletionEngine(
                knowledgeGraph,
                30, // scoreDecayDays
                frequencyWeight,
                recencyWeight,
                distanceWeight,
                maxDepth,
                enableRecursive
            );

            // Get suggestions (allow multiple for predictor)
            var suggestions = engine.GetSuggestions(wordToComplete, currentDir, maxResults: 5);

            if (suggestions.Count == 0)
            {
                return default;
            }

            // Convert to PredictiveSuggestion objects
            var predictiveSuggestions = suggestions.Select(s =>
            {
                var fullText = pcdPrefix + " " + s.Path;
                return new PredictiveSuggestion(fullText, s.Tooltip);
            }).ToList();

            return new SuggestionPackage(predictiveSuggestions);
        }
        catch
        {
            // Silently ignore errors
            return default;
        }
    }

    /// <summary>
    /// Helper to read double from environment variable.
    /// </summary>
    private double GetEnvDouble(string key, double defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return double.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Helper to read int from environment variable.
    /// </summary>
    private int GetEnvInt(string key, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Helper to read bool from environment variable.
    /// </summary>
    private bool GetEnvBool(string key, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? defaultValue;
    }

    /// <summary>
    /// Gets workflow predictions for the next command based on command history.
    /// </summary>
    private SuggestionPackage GetWorkflowPredictions()
    {
        if (!_enableGenericLearning)
        {
            return default;
        }

        try
        {
            var workflowLearner = PSCueModule.WorkflowLearner;
            var commandHistory = PSCueModule.CommandHistory;

            if (workflowLearner == null || commandHistory == null)
            {
                return default;
            }

            // Get the most recent command
            var recentCommands = commandHistory.GetRecent(1);
            if (recentCommands.Count == 0)
            {
                return default;
            }

            var lastCommand = recentCommands[0];
            var timeSinceLastCommand = DateTime.UtcNow - lastCommand.Timestamp;

            // Get workflow predictions
            var predictions = workflowLearner.GetNextCommandPredictions(
                lastCommand.CommandLine,
                timeSinceLastCommand,
                maxResults: 5
            );

            if (predictions.Count == 0)
            {
                return default;
            }

            // Convert to PredictiveSuggestions
            var suggestions = predictions
                .Select(p => new PredictiveSuggestion(p.Command, p.Reason))
                .ToList();

            return new SuggestionPackage(suggestions);
        }
        catch
        {
            // Silently ignore errors
            return default;
        }
    }

    /// <summary>
    /// Gets workflow suggestions that match the partial command being typed.
    /// </summary>
    private List<WorkflowSuggestion>? GetWorkflowSuggestionsForCommand(string partialCommand)
    {
        try
        {
            var workflowLearner = PSCueModule.WorkflowLearner;
            var commandHistory = PSCueModule.CommandHistory;

            if (workflowLearner == null || commandHistory == null)
            {
                return null;
            }

            // Get the most recent command
            var recentCommands = commandHistory.GetRecent(1);
            if (recentCommands.Count == 0)
            {
                return null;
            }

            var lastCommand = recentCommands[0];
            var timeSinceLastCommand = DateTime.UtcNow - lastCommand.Timestamp;

            // Get workflow predictions
            var predictions = workflowLearner.GetNextCommandPredictions(
                lastCommand.CommandLine,
                timeSinceLastCommand,
                maxResults: 5
            );

            // Filter predictions that start with the partial command
            return predictions
                .Where(p => p.Command.StartsWith(partialCommand, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch
        {
            // Silently ignore errors
            return null;
        }
    }

    /// <summary>
    /// Merges known completions with generic learned suggestions and workflow predictions.
    /// Deduplicates and ranks by score.
    ///
    /// Blending strategy:
    /// - Known completions: Scored 1.0 to 0.5 based on order
    /// - Generic suggestions: Scored by frequency (0.6) + recency (0.4)
    /// - ML sequence predictions: Scored by n-gram probability + recency (via GenericPredictor)
    /// - Workflow predictions: Scored by confidence (learned patterns)
    /// - Overlap boost: If suggestion appears in both, boost score by 1.2×
    ///
    /// Final score = (0.25 × frequency/recency) + (0.25 × n-gram) + (0.25 × workflow) + (0.25 × known completions)
    /// </summary>
    private List<PredictiveSuggestion> MergeSuggestions(
        ReadOnlySpan<char> input,
        List<ICompletion> knownCompletions,
        List<PredictionSuggestion>? genericSuggestions,
        List<WorkflowSuggestion>? workflowSuggestions = null)
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

        // Merge in generic suggestions (includes ML sequence predictions)
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
                    // New suggestion from generic learning (including ML sequences)
                    suggestions[g.Text] = (fullText, g.Description, g.Score);
                }
            }
        }

        // Merge in workflow predictions
        if (workflowSuggestions != null)
        {
            foreach (var w in workflowSuggestions)
            {
                var fullText = Combine(input, w.Command);
                var tooltip = $"Workflow: {w.Reason}";

                if (suggestions.TryGetValue(w.Command, out var existing))
                {
                    // Already exists - boost the score if workflow confidence is high
                    var boostedScore = Math.Max(existing.score, w.Confidence) * 1.3;
                    boostedScore = Math.Min(1.0, boostedScore);

                    // Combine tooltips
                    var combinedTooltip = !string.IsNullOrEmpty(existing.tooltip)
                        ? $"{existing.tooltip} | {tooltip}"
                        : tooltip;

                    suggestions[w.Command] = (fullText, combinedTooltip, boostedScore);
                }
                else
                {
                    // New suggestion from workflow learning
                    suggestions[w.Command] = (fullText, tooltip, w.Confidence);
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
        // "git che" + "checkout master" => "git checkout master" (multi-word completion)
        // "cd dotnet" + "D:\path\dotnet\" => "cd D:\path\dotnet\" (navigation paths replace last word)

        // Find the last word boundary (space) in input
        var lastSpaceIndex = input.LastIndexOf(' ');

        // Only look for overlaps starting from the last word (after the last space)
        // This prevents false matches like "claude plugin" + "install" => "claude pluginstall"
        var startIndex = lastSpaceIndex >= 0 ? lastSpaceIndex + 1 : 0;

        // Get the last word from input (the word being completed)
        var lastWord = input[startIndex..];

        // Check if completionText is a multi-word completion (contains spaces)
        // If so, check if its first word matches the partial word being completed
        if (completionText.Contains(' '))
        {
            var firstCompletionWord = completionText.AsSpan(0, completionText.IndexOf(' '));
            if (firstCompletionWord.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
            {
                // Replace the partial word with the full multi-word completion
                // "git che" + "checkout master" => "git checkout master"
                return string.Concat(input[..startIndex], completionText);
            }
        }

        // Only match if the completionText starts with the ENTIRE last word
        // Don't match partial words like "in" from "plugin"
        if (completionText.AsSpan().StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(input[..startIndex], completionText);
        }

        // Special case: If completionText looks like an absolute path and we have a command + word,
        // replace the last word instead of appending (for navigation commands like cd)
        if (lastSpaceIndex >= 0 && IsAbsolutePath(completionText))
        {
            // Replace the last word with the absolute path
            // "cd dotnet" + "D:\path\" => "cd D:\path\"
            return string.Concat(input[..startIndex], completionText);
        }

        return $"{input} {completionText}";
    }

    /// <summary>
    /// Checks if a string looks like an absolute path.
    /// </summary>
    private static bool IsAbsolutePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 2)
            return false;

        // Windows: C:\, D:\, \\server\share
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
            return true;

        if (path.StartsWith("\\\\"))
            return true;

        // Unix: /home, /var
        if (path[0] == '/')
            return true;

        return false;
    }
}
