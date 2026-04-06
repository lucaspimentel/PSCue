namespace PSCue.Module;

/// <summary>
/// fzf-style interactive console menu for PCD directory selection.
/// Typing live-filters results using PcdSubsequenceScorer, arrow keys navigate,
/// Enter selects, Escape cancels.
/// </summary>
internal sealed class ConsoleMenu
{
    private const int PageSize = 15;

    // ANSI escape codes
    private const string Reset = "\e[0m";
    private const string Bold = "\e[1m";
    private const string Dim = "\e[2m";
    private const string Cyan = "\e[36m";
    private const string Red = "\e[31m";
    private const string Green = "\e[32m";
    private const string Yellow = "\e[33m";
    private const string White = "\e[37m";
    private const string Grey = "\e[90m";
    private const string BoldCyan = "\e[1;36m";
    private const string BoldWhite = "\e[1;37m";
    private const string BoldGreen = "\e[1;32m";
    private const string ClearToEndOfLine = "\e[K";
    private const string HideCursor = "\e[?25l";
    private const string ShowCursor = "\e[?25h";

    private string _query;
    private int _selectedIndex;
    private int _scrollOffset;
    private int _lastRenderedLineCount;

    private readonly Func<PcdSuggestion, string> _formatPath;
    private readonly Func<PcdSuggestion, string> _formatStats;
    private readonly string _title;
    private readonly bool _supportsUnicode;

    private string SymbolPointer => _supportsUnicode ? "\u276f" : ">";
    private string SymbolRule => _supportsUnicode ? "\u2500" : "-";

    public ConsoleMenu(
        Func<PcdSuggestion, string> formatPath,
        Func<PcdSuggestion, string> formatStats,
        string title,
        bool supportsUnicode,
        string initialQuery = "")
    {
        _formatPath = formatPath;
        _formatStats = formatStats;
        _title = title;
        _supportsUnicode = supportsUnicode;
        _query = initialQuery;
    }

    public PcdSuggestion? Show(IReadOnlyList<PcdSuggestion> allItems)
    {
        var filtered = Filter(allItems);

        Console.Write(HideCursor);
        try
        {
            Render(filtered, allItems.Count, isInitial: true);

            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                switch (key.Key)
                {
                    case ConsoleKey.Escape:
                        return null;

                    case ConsoleKey.Enter:
                        if (filtered.Count > 0 && _selectedIndex < filtered.Count)
                            return filtered[_selectedIndex].Suggestion;
                        return null;

                    case ConsoleKey.UpArrow:
                        if (filtered.Count > 0)
                        {
                            _selectedIndex = _selectedIndex > 0 ? _selectedIndex - 1 : filtered.Count - 1;
                            AdjustScroll(filtered.Count);
                        }
                        break;

                    case ConsoleKey.DownArrow:
                        if (filtered.Count > 0)
                        {
                            _selectedIndex = _selectedIndex < filtered.Count - 1 ? _selectedIndex + 1 : 0;
                            AdjustScroll(filtered.Count);
                        }
                        break;

                    case ConsoleKey.PageUp:
                        if (filtered.Count > 0)
                        {
                            _selectedIndex = Math.Max(0, _selectedIndex - PageSize);
                            AdjustScroll(filtered.Count);
                        }
                        break;

                    case ConsoleKey.PageDown:
                        if (filtered.Count > 0)
                        {
                            _selectedIndex = Math.Min(filtered.Count - 1, _selectedIndex + PageSize);
                            AdjustScroll(filtered.Count);
                        }
                        break;

                    case ConsoleKey.Home:
                        _selectedIndex = 0;
                        _scrollOffset = 0;
                        break;

                    case ConsoleKey.End:
                        if (filtered.Count > 0)
                        {
                            _selectedIndex = filtered.Count - 1;
                            AdjustScroll(filtered.Count);
                        }
                        break;

                    case ConsoleKey.Backspace:
                        if (_query.Length > 0)
                        {
                            _query = _query[..^1];
                            filtered = Filter(allItems);
                            _selectedIndex = 0;
                            _scrollOffset = 0;
                        }
                        break;

                    default:
                        if (key.KeyChar >= ' ')
                        {
                            _query += key.KeyChar;
                            filtered = Filter(allItems);
                            _selectedIndex = 0;
                            _scrollOffset = 0;
                        }
                        break;
                }

                Render(filtered, allItems.Count, isInitial: false);
            }
        }
        finally
        {
            Console.Write(ShowCursor);
        }
    }

    private void Render(List<FilteredItem> filtered, int totalCount, bool isInitial)
    {
        // Move cursor back to top of previous render
        if (!isInitial && _lastRenderedLineCount > 0)
        {
            Console.Write($"\e[{_lastRenderedLineCount}A\r");
        }

        int lineCount = 0;

        // Title rule with match count
        var matchInfo = string.IsNullOrEmpty(_query)
            ? $"{totalCount} directories"
            : $"{filtered.Count}/{totalCount}";
        var ruleText = $" {_title} ({matchInfo}) ";
        var ruleWidth = Math.Max(40, Console.WindowWidth - 2);
        var ruleLineLen = (ruleWidth - ruleText.Length) / 2;
        var ruleLine = new string(_supportsUnicode ? '\u2500' : '-', Math.Max(1, ruleLineLen));
        Console.Write($"{Cyan}{ruleLine}{Reset}{BoldCyan}{ruleText}{Reset}{Cyan}{ruleLine}{Reset}{ClearToEndOfLine}\n");
        lineCount++;

        // Search input (ClearToEndOfLine on blank lines to erase stale content from previous renders)
        Console.Write($"{ClearToEndOfLine}\n  {Cyan}>{Reset} {(_query.Length > 0 ? _query : $"{Grey}Type to filter...{Reset}")}{ClearToEndOfLine}\n{ClearToEndOfLine}\n");
        lineCount += 3;

        // Visible items
        int visibleCount = Math.Min(PageSize, filtered.Count);
        int visibleEnd = _scrollOffset + visibleCount;

        for (int i = _scrollOffset; i < visibleEnd; i++)
        {
            var entry = filtered[i];
            bool selected = i == _selectedIndex;

            // Path line with highlighted match positions
            var pointer = selected ? $"{BoldCyan}{SymbolPointer}{Reset} " : "  ";
            var pathText = _formatPath(entry.Suggestion);
            Console.Write($"  {pointer}");
            WriteHighlightedPath(pathText, entry.MatchPositions, selected);
            Console.Write($"{Reset}{ClearToEndOfLine}\n");
            lineCount++;

            // Stats line
            var statsText = _formatStats(entry.Suggestion);
            Console.Write($"      {statsText}{Reset}{ClearToEndOfLine}\n");
            lineCount++;
        }

        // Empty state
        if (filtered.Count == 0)
        {
            Console.Write($"  {Grey}No matches{Reset}{ClearToEndOfLine}\n");
            lineCount++;
        }

        // Footer (ClearToEndOfLine on blank line to erase stale content from previous renders)
        Console.Write($"{ClearToEndOfLine}\n  {Grey}\u2191\u2193 navigate  Enter select  Esc cancel{Reset}{ClearToEndOfLine}\n");
        lineCount += 2;

        // Pad with blank lines if previous render was taller (clear leftover lines)
        for (int i = lineCount; i < _lastRenderedLineCount; i++)
        {
            Console.Write($"{ClearToEndOfLine}\n");
            lineCount++;
        }

        _lastRenderedLineCount = lineCount;
    }

    private static void WriteHighlightedPath(string pathText, int[]? matchPositions, bool selected)
    {
        if (matchPositions == null || matchPositions.Length == 0)
        {
            // No match positions — write the whole path in one style
            Console.Write($"{(selected ? BoldWhite : White)}{pathText}");
            return;
        }

        var baseStyle = selected ? BoldWhite : White;
        var matchStyle = selected ? BoldGreen : Green;
        int mi = 0;

        for (int ci = 0; ci < pathText.Length; ci++)
        {
            bool isMatch = mi < matchPositions.Length && matchPositions[mi] == ci;

            if (isMatch)
            {
                Console.Write($"{matchStyle}{pathText[ci]}");
                mi++;
            }
            else
            {
                Console.Write($"{baseStyle}{pathText[ci]}");
            }
        }
    }

    private void AdjustScroll(int itemCount)
    {
        if (_selectedIndex < _scrollOffset)
        {
            _scrollOffset = _selectedIndex;
        }
        else if (_selectedIndex >= _scrollOffset + PageSize)
        {
            _scrollOffset = _selectedIndex - PageSize + 1;
        }
    }

    private List<FilteredItem> Filter(IReadOnlyList<PcdSuggestion> items)
    {
        if (string.IsNullOrEmpty(_query))
            return items.Select(s => new FilteredItem(s, null)).ToList();

        return items
            .Select(s =>
            {
                // Score against the formatted display path so match positions align with rendered text
                var displayPath = _formatPath(s);
                var trimmed = displayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dirName = Path.GetFileName(trimmed);

                var dirScore = PcdSubsequenceScorer.Score(_query.AsSpan(), dirName.AsSpan(), out var dirPositions);
                var pathScore = PcdSubsequenceScorer.Score(_query.AsSpan(), trimmed.AsSpan(), out var pathPositions);

                if (dirScore > pathScore)
                {
                    // Offset dir-name positions to be relative to the full display path
                    int dirStart = trimmed.Length - dirName.Length;
                    if (dirPositions != null && dirStart > 0)
                    {
                        for (int i = 0; i < dirPositions.Length; i++)
                            dirPositions[i] += dirStart;
                    }
                    return (suggestion: s, score: dirScore, positions: dirPositions);
                }

                return (suggestion: s, score: pathScore, positions: pathPositions);
            })
            .Where(x => x.score > 0.0)
            .OrderByDescending(x => x.score)
            .Select(x => new FilteredItem(x.suggestion, x.positions))
            .ToList();
    }

    private readonly record struct FilteredItem(PcdSuggestion Suggestion, int[]? MatchPositions);
}
