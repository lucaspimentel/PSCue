# PSCue Database Functions

## Overview

PSCue now includes functions to query the SQLite database directly, allowing you to inspect what's actually persisted on disk vs. what's in memory.

## New Functions

### `Get-PSCueDatabaseStats`

Gets statistics directly from the SQLite database file.

**Basic Usage:**
```powershell
Get-PSCueDatabaseStats
```

**Output:**
```
TotalCommands       : 15
TotalArguments      : 142
TotalHistoryEntries : 89
TotalCoOccurrences  : 234
TotalFlagCombinations : 23
DatabaseSizeKB      : 156.50
DatabasePath        : C:\Users\...\AppData\Local\PSCue\learned-data.db
TopCommands         : {git, docker, kubectl, ...}
```

**Detailed Mode:**
```powershell
Get-PSCueDatabaseStats -Detailed
```

Shows per-command statistics including top arguments with usage counts.

**JSON Export:**
```powershell
Get-PSCueDatabaseStats -AsJson | Out-File db-stats.json
```

### `Get-PSCueDatabaseHistory`

Gets command history entries directly from the database.

**Basic Usage:**
```powershell
Get-PSCueDatabaseHistory
```

Shows last 20 history entries (default).

**Show more entries:**
```powershell
Get-PSCueDatabaseHistory -Last 50
```

**Filter by command:**
```powershell
Get-PSCueDatabaseHistory -Command "git"
```

**Output:**
```
Id Command CommandLine                  Timestamp            Success
-- ------- -----------                  ---------            -------
89 git     git status                   2025-10-30 9:45:12   True
88 docker  docker ps                    2025-10-30 9:43:08   True
87 git     git log --oneline            2025-10-30 9:42:01   True
```

## Database Schema

PSCue uses SQLite with the following tables:

### `commands`
- `command` (TEXT PRIMARY KEY) - Command name
- `total_usage_count` (INTEGER) - How many times used
- `first_seen` (TEXT) - First observation timestamp
- `last_used` (TEXT) - Last usage timestamp

### `arguments`
- `command` (TEXT) - Parent command
- `argument` (TEXT) - Argument text
- `usage_count` (INTEGER) - Usage frequency
- `first_seen` (TEXT) - First observation
- `last_used` (TEXT) - Last usage
- `is_flag` (BOOLEAN) - Whether it's a flag (starts with -)

### `command_history`
- `id` (INTEGER PRIMARY KEY) - Auto-increment ID
- `command` (TEXT) - Command name
- `command_line` (TEXT) - Full command line
- `arguments` (TEXT) - JSON array of arguments
- `timestamp` (TEXT) - Execution time
- `success` (BOOLEAN) - Whether command succeeded
- `working_directory` (TEXT) - Working directory when executed

### `co_occurrences`
Tracks which arguments are used together.

### `flag_combinations`
Tracks common flag combinations (e.g., `-la` for `ls -la`).

## Comparison: In-Memory vs Database

### In-Memory Functions
- `Get-PSCueLearning` - Shows what's currently loaded in memory
- `Get-PSCueCache` - Shows completion cache (in-memory only, not persisted)
- Fast access, current session state

### Database Functions
- `Get-PSCueDatabaseStats` - Shows what's persisted on disk
- `Get-PSCueDatabaseHistory` - Shows historical command executions
- Survives PowerShell restarts, cross-session data

### Why They Might Differ

1. **Auto-save interval** - Data is saved every 5 minutes
2. **Module just loaded** - Data may not be synced yet
3. **Manual save needed** - Run `Save-PSCueLearning` to sync immediately

**Check sync status:**
```powershell
$inMemory = (Get-PSCueLearning).Count
$inDb = (Get-PSCueDatabaseStats).TotalCommands

Write-Host "In memory: $inMemory | In database: $inDb"

if ($inMemory -ne $inDb) {
    Save-PSCueLearning  # Force sync
}
```

## Database Location

**Windows:**
```
C:\Users\<username>\AppData\Local\PSCue\learned-data.db
```

**Linux/macOS:**
```
~/.local/share/PSCue/learned-data.db
```

Get path programmatically:
```powershell
[PSCue.Module.PSCueModule]::Persistence.DatabasePath
```

## Concurrency & Safety

- Uses SQLite WAL (Write-Ahead Logging) mode
- Multiple PowerShell sessions can read/write safely
- 5-second busy timeout for concurrent access
- Additive merging: frequencies summed, timestamps use max

## Use Cases

### Debugging Learning Issues
```powershell
# Check if data is actually being persisted
Get-PSCueDatabaseStats

# See what commands have been tracked
Get-PSCueDatabaseHistory

# Compare in-memory vs database
Get-PSCueLearning
Get-PSCueDatabaseStats
```

### Exporting Historical Data
```powershell
# Export all historical commands
Get-PSCueDatabaseHistory -Last 1000 | Export-Csv command-history.csv

# Export detailed statistics
Get-PSCueDatabaseStats -Detailed -AsJson | Out-File db-stats.json
```

### Investigating Command Usage Patterns
```powershell
# See which git commands you use most
Get-PSCueDatabaseHistory -Command "git" |
    Group-Object CommandLine |
    Sort-Object Count -Descending |
    Select-Object Count, Name -First 10
```

### Checking Database Health
```powershell
$stats = Get-PSCueDatabaseStats

Write-Host "Database Size: $($stats.DatabaseSizeKB) KB"
Write-Host "Total Commands: $($stats.TotalCommands)"
Write-Host "Total Arguments: $($stats.TotalArguments)"
Write-Host "History Entries: $($stats.TotalHistoryEntries)"

if ($stats.TotalCommands -eq 0) {
    Write-Host "No learning data yet - run some commands!"
}
```

## Related Functions

- `Get-PSCueLearning` - In-memory learning data
- `Save-PSCueLearning` - Force save to database
- `Clear-PSCueLearning` - Clear database and memory
- `Export-PSCueLearning` - Export to JSON
- `Import-PSCueLearning` - Import from JSON

## Technical Details

### Direct SQLite Access
These functions use `Microsoft.Data.Sqlite` directly to query the database:
- Read-only connection for safety
- 5-second busy timeout for concurrency
- Proper connection disposal (no locks left behind)

### Performance
- Very fast - direct SQL queries
- No impact on in-memory learning system
- Can run anytime without disrupting predictions

## Files Added

- `module/Functions/DatabaseManagement.ps1` - New database query functions (332 lines)
- `test-database-functions.ps1` - Test script demonstrating usage

## Files Modified

- `module/PSCue.psm1:101` - Added `. $PSScriptRoot/Functions/DatabaseManagement.ps1`
- `module/PSCue.psd1:74-76` - Added function exports:
  - `Get-PSCueDatabaseStats`
  - `Get-PSCueDatabaseHistory`
