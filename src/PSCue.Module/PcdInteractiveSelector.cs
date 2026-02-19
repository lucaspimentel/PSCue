using Spectre.Console;
using System.Diagnostics.CodeAnalysis;

namespace PSCue.Module;

public class PcdInteractiveSelector
{
    private readonly ArgumentGraph _graph;
    private readonly PcdCompletionEngine _engine;

    // Detect whether the console supports Unicode (UTF-8) output
    private static readonly bool s_supportsUnicode = DetectUnicodeSupport();

    private static bool DetectUnicodeSupport()
    {
        try
        {
            var encoding = Console.OutputEncoding;
            return encoding.CodePage == 65001 // UTF-8
                || encoding is System.Text.UTF8Encoding
                || encoding is System.Text.UnicodeEncoding;
        }
        catch
        {
            return false;
        }
    }

    // Emoji/symbol helpers with ASCII fallbacks
    private static string SymbolFolder => s_supportsUnicode ? "\U0001f4c1" : "#";
    private static string SymbolPrompt => s_supportsUnicode ? "\u2771" : ">";
    private static string SymbolSearch => s_supportsUnicode ? "\U0001f50d" : "?";
    private static string SymbolError => s_supportsUnicode ? "\u2717" : "x";
    private static string SymbolWarning => s_supportsUnicode ? "\u26a0" : "!";
    private static string SymbolBack => s_supportsUnicode ? "\u2190" : "<";
    private static string SymbolDotFilled => s_supportsUnicode ? "\u25cf" : "*";
    private static string SymbolDotEmpty => s_supportsUnicode ? "\u25cb" : "-";
    private static string SymbolSeparator => s_supportsUnicode ? "\u2022" : "|";

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

    public string? ShowSelectionPrompt(string currentDirectory, int maxResults, string filter = "")
    {
        if (string.IsNullOrEmpty(currentDirectory))
        {
            AnsiConsole.MarkupLine($"[red]{SymbolError}[/] [bold red]Error:[/] Current directory is not available.");
            return null;
        }

        // Get learned directories via PcdCompletionEngine
        // Pass empty wordToComplete to get top-scored directories
        var suggestions = _engine.GetSuggestions(string.Empty, currentDirectory, maxResults);

        if (suggestions.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{SymbolWarning}[/] No learned directories yet.");
            AnsiConsole.MarkupLine("[dim]  Use 'pcd <path>' to navigate and build history.[/]");
            return null;
        }

        // Filter to only existing directories, excluding the parent dir shortcut (..) and the current directory
        // The ~ (home) shortcut is kept as it's a useful navigation target
        var normalizedCurrentDir = currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var validSuggestions = suggestions
            .Where(s => s.Path != ".." &&
                        Directory.Exists(s.DisplayPath) &&
                        !s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Equals(normalizedCurrentDir, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Apply path filter if provided
        if (!string.IsNullOrWhiteSpace(filter))
        {
            validSuggestions = validSuggestions
                .Where(s => s.DisplayPath.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (validSuggestions.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(filter))
            {
                AnsiConsole.MarkupLine($"[yellow]{SymbolWarning}[/] No directories matching '{EscapeMarkup(filter)}' in history.");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]{SymbolWarning}[/] No valid directories in history.");
                AnsiConsole.MarkupLine("[dim]  All learned paths have been deleted or moved.[/]");
            }
            return null;
        }

        // Check if we're in a non-interactive terminal
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            AnsiConsole.MarkupLine($"[red]{SymbolError}[/] [bold red]Error:[/] Interactive mode requires a TTY terminal.");
            AnsiConsole.MarkupLine("[dim]  Try running in Windows Terminal or a standard console.[/]");
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
            AnsiConsole.WriteLine();

            var ruleTitle = string.IsNullOrWhiteSpace(filter)
                ? $"[cyan]{SymbolFolder} Navigate to Directory[/]"
                : $"[cyan]{SymbolFolder} Navigate to Directory[/] [dim](filter: {EscapeMarkup(filter)})[/]";
            var rule = new Rule(ruleTitle)
            {
                Style = Style.Parse("cyan dim"),
                Justification = Justify.Left
            };
            AnsiConsole.Write(rule);
            AnsiConsole.WriteLine();

            // Create selection prompt
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<PcdSuggestion>()
                    .Title($"[cyan]{SymbolPrompt}[/] [bold]Select a directory[/]")
                    .PageSize(15)
                    .MoreChoicesText(s_supportsUnicode
                        ? "[grey]\u2193 Move up and down to reveal more directories \u2193[/]"
                        : "[grey]Move up and down to reveal more directories[/]")
                    .AddChoices(choices)
                    .UseConverter(s => s == cancelSentinel ? FormatCancelEntry() : FormatDirectoryEntry(s))
                    .EnableSearch()
                    .SearchPlaceholderText($"{SymbolSearch} Type to search...")
                    .WrapAround()
                    .HighlightStyle(new Style(foreground: Color.Cyan1, background: Color.Grey15, decoration: Decoration.Bold))
            );

            AnsiConsole.WriteLine();

            // Check if user selected cancel
            if (selection == cancelSentinel)
                return null;

            return selection.DisplayPath;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("cannot read") || ex.Message.Contains("redirected"))
        {
            // Spectre.Console can't take over console (e.g., in VSCode integrated terminal)
            AnsiConsole.MarkupLine($"[red]{SymbolError}[/] [bold red]Error:[/] Cannot show interactive prompt in this terminal.");
            AnsiConsole.MarkupLine("[dim]  Try running in Windows Terminal or use regular 'pcd' commands.[/]");
            return null;
        }
    }

    private string FormatCancelEntry()
    {
        return $"[dim]{SymbolBack} Cancel[/]\n  [dim grey]Go back without selecting[/]";
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

        // Shorten path for better display, ensure trailing separator
        var displayPath = ShortenPath(normalizedPath);

        // Ensure trailing separator for consistency (especially important for drive roots like "C:")
        // Exception: "~" should remain without a trailing separator
        if (displayPath != "~" &&
            !displayPath.EndsWith(Path.DirectorySeparatorChar) &&
            !displayPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            displayPath += Path.DirectorySeparatorChar;
        }

        var pathLine = $"[bold white]{EscapeMarkup(displayPath)}[/]";

        // Format the stats with color-coded usage indicators
        var statsLine = "  ";

        // Usage count with visual indicator
        if (usageCount > 0)
        {
            var usageColor = usageCount >= 10 ? "green" : usageCount >= 5 ? "yellow" : "grey";
            statsLine += $"[{usageColor}]{SymbolDotFilled}[/] [dim]{usageCount} visit{(usageCount == 1 ? "" : "s")}[/]";
        }
        else
        {
            statsLine += $"[dim grey]{SymbolDotEmpty} no visits[/]";
        }

        // Last used time
        if (lastUsed != DateTime.MinValue)
        {
            var timeStr = FormatLastUsed(lastUsed);
            var timeColor = GetTimeColor(DateTime.UtcNow - lastUsed);
            statsLine += $" [dim grey]{SymbolSeparator}[/] [dim {timeColor}]{timeStr}[/]";
        }

        return $"{pathLine}\n{statsLine}";
    }

    private string ShortenPath(string path)
    {
        // Replace home directory with ~ for cleaner display
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + path.Substring(home.Length);
        }

        return path;
    }

    private string GetTimeColor(TimeSpan delta)
    {
        if (delta.TotalHours < 1) return "green";
        if (delta.TotalDays < 1) return "yellow";
        if (delta.TotalDays < 7) return "grey";
        return "grey50";
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
