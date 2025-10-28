using System;
using System.Collections.Generic;
using System.Linq;

namespace PSCue.Module;

/// <summary>
/// A suggestion with its score for ranking.
/// </summary>
public class PredictionSuggestion
{
    /// <summary>
    /// The suggestion text (e.g., "-m", "commit", "--message").
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Description/tooltip for the suggestion.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Combined score (0.0-1.0) based on frequency, recency, and context.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Whether this is a flag (starts with -).
    /// </summary>
    public bool IsFlag { get; set; }

    /// <summary>
    /// Source of the suggestion (for debugging).
    /// </summary>
    public string Source { get; set; } = "generic";
}

/// <summary>
/// Generic prediction engine that generates suggestions from learned data.
/// Works for ANY command, even those not explicitly supported by PSCue.
/// </summary>
public class GenericPredictor
{
    private readonly CommandHistory _history;
    private readonly ArgumentGraph _argumentGraph;
    private readonly ContextAnalyzer _contextAnalyzer;

    /// <summary>
    /// Creates a new GenericPredictor.
    /// </summary>
    public GenericPredictor(CommandHistory history, ArgumentGraph argumentGraph, ContextAnalyzer contextAnalyzer)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _argumentGraph = argumentGraph ?? throw new ArgumentNullException(nameof(argumentGraph));
        _contextAnalyzer = contextAnalyzer ?? throw new ArgumentNullException(nameof(contextAnalyzer));
    }

    /// <summary>
    /// Gets generic suggestions for the current command line.
    /// Returns suggestions sorted by score (highest first).
    /// </summary>
    /// <param name="commandLine">The current command line being typed.</param>
    /// <param name="maxResults">Maximum number of suggestions to return (default: 10).</param>
    public List<PredictionSuggestion> GetSuggestions(string commandLine, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return new List<PredictionSuggestion>();

        // Parse command line
        var parts = ParseCommandLine(commandLine);
        if (parts.Count == 0)
            return new List<PredictionSuggestion>();

        var command = parts[0];
        var arguments = parts.Skip(1).ToList();
        var wordToComplete = arguments.Count > 0 ? arguments[^1] : string.Empty;

        // Check if this is a navigation command
        var isNavigationCommand = command.Equals("cd", StringComparison.OrdinalIgnoreCase)
            || command.Equals("Set-Location", StringComparison.OrdinalIgnoreCase)
            || command.Equals("sl", StringComparison.OrdinalIgnoreCase)
            || command.Equals("chdir", StringComparison.OrdinalIgnoreCase);

        if (isNavigationCommand)
        {
            // Get directory suggestions from filesystem with full paths for descriptions
            var directorySuggestions = PSCue.Shared.KnownCompletions.SetLocationCommand.GetDirectorySuggestionsWithPaths(wordToComplete);

            // Get learned directory paths from ArgumentGraph
            var learnedPaths = _argumentGraph.GetSuggestions(command, arguments.ToArray(), maxResults * 2)
                .Where(s => System.IO.Directory.Exists(s.Argument))
                .ToList();

            // Merge suggestions: filesystem dirs + learned paths that still exist
            var navSuggestions = new List<PredictionSuggestion>();

            // Add filesystem directory suggestions (base score based on relevance)
            foreach (var (completionText, fullPath) in directorySuggestions.Take(maxResults))
            {
                navSuggestions.Add(new PredictionSuggestion
                {
                    Text = completionText,
                    Description = $"Directory: {fullPath}",
                    Score = 0.6, // Base score for filesystem suggestions
                    IsFlag = false,
                    Source = "filesystem"
                });
            }

            // Add learned paths with boosted scores (frequency + recency)
            foreach (var learned in learnedPaths)
            {
                var existing = navSuggestions.FirstOrDefault(s => s.Text.Equals(learned.Argument, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    // Boost score for frequently visited directories
                    var learnedScore = learned.GetScore(_argumentGraph.GetCommandKnowledge(command)?.TotalUsageCount ?? 1);
                    existing.Score = Math.Max(existing.Score, 0.7) + (learnedScore * 0.3); // Boost to 0.7-1.0 range
                    existing.Score = Math.Min(1.0, existing.Score);
                    existing.Description = $"visited {learned.UsageCount}x";
                    existing.Source = "learned+filesystem";
                }
                else
                {
                    // Add learned path not in filesystem suggestions
                    navSuggestions.Add(new PredictionSuggestion
                    {
                        Text = learned.Argument,
                        Description = $"visited {learned.UsageCount}x",
                        Score = 0.8 + (learned.GetScore(_argumentGraph.GetCommandKnowledge(command)?.TotalUsageCount ?? 1) * 0.2),
                        IsFlag = false,
                        Source = "learned"
                    });
                }
            }

            // Sort by score and return
            return navSuggestions
                .OrderByDescending(s => s.Score)
                .Take(maxResults)
                .ToList();
        }

        // Get context from recent history
        var context = _contextAnalyzer.AnalyzeContext(_history, command);

        // Get learned suggestions from argument graph
        var learnedSuggestions = _argumentGraph.GetSuggestions(command, arguments.ToArray(), maxResults * 2);

        // Convert to prediction suggestions and apply context boosts
        var suggestions = learnedSuggestions.Select(stats =>
        {
            var suggestion = new PredictionSuggestion
            {
                Text = stats.Argument,
                Description = BuildDescription(stats, command),
                Score = stats.GetScore(_argumentGraph.GetCommandKnowledge(command)?.TotalUsageCount ?? 1),
                IsFlag = stats.IsFlag,
                Source = "learned"
            };

            // Apply context boost if available
            if (context.ContextBoosts.TryGetValue(stats.Argument, out var boost))
            {
                suggestion.Score *= boost;
                suggestion.Score = Math.Min(1.0, suggestion.Score); // Cap at 1.0
            }

            return suggestion;
        }).ToList();

        // Add context-based suggestions (e.g., next command in sequence)
        AddContextSuggestions(command, arguments, context, suggestions);

        // Sort by score and return top N
        return suggestions
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Text.StartsWith("-")) // Flags first within same score
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Parses command line into tokens.
    /// Simple whitespace split - doesn't handle quotes perfectly, but good enough.
    /// </summary>
    private static List<string> ParseCommandLine(string commandLine)
    {
        return commandLine
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Builds a description for a suggestion based on usage stats.
    /// </summary>
    private string BuildDescription(ArgumentStats stats, string command)
    {
        var parts = new List<string>();

        // Usage count
        parts.Add($"used {stats.UsageCount}x");

        // Recency
        var age = DateTime.UtcNow - stats.LastUsed;
        if (age.TotalHours < 1)
        {
            parts.Add("just now");
        }
        else if (age.TotalDays < 1)
        {
            parts.Add($"{(int)age.TotalHours}h ago");
        }
        else if (age.TotalDays < 7)
        {
            parts.Add($"{(int)age.TotalDays}d ago");
        }

        // Co-occurrence hints (top co-occurring arg)
        var topCoOccurrence = stats.CoOccurrences
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(topCoOccurrence.Key) && topCoOccurrence.Value >= 2)
        {
            parts.Add($"often with {topCoOccurrence.Key}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Adds context-based suggestions (e.g., next command in workflow).
    /// </summary>
    private void AddContextSuggestions(string command, List<string> arguments, CommandContext context, List<PredictionSuggestion> suggestions)
    {
        // If this is a new command (no args yet), suggest subcommands based on sequences
        if (arguments.Count == 0 && context.SuggestedNextCommands.Count > 0)
        {
            foreach (var nextCmd in context.SuggestedNextCommands.Take(3))
            {
                // Check if this is a subcommand suggestion (e.g., "commit" for "git")
                if (nextCmd.StartsWith(command, StringComparison.OrdinalIgnoreCase))
                {
                    var subcommand = nextCmd.Substring(command.Length).Trim();
                    if (!string.IsNullOrEmpty(subcommand) && !subcommand.Contains(' '))
                    {
                        // Don't add if already in suggestions
                        if (!suggestions.Any(s => s.Text.Equals(subcommand, StringComparison.OrdinalIgnoreCase)))
                        {
                            suggestions.Add(new PredictionSuggestion
                            {
                                Text = subcommand,
                                Description = "common next step",
                                Score = 0.85, // High score for context suggestions
                                IsFlag = false,
                                Source = "context"
                            });
                        }
                    }
                }
            }
        }

        // Boost suggestions that match detected sequences
        foreach (var suggestion in suggestions)
        {
            if (context.DetectedSequences.Any(seq => seq.Contains(suggestion.Text, StringComparison.OrdinalIgnoreCase)))
            {
                suggestion.Score *= 1.2;
                suggestion.Score = Math.Min(1.0, suggestion.Score);
            }
        }
    }

    /// <summary>
    /// Gets statistics about what the predictor has learned.
    /// </summary>
    public GenericPredictorStatistics GetStatistics()
    {
        var historyStats = _history.GetStatistics();
        var graphStats = _argumentGraph.GetStatistics();

        return new GenericPredictorStatistics
        {
            TotalCommandsTracked = historyStats.TotalCommands,
            UniqueCommandsLearned = graphStats.TotalCommands,
            TotalArgumentsLearned = graphStats.TotalArguments,
            SuccessRate = historyStats.SuccessRate,
            MostCommonCommand = historyStats.MostCommonCommand ?? graphStats.MostUsedCommand,
            MostCommonCommandCount = Math.Max(historyStats.MostCommonCommandCount, graphStats.MostUsedCommandCount)
        };
    }
}

/// <summary>
/// Statistics about generic predictor learning.
/// </summary>
public class GenericPredictorStatistics
{
    public int TotalCommandsTracked { get; set; }
    public int UniqueCommandsLearned { get; set; }
    public int TotalArgumentsLearned { get; set; }
    public double SuccessRate { get; set; }
    public string? MostCommonCommand { get; set; }
    public int MostCommonCommandCount { get; set; }
}
