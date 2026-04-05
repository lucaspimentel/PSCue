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
                            return filtered[_selectedIndex];
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

    private void Render(List<PcdSuggestion> filtered, int totalCount, bool isInitial)
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

        // Search input
        Console.Write($"\n  {Cyan}>{Reset} {(_query.Length > 0 ? _query : $"{Grey}Type to filter...{Reset}")}{ClearToEndOfLine}\n\n");
        lineCount += 3;

        // Visible items
        int visibleCount = Math.Min(PageSize, filtered.Count);
        int visibleEnd = _scrollOffset + visibleCount;

        for (int i = _scrollOffset; i < visibleEnd; i++)
        {
            var item = filtered[i];
            bool selected = i == _selectedIndex;

            // Path line
            var pointer = selected ? $"{BoldCyan}{SymbolPointer}{Reset} " : "  ";
            var pathText = _formatPath(item);
            var pathStyle = selected ? BoldWhite : White;
            Console.Write($"  {pointer}{pathStyle}{pathText}{Reset}{ClearToEndOfLine}\n");
            lineCount++;

            // Stats line
            var statsText = _formatStats(item);
            Console.Write($"      {statsText}{Reset}{ClearToEndOfLine}\n");
            lineCount++;
        }

        // Empty state
        if (filtered.Count == 0)
        {
            Console.Write($"  {Grey}No matches{Reset}{ClearToEndOfLine}\n");
            lineCount++;
        }

        // Footer
        Console.Write($"\n  {Grey}\u2191\u2193 navigate  Enter select  Esc cancel{Reset}{ClearToEndOfLine}\n");
        lineCount += 2;

        // Pad with blank lines if previous render was taller (clear leftover lines)
        for (int i = lineCount; i < _lastRenderedLineCount; i++)
        {
            Console.Write($"{ClearToEndOfLine}\n");
            lineCount++;
        }

        _lastRenderedLineCount = lineCount;
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

    private List<PcdSuggestion> Filter(IReadOnlyList<PcdSuggestion> items)
    {
        if (string.IsNullOrEmpty(_query))
            return new List<PcdSuggestion>(items);

        return items
            .Select(s =>
            {
                var trimmed = s.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dirName = Path.GetFileName(trimmed);
                var dirScore = PcdSubsequenceScorer.Score(_query.AsSpan(), dirName.AsSpan());
                var pathScore = PcdSubsequenceScorer.Score(_query.AsSpan(), trimmed.AsSpan());
                return (suggestion: s, score: Math.Max(dirScore, pathScore));
            })
            .Where(x => x.score > 0.0)
            .OrderByDescending(x => x.score)
            .Select(x => x.suggestion)
            .ToList();
    }
}
