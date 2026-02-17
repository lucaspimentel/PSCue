using Spectre.Console;
using System.Diagnostics.CodeAnalysis;

namespace PSCue.Module;

public class PcdInteractiveSelector
{
    private readonly ArgumentGraph _graph;
    private readonly PcdCompletionEngine _engine;

    public PcdInteractiveSelector(ArgumentGraph graph)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));

        _engine = new PcdCompletionEngine(
            graph,
            PcdConfiguration.ScoreDecayDays,
            PcdConfiguration.FrequencyWeight,
            PcdConfiguration.RecencyWeight,
            PcdConfiguration.DistanceWeight,
            PcdConfiguration.TabCompletionMaxDepth,
            PcdConfiguration.EnableRecursiveSearch,
            PcdConfiguration.ExactMatchBoost,
            PcdConfiguration.FuzzyMinMatchPercentage
        );
    }

    public string? ShowSelectionPrompt(string currentDirectory, int maxResults)
    {
        if (string.IsNullOrEmpty(currentDirectory))
        {
            AnsiConsole.MarkupLine("[red]Error: Current directory is not available.[/]");
            return null;
        }

        // Get learned directories via PcdCompletionEngine
        // Pass empty wordToComplete to get top-scored directories
        var suggestions = _engine.GetSuggestions(string.Empty, currentDirectory, maxResults);

        if (suggestions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No learned directories yet.[/]");
            AnsiConsole.MarkupLine("[dim]Use 'pcd <path>' to navigate and build history.[/]");
            return null;
        }

        // Filter to only existing directories
        var validSuggestions = suggestions
            .Where(s => Directory.Exists(s.DisplayPath))
            .ToList();

        if (validSuggestions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No valid directories in history.[/]");
            AnsiConsole.MarkupLine("[dim]All learned paths have been deleted or moved.[/]");
            return null;
        }

        // Check if we're in a non-interactive terminal
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            AnsiConsole.MarkupLine("[red]Error: Interactive mode requires a TTY terminal.[/]");
            AnsiConsole.MarkupLine("[dim]Try running in Windows Terminal or a standard console.[/]");
            return null;
        }

        // Create cancel sentinel as the first choice
        var cancelSentinel = new PcdSuggestion
        {
            Path = string.Empty,
            DisplayPath = string.Empty,
            Score = 0,
            UsageCount = 0,
            LastUsed = DateTime.MinValue,
            MatchType = MatchType.WellKnown,
            Tooltip = "Cancel selection"
        };

        // Add cancel sentinel as first choice, followed by valid directories
        var choices = new List<PcdSuggestion> { cancelSentinel };
        choices.AddRange(validSuggestions);

        try
        {
            // Create selection prompt
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<PcdSuggestion>()
                    .Title("[green]Select a directory[/] [dim](choose Cancel to go back)[/]:")
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up and down to reveal more directories)[/]")
                    .AddChoices(choices)
                    .UseConverter(s => s == cancelSentinel ? "[dim]< Cancel >[/]" : FormatDirectoryEntry(s))
                    .EnableSearch()
                    .SearchPlaceholderText("Type to search...")
                    .WrapAround()
                    .HighlightStyle(new Style(foreground: Color.Green))
            );

            // Check if user selected cancel
            if (selection == cancelSentinel)
                return null;

            return selection.DisplayPath;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("cannot read") || ex.Message.Contains("redirected"))
        {
            // Spectre.Console can't take over console (e.g., in VSCode integrated terminal)
            AnsiConsole.MarkupLine("[red]Error: Cannot show interactive prompt in this terminal.[/]");
            AnsiConsole.MarkupLine("[dim]Try running in Windows Terminal or use regular 'pcd' commands.[/]");
            return null;
        }
    }

    private string FormatDirectoryEntry(PcdSuggestion suggestion)
    {
        // Get usage statistics from ArgumentGraph
        var normalizedPath = suggestion.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var commandKnowledge = _graph.GetCommandKnowledge("cd");

        int usageCount = 0;
        DateTime lastUsed = DateTime.MinValue;

        if (commandKnowledge != null)
        {
            // Try to find the argument stats for this directory path
            var argStats = commandKnowledge.Arguments.Values
                .FirstOrDefault(a => a.Argument.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (argStats != null)
            {
                usageCount = argStats.UsageCount;
                lastUsed = argStats.LastUsed;
            }
        }

        // Format the entry with path on first line, stats on second line
        var pathLine = EscapeMarkup(normalizedPath);
        var statsLine = $"  [dim]{usageCount} visit{(usageCount == 1 ? "" : "s")}";

        if (lastUsed != DateTime.MinValue)
        {
            statsLine += $" · last used {FormatLastUsed(lastUsed)}";
        }

        statsLine += "[/]";

        return $"{pathLine}\n{statsLine}";
    }

    private string FormatLastUsed(DateTime lastUsed)
    {
        var delta = DateTime.UtcNow - lastUsed;

        if (delta.TotalMinutes < 1)
            return "just now";
        if (delta.TotalMinutes < 60)
            return $"{(int)delta.TotalMinutes} minute{((int)delta.TotalMinutes == 1 ? "" : "s")} ago";
        if (delta.TotalHours < 24)
            return $"{(int)delta.TotalHours} hour{((int)delta.TotalHours == 1 ? "" : "s")} ago";
        if (delta.TotalDays < 7)
            return $"{(int)delta.TotalDays} day{((int)delta.TotalDays == 1 ? "" : "s")} ago";
        if (delta.TotalDays < 30)
            return $"{(int)(delta.TotalDays / 7)} week{((int)(delta.TotalDays / 7) == 1 ? "" : "s")} ago";
        if (delta.TotalDays < 365)
            return $"{(int)(delta.TotalDays / 30)} month{((int)(delta.TotalDays / 30) == 1 ? "" : "s")} ago";

        return $"{(int)(delta.TotalDays / 365)} year{((int)(delta.TotalDays / 365) == 1 ? "" : "s")} ago";
    }

    private static string EscapeMarkup(string text)
    {
        // Escape Spectre.Console markup characters
        return text
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}
