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
    private static string SymbolError => s_supportsUnicode ? "\u2717" : "x";
    private static string SymbolWarning => s_supportsUnicode ? "\u26a0" : "!";
    private static string SymbolDotFilled => s_supportsUnicode ? "\u25cf" : "*";
    private static string SymbolDotEmpty => s_supportsUnicode ? "\u25cb" : "-";
    private static string SymbolSeparator => s_supportsUnicode ? "\u2022" : "|";

    // ANSI escape codes for colored console output
    private const string Reset = "\e[0m";
    private const string Red = "\e[31m";
    private const string BoldRed = "\e[1;31m";
    private const string Yellow = "\e[33m";
    private const string Green = "\e[32m";
    private const string Grey = "\e[90m";
    private const string Grey50 = "\e[37m";
    private const string Dim = "\e[2m";

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
            WriteError("Current directory is not available.");
            return null;
        }

        // Get learned directories via PcdCompletionEngine
        // Pass empty wordToComplete to get top-scored directories
        var suggestions = _engine.GetSuggestions(string.Empty, currentDirectory, maxResults);

        if (suggestions.Count == 0)
        {
            WriteWarning("No learned directories yet.");
            Console.WriteLine($"{Dim}  Use 'pcd <path>' to navigate and build history.{Reset}");
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

        if (validSuggestions.Count == 0)
        {
            WriteWarning("No valid directories in history.");
            Console.WriteLine($"{Dim}  All learned paths have been deleted or moved.{Reset}");
            return null;
        }

        // Check if we're in a non-interactive terminal
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            WriteError("Interactive mode requires a TTY terminal.");
            Console.WriteLine($"{Dim}  Try running in Windows Terminal or a standard console.{Reset}");
            return null;
        }

        try
        {
            Console.WriteLine();

            var menu = new ConsoleMenu(
                formatPath: FormatDirectoryPath,
                formatStats: FormatDirectoryStats,
                title: $"{SymbolFolder} Navigate to Directory",
                supportsUnicode: s_supportsUnicode,
                initialQuery: filter ?? "");

            var selection = menu.Show(validSuggestions);

            Console.WriteLine();

            return selection?.DisplayPath;
        }
        catch (InvalidOperationException)
        {
            WriteError("Cannot show interactive prompt in this terminal.");
            Console.WriteLine($"{Dim}  Try running in Windows Terminal or use regular 'pcd' commands.{Reset}");
            return null;
        }
    }

    private string FormatDirectoryPath(PcdSuggestion suggestion)
    {
        var normalizedPath = suggestion.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var displayPath = ShortenPath(normalizedPath);

        // Ensure trailing separator for consistency (especially important for drive roots like "C:")
        // Exception: "~" should remain without a trailing separator
        if (displayPath != "~" &&
            !displayPath.EndsWith(Path.DirectorySeparatorChar) &&
            !displayPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            displayPath += Path.DirectorySeparatorChar;
        }

        return displayPath;
    }

    private string FormatDirectoryStats(PcdSuggestion suggestion)
    {
        var normalizedPath = suggestion.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var commandKnowledge = _graph.GetCommandKnowledge("cd");

        int usageCount = 0;
        DateTime lastUsed = DateTime.MinValue;

        if (commandKnowledge != null)
        {
            var argStats = commandKnowledge.Arguments.Values
                .FirstOrDefault(a => a.Argument.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (argStats != null)
            {
                usageCount = argStats.UsageCount;
                lastUsed = argStats.LastUsed;
            }
        }

        var parts = new System.Text.StringBuilder();

        // Usage count with color-coded indicator
        if (usageCount > 0)
        {
            var usageColor = usageCount >= 10 ? Green : usageCount >= 5 ? Yellow : Grey;
            parts.Append($"{usageColor}{SymbolDotFilled}{Reset} {Grey}{usageCount} visit{(usageCount == 1 ? "" : "s")}{Reset}");
        }
        else
        {
            parts.Append($"{Grey}{SymbolDotEmpty} no visits{Reset}");
        }

        // Last used time
        if (lastUsed != DateTime.MinValue)
        {
            var timeStr = FormatLastUsed(lastUsed);
            var timeColor = GetTimeColor(DateTime.UtcNow - lastUsed);
            parts.Append($" {Grey}{SymbolSeparator}{Reset} {timeColor}{timeStr}{Reset}");
        }

        return parts.ToString();
    }

    private string ShortenPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + path.Substring(home.Length);
        }

        return path;
    }

    private string GetTimeColor(TimeSpan delta)
    {
        if (delta.TotalHours < 1) return Green;
        if (delta.TotalDays < 1) return Yellow;
        if (delta.TotalDays < 7) return Grey;
        return Grey50;
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

    private static void WriteError(string message)
    {
        Console.WriteLine($"{Red}{SymbolError}{Reset} {BoldRed}Error:{Reset} {message}");
    }

    private static void WriteWarning(string message)
    {
        Console.WriteLine($"{Yellow}{SymbolWarning}{Reset} {message}");
    }
}
