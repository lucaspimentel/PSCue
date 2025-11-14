# FZF-Style Interactive Menu Implementation Plan

## Overview

Add an FZF-style fuzzy filtering interactive completion menu to PSCue. This will be **opt-in** - users configure it in their profile using `Set-PSReadLineKeyHandler`.

## Key FZF Features to Implement

**Core Behavior**:
- ✅ **Real-time fuzzy filtering**: As you type, list updates instantly
- ✅ **Fuzzy matching**: "gco" matches "git checkout", "gsm" matches "git submodule"
- ✅ **Smart scoring**: Prioritize consecutive matches, word boundaries, case matches
- ✅ **Highlight matches**: Show which characters matched in each result
- ✅ **Fast performance**: <10ms filtering even with 1000+ items
- ✅ **Query shown at top**: Display what user is typing

**UI Layout** (similar to FZF):
```
> gc_                          [query input line]
  15/342                       [count: filtered/total]
> git commit                   [selected item - highlighted]
  git checkout                 [unselected items]
  git clone
  git config
  ...
```

## Architecture

```
InteractiveMenu
  ├─> FuzzyMatcher (new)
  │   ├─> Match(string candidate, string query) -> MatchResult?
  │   ├─> Score calculation (consecutive chars, word boundaries, case)
  │   └─> Match highlighting (which chars matched)
  │
  ├─> MenuState (tracks current state)
  │   ├─> AllItems (original list)
  │   ├─> FilteredItems (after fuzzy match)
  │   ├─> Query (current search string)
  │   ├─> SelectedIndex
  │   └─> ScrollOffset
  │
  └─> Show() main loop
      ├─> Render query line + items
      ├─> Handle input (typing, arrows, Enter, Escape)
      └─> Update FilteredItems on query change
```

### File Structure

```
src/PSCue.Module/
  ├─> InteractiveMenu.cs (new)
  ├─> FuzzyMatcher.cs (new)
  ├─> CompletionItem.cs (new)
  └─> PSCueModule.cs (add GetCompletionsForMenu method)

test/PSCue.Module.Tests/
  ├─> FuzzyMatcherTests.cs (new)
  └─> InteractiveMenuTests.cs (new)

docs/
  └─> INTERACTIVE-MENU.md (new - user guide)
```

## Design Details

### CompletionItem Structure

```csharp
public class CompletionItem
{
    public string CompletionText { get; set; }  // What gets inserted
    public string ListItemText { get; set; }    // What's shown in menu (may differ)
    public string ToolTip { get; set; }         // Description/help text
    public CompletionResultType ResultType { get; set; } // For color coding
}
```

### FuzzyMatcher Design

```csharp
public class FuzzyMatcher
{
    public record MatchResult(
        int Score,
        int[] MatchedIndices  // Which character positions matched
    );

    public static MatchResult? Match(string candidate, string query)
    {
        if (string.IsNullOrEmpty(query))
            return new MatchResult(0, Array.Empty<int>());

        // Implement fuzzy matching algorithm
        // Similar to FZF's scoring:
        // 1. Check if all query chars exist in candidate (in order)
        // 2. Score bonuses:
        //    - Consecutive characters: +15 per char
        //    - Word boundary (after space, dash, slash): +10
        //    - Case match: +5
        //    - Early match (closer to start): +2
        // 3. Score penalties:
        //    - Gap between matches: -1 per char
        //    - Late match (far from start): -1
    }

    public static IEnumerable<(CompletionItem item, MatchResult match)>
        FilterAndSort(CompletionItem[] items, string query)
    {
        return items
            .Select(item => (item, match: Match(item.ListItemText, query)))
            .Where(x => x.match != null)
            .OrderByDescending(x => x.match!.Score)
            .ThenBy(x => x.item.ListItemText);
    }
}
```

### Scoring Algorithm (based on FZF)

```csharp
private static int CalculateScore(string candidate, int[] matchedIndices)
{
    int score = 0;

    for (int i = 0; i < matchedIndices.Length; i++)
    {
        int pos = matchedIndices[i];

        // Bonus: Consecutive match
        if (i > 0 && matchedIndices[i - 1] == pos - 1)
            score += 15;

        // Bonus: Word boundary (after space, slash, dash, underscore)
        if (pos > 0 && IsWordBoundary(candidate[pos - 1]))
            score += 10;

        // Bonus: Case match
        if (char.IsUpper(candidate[pos]))
            score += 5;

        // Bonus: Early position
        score += Math.Max(0, 20 - pos);

        // Penalty: Gap from previous match
        if (i > 0)
        {
            int gap = pos - matchedIndices[i - 1] - 1;
            score -= gap;
        }
    }

    return score;
}

private static bool IsWordBoundary(char c)
{
    return c == ' ' || c == '/' || c == '-' || c == '_' || c == '.';
}
```

### InteractiveMenu Implementation

```csharp
public class InteractiveMenu
{
    private const int MaxVisibleItems = 10;

    private class MenuState
    {
        public CompletionItem[] AllItems { get; set; }
        public List<(CompletionItem item, FuzzyMatcher.MatchResult match)> FilteredItems { get; set; }
        public string Query { get; set; } = "";
        public int SelectedIndex { get; set; } = 0;
        public int ScrollOffset { get; set; } = 0;
    }

    public static string? Show(CompletionItem[] items)
    {
        if (items.Length == 0) return null;

        var state = new MenuState
        {
            AllItems = items,
            FilteredItems = items.Select(i => (i, new FuzzyMatcher.MatchResult(0, Array.Empty<int>()))).ToList()
        };

        var (cursorLeft, cursorTop) = Console.GetCursorPosition();

        try
        {
            RenderMenu(state, cursorLeft, cursorTop);

            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                switch (key.Key)
                {
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.Tab when !key.Modifiers.HasFlag(ConsoleModifiers.Shift):
                        if (state.SelectedIndex < state.FilteredItems.Count - 1)
                        {
                            state.SelectedIndex++;
                            AdjustScroll(state);
                            RenderMenu(state, cursorLeft, cursorTop);
                        }
                        break;

                    case ConsoleKey.UpArrow:
                    case ConsoleKey.Tab when key.Modifiers.HasFlag(ConsoleModifiers.Shift):
                        if (state.SelectedIndex > 0)
                        {
                            state.SelectedIndex--;
                            AdjustScroll(state);
                            RenderMenu(state, cursorLeft, cursorTop);
                        }
                        break;

                    case ConsoleKey.Enter:
                        if (state.FilteredItems.Count > 0)
                        {
                            ClearMenu(state, cursorLeft, cursorTop);
                            return state.FilteredItems[state.SelectedIndex].item.CompletionText;
                        }
                        break;

                    case ConsoleKey.Escape:
                        ClearMenu(state, cursorLeft, cursorTop);
                        return null;

                    case ConsoleKey.Backspace:
                        if (state.Query.Length > 0)
                        {
                            state.Query = state.Query[..^1];
                            UpdateFilter(state);
                            RenderMenu(state, cursorLeft, cursorTop);
                        }
                        break;

                    default:
                        // Any printable character
                        if (!char.IsControl(key.KeyChar))
                        {
                            state.Query += key.KeyChar;
                            UpdateFilter(state);
                            RenderMenu(state, cursorLeft, cursorTop);
                        }
                        break;
                }
            }
        }
        catch
        {
            ClearMenu(state, cursorLeft, cursorTop);
            return null;
        }
    }

    private static void UpdateFilter(MenuState state)
    {
        state.FilteredItems = FuzzyMatcher.FilterAndSort(state.AllItems, state.Query).ToList();
        state.SelectedIndex = 0;
        state.ScrollOffset = 0;
    }

    private static void AdjustScroll(MenuState state)
    {
        if (state.SelectedIndex < state.ScrollOffset)
            state.ScrollOffset = state.SelectedIndex;
        else if (state.SelectedIndex >= state.ScrollOffset + MaxVisibleItems)
            state.ScrollOffset = state.SelectedIndex - MaxVisibleItems + 1;
    }

    private static void RenderMenu(MenuState state, int left, int top)
    {
        int menuWidth = Math.Min(80, Console.WindowWidth - left);

        // Line 0: Query input
        Console.SetCursorPosition(left, top);
        Console.Write($"> {state.Query}".PadRight(menuWidth));

        // Line 1: Count
        Console.SetCursorPosition(left, top + 1);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {state.FilteredItems.Count}/{state.AllItems.Length}".PadRight(menuWidth));
        Console.ResetColor();

        // Lines 2+: Items
        int visibleCount = Math.Min(state.FilteredItems.Count - state.ScrollOffset, MaxVisibleItems);
        for (int i = 0; i < visibleCount; i++)
        {
            int itemIndex = state.ScrollOffset + i;
            var (item, match) = state.FilteredItems[itemIndex];

            Console.SetCursorPosition(left, top + 2 + i);

            bool isSelected = itemIndex == state.SelectedIndex;
            if (isSelected)
            {
                Console.Write("> ");
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                Console.Write("  ");
            }

            // Render item with highlighted matches
            RenderWithHighlights(item.ListItemText, match.MatchedIndices, menuWidth - 2, isSelected);
            Console.ResetColor();
        }

        // Clear any remaining lines from previous render
        for (int i = visibleCount; i < MaxVisibleItems; i++)
        {
            Console.SetCursorPosition(left, top + 2 + i);
            Console.Write(new string(' ', menuWidth));
        }
    }

    private static void RenderWithHighlights(string text, int[] matchedIndices, int maxWidth, bool isSelected)
    {
        var matchSet = new HashSet<int>(matchedIndices);
        int written = 0;

        for (int i = 0; i < text.Length && written < maxWidth; i++)
        {
            if (matchSet.Contains(i))
            {
                // Highlight matched character
                var oldFg = Console.ForegroundColor;
                Console.ForegroundColor = isSelected ? ConsoleColor.Yellow : ConsoleColor.Cyan;
                Console.Write(text[i]);
                Console.ForegroundColor = oldFg;
            }
            else
            {
                Console.Write(text[i]);
            }
            written++;
        }

        // Pad remaining space
        if (written < maxWidth)
            Console.Write(new string(' ', maxWidth - written));
    }

    private static void ClearMenu(MenuState state, int left, int top)
    {
        int linesToClear = 2 + Math.Min(state.FilteredItems.Count, MaxVisibleItems);
        for (int i = 0; i < linesToClear; i++)
        {
            Console.SetCursorPosition(0, top + i);
            Console.Write(new string(' ', Console.WindowWidth));
        }
        Console.SetCursorPosition(left, top);
    }
}
```

### Bridge Method in PSCueModule

```csharp
public static CompletionItem[] GetCompletionsForMenu(string line, int cursor)
{
    // Reuse existing completion logic from:
    // - CommandCompleter
    // - ArgumentGraph (learned suggestions)
    // - GenericPredictor

    // Return as CompletionItem[] for the menu
}
```

## Performance Optimizations

1. **Efficient fuzzy matching**:
   - Early exit if query chars not in candidate
   - Use `Span<char>` for zero-allocation matching
   - Cache scoring calculations

2. **Incremental filtering**:
   - When user types, start from previous filtered results (if query is longer)
   - Only re-filter from scratch on backspace

3. **Lazy rendering**:
   - Only render visible items (10 at a time)
   - Don't recompute highlights if they haven't changed

## User Profile Setup

```powershell
# User's profile (Microsoft.PowerShell_profile.ps1)

# Option 1: Override Tab with FZF-style menu
Set-PSReadLineKeyHandler -Chord 'Tab' -ScriptBlock {
    $line = $null
    $cursor = $null
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)

    $completions = [PSCue.Module.PSCueModule]::GetCompletionsForMenu($line, $cursor)
    $selected = [PSCue.Module.InteractiveMenu]::Show($completions)

    if ($selected) {
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert($selected)
    }
}

# Option 2: Ctrl+Space (keep Tab default)
Set-PSReadLineKeyHandler -Chord 'Ctrl+Space' -ScriptBlock {
    $line = $null
    $cursor = $null
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)

    $completions = [PSCue.Module.PSCueModule]::GetCompletionsForMenu($line, $cursor)
    $selected = [PSCue.Module.InteractiveMenu]::Show($completions)

    if ($selected) {
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert($selected)
    }
}
```

## User Experience Example

```
PS> git
[User presses Ctrl+Space]

> _                           [empty query, all items shown]
  150/150
> git add
  git commit
  git checkout
  git push
  ...

[User types "co"]
> co_                         [query updates in real-time]
  8/150
> git commit                  [matched chars highlighted]
  git checkout
  git config
  git clone
  ...

[User types "m"]
> com_                        [filters down further]
  2/150
> git commit
  git commit --amend
```

## Feature Phases

### Phase 1 (MVP)
- ✅ Fuzzy matching with scoring
- ✅ Real-time filtering as you type
- ✅ Match highlighting
- ✅ Arrow key navigation
- ✅ Query display
- ✅ Count display
- ✅ Enter to select, Escape to cancel
- ✅ Backspace to edit query
- ✅ Scrolling for long lists

### Phase 2 (Enhancements)
- Ctrl+N / Ctrl+P for navigation (vi-style)
- Ctrl+U to clear query
- Case-sensitive mode toggle (Ctrl+S?)
- Exact match mode (prefix with `'`)
- Tab/Shift+Tab for navigation

### Phase 3 (Advanced)
- Multi-select mode (Ctrl+Space to toggle item)
- Preview pane (show full tooltip/description)
- Color themes configuration
- History of recent queries
- Multi-column layout for wide terminals

## Testing Strategy

**Unit Tests** (with mocked Console):
- FuzzyMatcher:
  - Scoring algorithm correctness
  - Match index tracking
  - Edge cases (empty strings, no matches, exact matches)
  - Performance with large candidate lists
- InteractiveMenu:
  - Navigation (arrows, tab)
  - Filtering updates
  - Selection and cancellation
  - Scrolling behavior

**Manual Testing**:
- Different terminals (Windows Terminal, VSCode, PowerShell ISE)
- Different color schemes (dark/light)
- Long completion lists (100+ items)
- Terminal resize while menu open
- Ctrl+C while menu open

## Performance Targets

- Fuzzy matching: <10ms for 1000+ items
- Rendering: <5ms per frame
- Total latency (keystroke → updated UI): <15ms

## Documentation

Create `docs/INTERACTIVE-MENU.md` with:
1. Feature overview and benefits
2. Installation instructions (profile setup)
3. Example configurations (Tab vs Ctrl+Space)
4. Keybindings reference
5. Customization options (future: colors, keybindings)
6. Troubleshooting common issues
7. Limitations and known issues

## Open Questions

1. **Completion text vs display text**: Should we handle cases where what's displayed differs from what's inserted? (e.g., show relative paths but insert absolute?)
   - **Answer**: Yes, use `ListItemText` for display, `CompletionText` for insertion

2. **Color scheme**: Should we use hardcoded colors or try to detect/respect terminal theme?
   - **Start with**: Hardcoded colors (DarkBlue bg, Cyan/Yellow highlights)
   - **Future**: Configuration support

3. **Filtering behavior with special chars**: How to handle quotes, spaces, backslashes in query?
   - **Start with**: Match literally, no special handling
   - **Future**: Smart escaping/quoting

4. **Integration with existing completer**: Should `GetCompletionsForMenu()` call `CommandCompleter.GetCompletions()` directly?
   - **Answer**: Yes, reuse existing logic to avoid duplication

5. **Case sensitivity**: Should fuzzy matching be case-insensitive by default?
   - **Answer**: Case-insensitive matching, but bonus points for case matches

## Implementation Order

1. ✅ Design document (this file)
2. Implement FuzzyMatcher class with tests
3. Implement InteractiveMenu class with mocked Console
4. Add GetCompletionsForMenu bridge method
5. Manual testing in PowerShell
6. Write user documentation
7. Create example profile snippets
