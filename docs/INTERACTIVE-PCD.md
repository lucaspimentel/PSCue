# Interactive Directory Selection (PCD)

## Overview

PSCue's `pcd` command now supports interactive directory selection, allowing users to browse and select from their most frequently visited directories using a visual menu interface.

## Usage

### Basic Interactive Mode

```powershell
# Show interactive menu with top 20 directories (default)
pcd -i
pcd --interactive

# Show more directories
pcd -i -Top 50
pcd --interactive -Top 30
```

### Parameters

- `-Interactive` (alias `-i`): Enable interactive selection mode
- `-Top <int>`: Number of directories to show (default: 20, range: 5-100)

## Features

### Visual Selection Menu

The interactive mode displays:
- **Directory paths**: Full absolute paths to learned directories
- **Usage statistics**: Number of visits and last used time
- **Search/filter**: Type to narrow down the list
- **Keyboard navigation**: Arrow keys to browse

### Display Format

Each entry shows:
```
D:\source\lucaspimentel\PSCue
  42 visits · last used 2 days ago
```

Time deltas are formatted as:
- "just now" (< 1 minute)
- "5 minutes ago"
- "3 hours ago"
- "2 days ago"
- "3 weeks ago"
- "2 months ago"
- "1 year ago"

### Keyboard Controls

- **Arrow keys (Up/Down)**: Navigate through the list
- **Type**: Search/filter directories by name
- **Enter**: Select the highlighted directory and navigate to it
- **Select "< Cancel >"**: Cancel selection without navigating (appears as first option in the list)

### Smart Filtering

- Only shows directories that currently exist on disk
- Filters out stale database entries automatically
- Sorted by frecency score (frequency + recency + distance)
- Same scoring algorithm as tab completion for consistency

## Technical Details

### Implementation

- **Library**: Spectre.Console 0.54.0 for cross-platform interactive UI
- **Class**: `PcdInteractiveSelector` in `PSCue.Module`
- **PowerShell Function**: `Invoke-PCD` (alias `pcd`) in `module/Functions/PCD.ps1`

### Scoring and Ranking

Interactive mode uses the same PcdCompletionEngine as tab completion:
- **Frequency**: How often you visit the directory
- **Recency**: When you last visited
- **Distance**: Proximity to current directory

This ensures predictable and consistent results across all pcd features.

### Performance

- **Initialization**: ~200ms on first use (Spectre.Console loading)
- **Subsequent prompts**: ~10ms
- **Data retrieval**: Reuses in-memory ArgumentGraph, no database overhead

### Terminal Compatibility

**Supported terminals**:
- Windows Terminal (recommended)
- PowerShell console host
- Standard console/TTY terminals

**Limited support**:
- VSCode integrated terminal (may not work if not a TTY)
- Redirected/piped sessions (not interactive)

If the terminal doesn't support interactive mode, you'll see:
```
Error: Interactive mode requires a TTY terminal.
Try running in Windows Terminal or a standard console.
```

## Use Cases

### Exploring Navigation History

When you can't remember the exact path fragment:
```powershell
pcd -i  # Browse your history visually
```

### Multiple Good Matches

When multiple directories match and you want to choose visually:
```powershell
pcd datadog   # Best-match fuzzy navigation
pcd -i        # OR browse and select from list
```

### Directory Discovery

Rediscover directories you visited in the past:
```powershell
pcd -i -Top 100  # Show more history
```

## Edge Cases

### No Learned Data

If you haven't navigated to any directories yet:
```
No learned directories yet.
Use 'pcd <path>' to navigate and build history.
```

### All Paths Stale

If all learned directories have been deleted or moved:
```
No valid directories in history.
All learned paths have been deleted or moved.
```

### Module Not Initialized

If PSCue module failed to initialize:
```
PSCue module not initialized. Cannot show interactive selection.
```

### Non-Interactive Terminal

If running in a non-TTY environment:
```
Error: Cannot show interactive prompt in this terminal.
Try running in Windows Terminal or use regular 'pcd' commands.
```

## Configuration

Interactive mode respects all PCD configuration environment variables:

```powershell
# Frecency scoring weights
$env:PSCUE_PCD_FREQUENCY_WEIGHT = "0.5"  # Default: 0.5
$env:PSCUE_PCD_RECENCY_WEIGHT = "0.3"    # Default: 0.3
$env:PSCUE_PCD_DISTANCE_WEIGHT = "0.2"   # Default: 0.2

# Directory filtering
$env:PSCUE_PCD_ENABLE_DOT_DIR_FILTER = "true"  # Filter cache directories
$env:PSCUE_PCD_CUSTOM_BLOCKLIST = ".myapp,temp"  # Additional patterns

# Fuzzy matching
$env:PSCUE_PCD_FUZZY_MIN_MATCH_PCT = "0.7"  # Minimum similarity (70%)
```

See main README for full configuration options.

## Testing

### Manual Testing

```powershell
# 1. Install locally
./install-local.ps1 -Force

# 2. Build history
cd C:\Windows
cd C:\Users
cd D:\source

# 3. Test interactive mode
pcd -i

# 4. Verify behavior
# - Shows menu with learned directories
# - Arrow keys navigate
# - Type to search/filter
# - Enter selects and navigates
# - Esc cancels
```

### Automated Testing

```powershell
# Run unit tests
dotnet test --filter "FullyQualifiedName~PcdInteractiveSelector"
```

Test coverage:
- Constructor validation
- Time formatting (minutes, hours, days, weeks, months, years)
- No learned data handling
- Empty current directory handling
- Directory scoring and sorting
- Non-existent path filtering

## Comparison with Other Modes

| Mode | Trigger | Use Case |
|------|---------|----------|
| **Home** | `pcd` (no args) | Navigate to home directory |
| **Direct** | `pcd <existing-path>` | Navigate to known path |
| **Best-match** | `pcd <partial>` | Fuzzy find and navigate automatically |
| **Interactive** | `pcd -i` | Browse and select from visual menu |
| **Tab completion** | `pcd <partial><TAB>` | See and select from completion list |

## Future Enhancements

Potential future features:
- **Preview pane**: Show directory contents in menu
- **Bookmarks**: Pin favorite directories to top of list
- **Recent sessions**: Separate view for recent vs frequent
- **Tree view**: Show directory hierarchy in menu
- **Multi-select**: Open multiple directories in tabs (Windows Terminal)

## Troubleshooting

### Spectre.Console.dll Not Found

If you see missing DLL errors, ensure you installed using the install script:
```powershell
./install-local.ps1 -Force
```

The script uses `dotnet publish` which includes all dependencies.

### Menu Not Showing

Check that:
1. You're running in an interactive terminal (not redirected)
2. PSCue module is fully initialized
3. You have some learned navigation history

### Paths Not Updating

If the menu shows stale paths, they're filtered out automatically. To clean up:
```powershell
# View learned data
Get-PSCueLearning -Command cd

# Clear stale entries (optional)
Clear-PSCueLearning -Force
```

## See Also

- Main README: PCD configuration and features
- `docs/COMPLETED.md`: PCD development history (Phases 17-21)
- `module/Functions/PCD.ps1`: PowerShell function implementation
- `src/PSCue.Module/PcdInteractiveSelector.cs`: Interactive selector class
