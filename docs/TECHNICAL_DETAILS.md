# PSCue Technical Details

This document provides technical details about PSCue's architecture, implementation, and internal workings. For user-facing documentation, see [README.md](../README.md). For development guidelines, see [CLAUDE.md](../CLAUDE.md).

## Table of Contents

- [Module Architecture](#module-architecture)
- [Multi-Word Prediction Suggestions](#multi-word-prediction-suggestions)
- [Navigation Path Learning](#navigation-path-learning)
- [PowerShell Module Functions](#powershell-module-functions)
- [Architecture Diagram](#architecture-diagram)
- [Performance Targets](#performance-targets)
- [Cross-Platform Compatibility](#cross-platform-compatibility)
- [Key Technical Decisions](#key-technical-decisions)
- [Build Process](#build-process)
- [Installation Strategies](#installation-strategies)
- [CI/CD Architecture](#cicd-architecture)

---

## Module Architecture

PSCue uses a dual-component architecture with direct in-process communication for optimal performance and simplicity.

### Component Architecture

**ArgumentCompleter** (`pscue-completer.exe`):
- NativeAOT executable for <10ms startup
- Computes completions locally with full dynamic arguments
- Uses `PSCue.Shared` for completion logic
- No external dependencies or IPC calls

**CommandPredictor** (`PSCue.Module.dll`):
- Long-lived managed DLL loaded with PowerShell module
- Provides inline suggestions via `ICommandPredictor`
- Implements learning via `IFeedbackProvider`
- Exports PowerShell functions for direct module access
- Uses `PSCue.Shared` for completion logic

**Benefits of Direct Architecture:**
- Simpler codebase (no IPC overhead)
- Faster module loading (no server startup)
- Easier debugging (no cross-process communication)
- More reliable (no connection timeouts)

## Multi-Word Prediction Suggestions

**Completed**: 2025-11-06

Enhanced learning system to track and suggest sequential argument patterns like "git checkout master" alongside single-word suggestions.

### Sequence Tracking (`ArgumentGraph.cs`)

**Problem**: Users frequently type common argument combinations (e.g., "git checkout master", "docker run -it") but only saw single-word suggestions.

**Solution**: Track consecutive argument pairs and suggest frequently-used combinations.

**Implementation**:
```csharp
// ArgumentGraph.cs:12-69
public class ArgumentSequence
{
    public string FirstArgument { get; set; }   // e.g., "checkout"
    public string SecondArgument { get; set; }  // e.g., "master"
    public int UsageCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastUsed { get; set; }
    public double GetScore(int totalUsageCount, int decayDays = 30) { ... }
}

// ArgumentGraph.cs:299-336
public void RecordUsage(string command, string[] arguments, ...)
{
    // Track argument sequences for multi-word suggestions
    for (int i = 0; i < arguments.Length - 1; i++)
    {
        var first = arguments[i];
        var second = arguments[i + 1];

        // Skip flag-to-flag pairs (handled separately)
        if (firstIsFlag && secondIsFlag) continue;

        // Skip navigation commands (paths too specific)
        if (isNavigationCommand) continue;

        // Record sequence
        var sequenceKey = $"{first}|{second}";
        var sequence = knowledge.ArgumentSequences.GetOrAdd(...);
        sequence.UsageCount++;
        sequence.LastUsed = now;
    }
}

// ArgumentGraph.cs:441-461
public List<ArgumentSequence> GetSequencesStartingWith(string command, string firstArgument, int maxResults = 5)
{
    // Returns sequences sorted by frequency + recency score
}
```

**Pruning**: Up to 50 sequences per command (LRU eviction).

### Multi-Word Generation (`GenericPredictor.cs`)

**Problem**: How to blend single-word and multi-word suggestions without overwhelming the user.

**Solution**: Generate multi-word suggestions for top single-word candidates only.

**Implementation**:
```csharp
// GenericPredictor.cs:392-452
private void AddMultiWordSuggestions(string command, List<PredictionSuggestion> suggestions, ...)
{
    const int minUsageThreshold = 3;  // Only suggest if used 3+ times

    // Get top 5 single-word suggestions
    var topSuggestions = suggestions
        .Where(s => !s.Text.Contains(' ') && !s.IsFlag)
        .Take(5);

    foreach (var singleWord in topSuggestions)
    {
        // Get sequences starting with this word
        var sequences = _argumentGraph.GetSequencesStartingWith(command, singleWord.Text);

        foreach (var seq in sequences)
        {
            if (seq.UsageCount < minUsageThreshold) continue;

            var multiWordText = $"{seq.FirstArgument} {seq.SecondArgument}";
            suggestions.Add(new PredictionSuggestion {
                Text = multiWordText,
                Description = $"used {seq.UsageCount}x together",
                Score = baseScore * 0.95  // Slightly lower than single-word
            });
        }
    }
}
```

**Strategy**:
- Minimum 3 usage threshold prevents noise
- Only top 5 single-words get multi-word expansions
- Score multiplier (0.95×) prefers flexibility
- Deduplicates against existing suggestions

### Multi-Word Combine (`CommandPredictor.cs`)

**Problem**: How to properly combine input with multi-word completions.

**Solution**: Detect multi-word completions and match first word against partial input.

**Implementation**:
```csharp
// CommandPredictor.cs:179-190
internal static string Combine(ReadOnlySpan<char> input, string completionText)
{
    var lastWord = input[startIndex..];

    // Check if completionText is multi-word
    if (completionText.Contains(' '))
    {
        var firstWord = completionText.AsSpan(0, completionText.IndexOf(' '));
        if (firstWord.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
        {
            // "git che" + "checkout master" => "git checkout master"
            return string.Concat(input[..startIndex], completionText);
        }
    }
    // ... existing single-word logic
}
```

### Persistence (`PersistenceManager.cs`)

**Database Schema**:
```sql
CREATE TABLE argument_sequences (
    command TEXT NOT NULL COLLATE NOCASE,
    first_argument TEXT NOT NULL COLLATE NOCASE,
    second_argument TEXT NOT NULL COLLATE NOCASE,
    usage_count INTEGER NOT NULL DEFAULT 0,
    first_seen TEXT NOT NULL,
    last_used TEXT NOT NULL,
    PRIMARY KEY (command, first_argument, second_argument)
);

CREATE INDEX idx_argument_sequences_command_first
    ON argument_sequences(command, first_argument);
```

**Save/Load**: Delta tracking with additive merge (same as other learning data).

**Performance**:
- Recording: ~1-2 dictionary ops per command (<1ms)
- Memory: ~64 bytes per sequence, max 50/command (~3KB)
- Generation: ~50 lookups for 5 candidates (<1ms)
- **Total overhead: Negligible**

**Benefits**:
- Faster workflow completion (one suggestion instead of two keystrokes)
- Learns personal patterns (e.g., your most-used git branches)
- Cross-session persistence
- Works with any command (docker, kubectl, npm, etc.)

## Navigation Path Learning

**Completed**: 2025-10-30

Enhanced cd/Set-Location learning system with path normalization and context awareness.

### Path Normalization (`ArgumentGraph.cs`)

**Problem**: Different path forms (relative, absolute, ~) were learned as separate entries:
- `cd ~/projects` → learned as "~/projects"
- `cd ../projects` → learned as "../projects"
- `cd /home/user/projects` → learned as "/home/user/projects"

**Solution**: Normalize all navigation paths to absolute form before learning.

**Implementation**:
```csharp
// ArgumentGraph.cs:220-281
public void RecordUsage(string command, string[] arguments, string? workingDirectory = null)
{
    // Detect navigation commands
    var isNavigationCommand = command.Equals("cd", ...)
        || command.Equals("Set-Location", ...) ...;

    // Normalize paths for navigation commands
    if (isNavigationCommand && workingDirectory != null) {
        arguments = NormalizeNavigationPaths(arguments, workingDirectory);
    }
    // ... record normalized arguments
}

private static string? NormalizePath(string path, string workingDirectory)
{
    // Expand ~ to home directory
    if (path.StartsWith("~/") || path == "~") {
        path = path.Replace("~", Environment.GetFolderPath(...));
    }

    // Convert relative/absolute paths to full path
    return Path.IsPathRooted(path)
        ? Path.GetFullPath(path)
        : Path.GetFullPath(Path.Combine(workingDirectory, path));
}
```

**Benefits**:
- `cd ~/projects`, `cd ../projects`, `cd /home/user/projects` all learn as `/home/user/projects`
- Usage counts merge across different input forms
- Cross-session persistence stores normalized absolute paths
- Works across different working directories

### Context-Aware Filtering (`GenericPredictor.cs`)

**Problem**: Suggestions included current directory and irrelevant paths.

**Solution**: Filter learned paths by current directory and partial matches.

**Implementation**:
```csharp
// GenericPredictor.cs:105-195
if (isNavigationCommand) {
    var currentDirectory = Directory.GetCurrentDirectory();

    // Filter out current directory
    learnedPaths = learnedPaths
        .Where(s => !IsSamePath(s.Argument, currentDirectory))
        .ToList();

    // Filter by partial path match
    if (!string.IsNullOrEmpty(wordToComplete)) {
        learnedPaths = learnedPaths
            .Where(s => MatchesPartialPath(s.Argument, wordToComplete))
            .ToList();
    }

    // Add trailing directory separator (PowerShell native behavior)
    var pathWithSeparator = learned.Argument + Path.DirectorySeparatorChar;
}

private static bool MatchesPartialPath(string fullPath, string partial)
{
    // Match against directory name, path segments, or full path
    return fullPath.Contains(partial, comparison)
        || Path.GetFileName(fullPath).StartsWith(partial, comparison)
        || segments.Any(s => s.StartsWith(partial, comparison));
}
```

**Benefits**:
- Current directory never suggested
- `cd dotnet` only suggests paths containing "dotnet"
- Trailing `\` or `/` matches PowerShell Tab completion
- Platform-aware path comparison (case-sensitive on Unix)

### Absolute Path Handling (`CommandPredictor.cs`)

**Problem**: `cd dotnet` was suggesting invalid `cd dotnet D:\source\dd-trace-dotnet\`.

**Solution**: Detect absolute paths and replace (not append) last word.

**Implementation**:
```csharp
// CommandPredictor.cs:175-207
internal static string Combine(ReadOnlySpan<char> input, string completionText)
{
    var lastSpaceIndex = input.LastIndexOf(' ');
    var startIndex = lastSpaceIndex >= 0 ? lastSpaceIndex + 1 : 0;
    var lastWord = input[startIndex..];

    // Normal overlap matching...
    if (completionText.StartsWith(lastWord, ...)) {
        return string.Concat(input[..startIndex], completionText);
    }

    // Special case: absolute path replaces last word
    if (lastSpaceIndex >= 0 && IsAbsolutePath(completionText)) {
        return string.Concat(input[..startIndex], completionText);
    }

    return $"{input} {completionText}";
}

private static bool IsAbsolutePath(string path)
{
    // Windows: C:\, D:\, \\server\share
    if (path[1] == ':' || path.StartsWith("\\\\")) return true;
    // Unix: /home, /var
    if (path[0] == '/') return true;
    return false;
}
```

**Benefits**:
- `cd dotnet` + `D:\path\` → `cd D:\path\` (correct)
- Works for Windows (`C:\`) and Unix (`/`) absolute paths
- UNC paths supported (`\\server\share`)
- No impact on non-navigation commands

### Enhanced Scoring

**Frequently visited paths prioritized**:
```csharp
// GenericPredictor.cs:160-165
existing.Score = 0.85 + (learnedScore * 0.15); // 0.85-1.0 range
existing.Description = $"visited {learned.UsageCount}x";
```

**Score ranges**:
- Filesystem-only suggestions: 0.6
- Learned paths (filesystem): 0.85-1.0
- Learned paths (not in filesystem): 0.85-1.0

**Display**:
- Tooltip shows visit count: `"visited 15x"`
- Sorted by score → usage count → alphabetical

### Working Directory Capture (`FeedbackProvider.cs`)

**Required for path normalization**:
```csharp
// FeedbackProvider.cs:387-404
private void LearnFromCommand(...)
{
    // Capture current directory
    string? workingDirectory = Directory.GetCurrentDirectory();

    // Pass to learning systems
    _commandHistory?.Add(..., workingDirectory);
    _argumentGraph.RecordUsage(command, arguments, workingDirectory);
}
```

### Performance

- Path normalization: <1ms per path
- Context filtering: <5ms per suggestion
- Total overhead: <10ms (acceptable for inline predictions)
- No impact on Tab completion (<50ms target maintained)

### Cross-Platform Support

**Windows**:
- Absolute paths: `C:\`, `D:\`, `\\server\share`
- Directory separator: `\`
- Case-insensitive path comparison

**Unix (Linux/macOS)**:
- Absolute paths: `/home`, `/var`, `/usr`
- Directory separator: `/`
- Case-sensitive path comparison
- Tilde expansion: `~` → `$HOME`

### Module Integration (`PSCue.Module/ModuleInitializer.cs`)

Module initialization uses background loading so `Import-Module` returns instantly:

```csharp
public void OnImport()
{
    // Register subsystems synchronously (required by PowerShell)
    RegisterCommandPredictor(new CommandPredictor());
    RegisterFeedbackProvider(new FeedbackProvider());

    // Load all learned data in the background
    _initCts = new CancellationTokenSource();
    _initTask = Task.Run(() => InitializeInBackground(config, _initCts.Token));
}

// Background task: loads DB, builds components, publishes to PSCueModule statics.
// All consumers null-check PSCueModule.* and return empty results until ready.
private static void InitializeInBackground(InitConfiguration config, CancellationToken ct)
{
    PSCueModule.Persistence = new PersistenceManager(dbPath);
    // One shared connection across all Load* calls avoids redundant open + PRAGMA cycles.
    using var conn = persistence.CreateSharedConnection();
    PSCueModule.KnowledgeGraph = persistence.LoadArgumentGraph(conn, ...);
    PSCueModule.CommandHistory = persistence.LoadCommandHistory(conn, ...);
    // ... more components ...
    PSCueModule.GenericPredictor = new GenericPredictor(...);

    // Auto-save timer starts only after init completes
    _autoSaveTimer = new Timer(AutoSave, null, interval, interval);
}

public void OnRemove(PSModuleInfo psModuleInfo)
{
    // Cancel background init if still running, wait for completion
    _initCts?.Cancel();
    _initTask?.Wait(TimeSpan.FromSeconds(5));

    // Save learned data, dispose resources, unregister subsystems
    // ...
}
```

#### PowerShell Module Loading and Duplicate OnImport() Calls

**Issue:** PowerShell's module loading mechanism calls `IModuleAssemblyInitializer.OnImport()` **twice** when loading a module that has both a script module (`.psm1`) as `RootModule` and a binary module (`.dll`) in `NestedModules`.

**Root Cause:** During module import, PowerShell analyzes the assembly multiple times:
1. First call: When processing the nested module (`PSCue.Module.dll`)
2. Second call: During manifest processing and assembly analysis

This is documented PowerShell behavior, not a bug. It occurs because:
- The manifest specifies `RootModule = 'PSCue.psm1'` (script module)
- The manifest specifies `NestedModules = @('PSCue.Module.dll')` (binary module)
- PowerShell calls `AnalyzeModuleAssemblyWithReflection()` twice during the import process

**Evidence from Stack Traces:**
Both calls show identical entry points through PowerShell's internal assembly analyzer:
```
at PSCue.Module.Init.OnImport()
at System.Management.Automation.Runspaces.PSSnapInHelpers.ExecuteModuleInitializer(Assembly assembly, ...)
at System.Management.Automation.Runspaces.PSSnapInHelpers.AnalyzeModuleAssemblyWithReflection(...)
```

**Solution:** Handle duplicate subsystem registration gracefully:

```csharp
private void RegisterCommandPredictor(ICommandPredictor commandPredictor)
{
    try
    {
        SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, commandPredictor);
        _subsystems.Add((SubsystemKind.CommandPredictor, commandPredictor.Id));
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("was already registered"))
    {
        // Already registered - this can happen if OnImport() is called multiple times
        // This is expected behavior due to PowerShell's module loading mechanism
        // Silently ignore duplicate registration
    }
    catch (Exception ex)
    {
        // Other errors - log for diagnostics
        Console.Error.WriteLine($"Note: Command predictor not registered: {ex.Message}");
    }
}
```

**Key Points:**
- The exception type is `System.InvalidOperationException`, not `PSInvalidOperationException`
- The exception message contains "was already registered for the subsystem"
- This pattern is necessary for any PowerShell module that uses `IModuleAssemblyInitializer` with nested modules
- Attempting to prevent the double call (e.g., by reorganizing the module structure) is not practical
- The defensive code approach (catch and ignore) is the recommended solution

**Alternative Approaches Considered:**
- ❌ Checking if subsystem is already registered before calling `RegisterSubsystem()` - No public API available
- ❌ Reordering PSReadLine configuration in profile - Doesn't prevent double initialization
- ❌ Restructuring module manifest - Would break `IModuleAssemblyInitializer` triggering
- ✅ Catch `InvalidOperationException` and silently ignore - Standard pattern, works reliably

## PowerShell Module Functions

PSCue provides native PowerShell functions for testing, diagnostics, and management via direct in-process access. The source lives in `module/Functions.ps1`, organized into `#region` blocks for Learning, Database, Workflow, Smart Navigation (pcd), and Debugging.

### Learning Management

- `Get-PSCueLearning [-Command <string>] [-AsJson]` — view learned command data from memory
- `Clear-PSCueLearning [-Force] [-WhatIf] [-Confirm]` — clear all learned data (memory + database); `-Force` deletes the DB even when init has failed
- `Export-PSCueLearning -Path <string>` — export learned data to JSON
- `Import-PSCueLearning -Path <string> [-Merge]` — import learned data from JSON (replace or merge)
- `Save-PSCueLearning` — force immediate save, bypassing the auto-save timer

### Database Management

- `Get-PSCueDatabaseStats [-Detailed] [-AsJson]` — SQLite statistics and top commands
- `Get-PSCueDatabaseHistory [-Last <n>] [-Command <string>] [-AsJson]` — query command history (defaults to last 20)

### Workflow Management

- `Get-PSCueWorkflows [-Command <string>] [-AsJson]` — view learned command-to-command transitions
- `Get-PSCueWorkflowStats [-Detailed] [-AsJson]` — workflow summary statistics
- `Clear-PSCueWorkflows [-WhatIf] [-Confirm]` — clear workflow data
- `Export-PSCueWorkflows -Path <string>` — export workflows to JSON
- `Import-PSCueWorkflows -Path <string> [-Merge]` — import workflows from JSON

### Smart Navigation

- `Invoke-PCD` (exported as `pcd`, shorthand `pcdi` for `pcd -i`) — smart directory navigation with fuzzy matching, bookmarks, interactive selector, and git-root jump (`pcd -Root`/`pcd -r`)

### Debugging & Diagnostics

- `Test-PSCueCompletion -InputString <string> [-IncludeTiming]` — test completion generation locally
- `Get-PSCueModuleInfo [-AsJson]` — module version, configuration, component status, learning and DB statistics

### Usage Examples

```powershell
# View and manage learning data
Get-PSCueLearning -Command kubectl
Export-PSCueLearning -Path ~/backup.json
Save-PSCueLearning

# Query database directly
Get-PSCueDatabaseStats -Detailed
Get-PSCueDatabaseHistory -Last 50 -Command "docker"

# Workflow insights
Get-PSCueWorkflows -Command "git add"
Get-PSCueWorkflowStats -Detailed

# Test completions and diagnostics
Test-PSCueCompletion -InputString "git checkout ma" -IncludeTiming
Get-PSCueModuleInfo
```

### Design Benefits

- Direct in-process access (no IPC overhead)
- PowerShell-native patterns (objects, pipeline, tab completion)
- Comprehensive help via `Get-Help <function>`
- Standard cmdlet parameters (`-WhatIf`, `-Confirm`, `-Verbose`)
- Discoverable via `Get-Command -Module PSCue`

## Architecture Diagram

```
┌──────────────────────────────────────────────────────────────┐
│ PowerShell Session                                           │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  PSCue.Module.dll (long-lived, ReadyToRun-compiled)         │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ CommandPredictor (ICommandPredictor)                   │ │
│  │  - Inline suggestions, 20ms PowerShell hard timeout    │ │
│  │  - Blends known + generic + ML + workflow predictions  │ │
│  │                                                        │ │
│  │ FeedbackProvider (IFeedbackProvider, PS 7.4+)          │ │
│  │  - Silent learning from executed commands              │ │
│  │  - Captures $PWD for path normalization                │ │
│  │  - Error-recovery suggestions (e.g. git errors)        │ │
│  │                                                        │ │
│  │ Learning System                                        │ │
│  │  - ArgumentGraph (commands → arguments, sequences,     │ │
│  │      parameters, value bindings)                       │ │
│  │  - CommandHistory (ring buffer)                        │ │
│  │  - SequencePredictor (n-gram ML)                       │ │
│  │  - WorkflowLearner (command → next command)            │ │
│  │  - GenericPredictor (context-aware generation)         │ │
│  │                                                        │ │
│  │ Smart Navigation (pcd)                                 │ │
│  │  - PcdCompletionEngine, PcdSubsequenceScorer           │ │
│  │  - BookmarkManager (SQLite write-through)              │ │
│  │  - Interactive selector (ConsoleMenu)                  │ │
│  │                                                        │ │
│  │ Persistence                                            │ │
│  │  - PersistenceManager (SQLite WAL, 5-minute auto-save) │ │
│  │                                                        │ │
│  │ Exported PowerShell functions (see module/Functions.ps1)│ │
│  │  - Learning, Database, Workflow, Navigation, Debug     │ │
│  └────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ Tab Press (independent NativeAOT process)                    │
├──────────────────────────────────────────────────────────────┤
│  pscue-completer.exe                                         │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ CommandCompleter.GetCompletions()                      │ │
│  │  - Uses PSCue.Shared (compiled in)                     │ │
│  │  - Computes locally with full dynamic arguments        │ │
│  │  - <10ms cold start, <50ms total                       │ │
│  │  - No SQLite, no IPC                                   │ │
│  └────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

## Performance Targets

| Metric | Target |
|--------|--------|
| ArgumentCompleter startup | <10ms (NativeAOT) |
| Tab completion total | <50ms |
| **Inline prediction response** | **<20ms** (PowerShell hard timeout) |
| Module function calls | <5ms |
| Database queries | <10ms |
| PCD tab completion | <10ms |
| PCD best-match navigation | <50ms |

### Critical Performance Constraint: ICommandPredictor 20ms Timeout

**PowerShell enforces a hardcoded 20ms timeout for `ICommandPredictor.GetSuggestion()`**:

- **Source**: `PowerShell/src/System.Management.Automation/engine/Subsystem/PredictionSubsystem/CommandPrediction.cs`
- **Mechanism**: `Task.WhenAny(predictorTask, Task.Delay(20))` — any predictor not responding in 20ms is silently ignored
- **Not configurable**: Cannot be changed without recompiling PowerShell
- **Impact**: Any expensive computation (ML inference, database queries) must complete in <20ms or be pre-computed asynchronously

**Implications**:
- **Tab completion** (`Register-ArgumentCompleter`): no hard timeout, 50-100ms acceptable
- **Inline predictions** (`ICommandPredictor`): 20ms hard limit, predictions discarded if slower
- **ML features**: must use background pre-computation and caching to stay under 20ms

**References**:
- [PSReadLine #4029](https://github.com/PowerShell/PSReadLine/issues/4029) — feature request to make timeout configurable

## Source Code Organization

### Key Files

**Module Components:**
- `src/PSCue.Module/ModuleInitializer.cs` — module initialization and subsystem registration
- `src/PSCue.Module/PSCueModule.cs` — static module state container
- `src/PSCue.Module/CommandPredictor.cs` — inline predictions (ICommandPredictor)
- `src/PSCue.Module/FeedbackProvider.cs` — learning from execution (IFeedbackProvider)

**Learning System:**
- `src/PSCue.Module/ArgumentGraph.cs` — knowledge graph with path normalization, sequences, parameters, value bindings
- `src/PSCue.Module/CommandHistory.cs` — ring buffer for recent commands
- `src/PSCue.Module/GenericPredictor.cs` — context-aware suggestions
- `src/PSCue.Module/SequencePredictor.cs` — n-gram sequence prediction
- `src/PSCue.Module/WorkflowLearner.cs` — command-to-command transition learning
- `src/PSCue.Module/PersistenceManager.cs` — SQLite cross-session persistence

**Smart Navigation (pcd):**
- `src/PSCue.Module/PcdCompletionEngine.cs` — directory suggestion engine
- `src/PSCue.Module/PcdSubsequenceScorer.cs` — fzf-style matching
- `src/PSCue.Module/BookmarkManager.cs` — directory bookmarks with SQLite write-through
- `src/PSCue.Module/ConsoleMenu.cs` — interactive selector

**PowerShell Functions:**
- `module/Functions.ps1` — all exported functions, organized into five `#region` blocks (Learning, Database, Workflow, Smart Navigation, Debugging)
- `module/PSCue.psm1` — module lifecycle and completer registration
- `module/PSCue.psd1` — module manifest

**ArgumentCompleter:**
- `src/PSCue.ArgumentCompleter/Program.cs` — entry point for Tab completion
- `src/PSCue.Shared/CommandCompleter.cs` — main completion orchestrator
- `src/PSCue.Shared/KnownCompletions/` — command-specific completions (Git, Gh, Gt, Scoop, Winget, Wt, Code, Claude, Chezmoi, GitWt, and more; Azure commands under `Azure/`)

**Testing:**
- `test/PSCue.ArgumentCompleter.Tests/` — tests for completion logic
- `test/PSCue.Module.Tests/` — tests for predictor, feedback, learning, workflows, persistence, PCD

**Benchmarks:**
- `benchmark/PSCue.Benchmarks/` — BenchmarkDotNet performance tests

## Cross-Platform Compatibility

| Platform | Status | Notes |
|----------|--------|-------|
| Windows x64 | Pre-built binaries | Full support, all features |
| Linux x64 | Pre-built binaries | Full support, case-sensitive paths |
| macOS (x64, arm64) | Build from source | Not shipped as pre-built binary |

**Platform-specific considerations:**
- Path separators handled automatically (`\` on Windows, `/` on Unix)
- Case sensitivity respects platform defaults
- SQLite database works identically on all platforms
- PowerShell functions work on PowerShell 7.2+ (learning features require 7.4+)

## Key Technical Decisions

### Core Architecture

1. **Removed IPC Layer** (Phase 16.7): Simplified architecture, no inter-process communication
2. **Direct In-Process Access**: PowerShell functions access module state directly
3. **NativeAOT for ArgumentCompleter**: <10ms startup time for Tab completion
4. **Managed DLL for Module**: Full .NET capabilities for learning and persistence
5. **Shared Completion Logic** (`PSCue.Shared`): Consistency between Tab and inline predictions
6. **SQLite for Persistence**: Cross-session learning data with WAL mode for concurrency
7. **Static Module State** (`PSCueModule`): PowerShell functions access via static properties
8. **Path Normalization**: All navigation paths stored as absolute paths for consistency

### Why Separate ArgumentCompleter and CommandPredictor?

1. **Different compilation requirements**:
   - ArgumentCompleter: NativeAOT for fast startup (CLI tool)
   - CommandPredictor: Managed DLL for PowerShell SDK integration

2. **Different lifetimes**:
   - ArgumentCompleter: Launched per-completion (short-lived process)
   - CommandPredictor: Loaded once with module (long-lived)

3. **Different performance constraints**:
   - ArgumentCompleter: No hard timeout, 50-100ms acceptable for Tab completion
   - CommandPredictor: **20ms hard timeout** enforced by PowerShell for inline predictions

4. **Clear separation of concerns**:
   - ArgumentCompleter: Handles `Register-ArgumentCompleter` (Tab completion)
   - CommandPredictor: Handles `ICommandPredictor` (inline suggestions)

### Database Access Architecture

**ArgumentCompleter (Tab completion)**:
- No SQLite access — NativeAOT executable, no database dependency
- Computes completions from static and dynamic sources only
- Includes dynamic arguments (git branches, scoop packages, etc.)
- No 20ms timeout constraint

**CommandPredictor (Inline predictions)**:
- Has SQLite access via `PersistenceManager`
- Loads learned data from database in a background task on module import; consumers null-check `PSCueModule.*` statics and return empty results until loading completes
- Provides inline suggestions via `ICommandPredictor`
- 20ms hard timeout — expensive queries must be served from in-memory data

**Implication for ML Features**:
- ML predictions in inline suggestions must stay under the 20ms budget (typically served from in-memory n-gram tables)
- ML predictions in Tab completion can be synchronous (no timeout)

### Project References

- Predictor can reference ArgumentCompleter to reuse completion logic
- ArgumentCompleter is self-contained (no dependencies except .NET)

### Code Migration Strategy

- **Copy, don't link**: Independent codebase for PSCue
- Reference original projects in README/documentation
- Clean break allows for PSCue-specific enhancements
- Original projects remain independent

## Implementation Notes

### NativeAOT Considerations
- ArgumentCompleter compiled to native code for instant startup
- PSCue.Shared library compiled into ArgumentCompleter (no runtime dependencies)
- Zero trimming warnings with proper `TrimmerRootAssembly` configuration
- Verify with `dotnet publish -c Release -r win-x64`

### Module Architecture
- Long-lived managed DLL stays loaded with PowerShell session
- Direct access to completion cache and learning systems
- No IPC overhead or connection timeouts
- Simpler, more reliable than previous IPC-based design

### Performance Optimization
- `ConcurrentDictionary` provides thread-safe access without explicit locks in the learning system
- Background module initialization keeps `Import-Module` fast (~sub-100ms synchronous path) while DB loading runs off the critical path
- `PublishReadyToRun=true` on `PSCue.Module.csproj` AOT-compiles managed IL to native on release publish, eliminating first-touch JIT on cold imports
- Shared SQLite connection and batched multi-statement queries during load cut per-table round-trips
- Generic learning uses frequency + recency scoring with platform-aware path normalization

### Error Handling
- Module functions use standard PowerShell error handling
- Learning system filters sensitive commands (built-in patterns + `PSCUE_IGNORE_PATTERNS`)
- Database errors are logged but do not crash the module
- `Clear-PSCueLearning -Force` recovers from a corrupted database without requiring module initialization

---

## Directory Structure

Run `ls` in the repo root for the authoritative layout. Notable top-level directories:

- `src/PSCue.ArgumentCompleter/` — NativeAOT executable for Tab completion
- `src/PSCue.Module/` — managed DLL: predictor, feedback provider, learning system, SQLite persistence, PCD engine
- `src/PSCue.Shared/` — completion framework and per-command completions (`KnownCompletions/`, with `Azure/` for Azure-family commands)
- `module/` — PowerShell module assets (`PSCue.psd1`, `PSCue.psm1`, `Functions.ps1`)
- `test/PSCue.ArgumentCompleter.Tests/`, `test/PSCue.Module.Tests/` — xUnit test projects
- `test/test-scripts/` — ad-hoc PowerShell test scripts for interactive verification
- `benchmark/PSCue.Benchmarks/` — BenchmarkDotNet benchmarks
- `docs/` — TECHNICAL_DETAILS.md, TROUBLESHOOTING.md, COMPLETED.md, DATABASE-FUNCTIONS.md
- `install-local.ps1`, `install-remote.ps1` — installers at the repo root

---

## Build Process

### Multi-stage Build Approach

1. **Build PSCue.ArgumentCompleter** (NativeAOT):
   - Publish as native executable per platform:
     - Windows: win-x64
     - macOS Intel: osx-x64
     - macOS Apple Silicon: osx-arm64
     - Linux: linux-x64
   - Output: `pscue-completer.exe` / `pscue-completer`
   - NativeAOT settings:
     - PublishAot: true
     - OptimizationPreference: Speed
     - InvariantGlobalization: true

2. **Build PSCue.Module** (Managed DLL):
   - Build as Release for net9.0
   - Output: `PSCue.Module.dll`
   - References PSCue.ArgumentCompleter as a project reference (for shared code)
   - Includes PowerShell SDK dependency
   - Release builds enable ReadyToRun (`PublishReadyToRun=true`) to AOT-compile managed IL to native, eliminating first-touch JIT on cold module import. Requires `dotnet publish -r <RID>` (R2R cannot target a RID-less publish).

3. **Test Projects**:
   - Build and run tests for both ArgumentCompleter and Module

### Build Commands

```bash
# Build ArgumentCompleter for current platform
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Build Module (managed DLL) -- dev inner loop, no R2R
dotnet build src/PSCue.Module/ -c Release -f net9.0

# Publish Module for release (R2R AOT-compiled, requires -r <RID>)
dotnet publish src/PSCue.Module/ -c Release -r win-x64

# Run tests
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/
```

---

## Installation Strategies

PSCue supports two installation methods: local (build from source) and remote (download pre-built binaries).

### 1. Local Installation (Development/Source)

**Script**: `install-local.ps1`

**Purpose**: For developers or users who want to build from source

**Usage**:
```powershell
# Clone the repository
git clone https://github.com/lucaspimentel/PSCue.git
cd PSCue

# Run the local installation script
./install-local.ps1
```

**Workflow**:
1. Detect platform (Windows/macOS/Linux, x64/arm64)
2. Build ArgumentCompleter with NativeAOT for the detected platform
3. Build CommandPredictor as managed DLL
4. Create installation directory: `~/.local/pwsh-modules/PSCue/`
5. Copy files to installation directory:
   - Native executable: `pscue-completer[.exe]`
   - Module DLL: `PSCue.Module.dll`
   - Module files: `PSCue.psd1`, `PSCue.psm1`, `Functions.ps1`
6. Display instructions for adding to `$PROFILE`

### 2. Remote Installation (End Users)

**Script**: `install-remote.ps1`

**Purpose**: One-liner installation for end users from GitHub releases

**Usage**:
```powershell
# One-line remote installation (latest version)
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex

# Install specific version
$version = "1.0.0"; irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex
```

**Workflow**:
1. Accept optional `$version` variable from caller (defaults to "latest")
2. Detect platform (Windows/macOS/Linux, x64/arm64)
3. Map platform to release asset name:
   - Windows x64: `PSCue-win-x64.zip`
   - macOS x64: `PSCue-osx-x64.tar.gz`
   - macOS arm64: `PSCue-osx-arm64.tar.gz`
   - Linux x64: `PSCue-linux-x64.tar.gz`
4. Determine download URL from GitHub API
5. Download release asset to temp directory
6. Extract archive to temp location
7. Create installation directory: `~/.local/pwsh-modules/PSCue/`
8. Copy files from extracted archive
9. Clean up temp files
10. Display instructions for adding to `$PROFILE`

**Key Features**:
- No build tools required (no .NET SDK needed)
- Downloads pre-built binaries from GitHub releases
- Fast installation
- Supports version pinning
- Platform auto-detection

### User's Profile Setup

After either installation method, users add these lines to their PowerShell profile:

```powershell
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1
Set-PSReadLineOption -PredictionSource HistoryAndPlugin
```

---

## CI/CD Architecture

### CI Workflow (.github/workflows/ci.yml)

**Triggers**:
- Push to main branch
- Pull requests to main branch

**Jobs**:

- **Build & Test (matrix: ubuntu-latest, windows-latest)**
  - Installs both .NET 9 and .NET 10 SDKs (ArgumentCompleter targets net10.0, Module targets net9.0)
  - `dotnet restore` → `dotnet build --configuration Release` → `dotnet test`
  - Uploads test results as artifacts

### Release Workflow (.github/workflows/release.yml)

**Triggers**:
- Manual workflow dispatch (workflow_dispatch)
- Git tags matching `v*` (e.g., `v1.0.0`)

**Jobs**:

1. **Build Release Binaries (matrix)**:
   - Matrix: `windows-latest` → `win-x64` (zip), `ubuntu-latest` → `linux-x64` (tar.gz)
   - Setup .NET 10 SDK
   - `dotnet publish` both ArgumentCompleter (NativeAOT) and Module (ReadyToRun, requires `-r <RID>`) for the matrix RID
   - Copy `PSCue.psd1`, `PSCue.psm1`, `Functions.ps1`, `LICENSE`, `README.md`
   - Pack platform archive, generate SHA256 checksum, upload as artifact

2. **Create GitHub Release** (runs on `ubuntu-latest`):
   - Download all build artifacts
   - Create GitHub release with platform archives and checksums
   - Update the `latest` tag for the remote installer

**Release assets**:
```
PSCue-win-x64.zip
PSCue-linux-x64.tar.gz
checksums.txt
```

**Each archive contains**:
```
pscue-completer[.exe]      # Native executable
PSCue.Module.dll           # Module assembly
PSCue.psd1                 # Module manifest
PSCue.psm1                 # Module script
Functions.ps1              # Consolidated module functions
LICENSE                    # License file
README.md                  # Installation instructions
```

### Creating a Release

**Manual release process**:
```bash
# 1. Update version in module manifest (module/PSCue.psd1)
# 2. Commit version bump
git add module/PSCue.psd1
git commit -m "Bump version to 1.0.0"

# 3. Create and push tag
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# 4. GitHub Actions automatically builds and creates release
```

---

## Naming Conventions

### Executables
- `pscue-completer` / `pscue-completer.exe` (ArgumentCompleter native executable)

### Assemblies
- `PSCue.Module.dll`, `PSCue.Shared.dll`

### Namespaces
- `PSCue.ArgumentCompleter.*`, `PSCue.Module.*`, `PSCue.Shared.*`

### Module
- Module name: `PSCue`
- Exported aliases: `pcd`, `pcdi`
- See `module/PSCue.psd1` (`FunctionsToExport`) for the authoritative list of exported functions

---

## Platform Support

### Shipping as pre-built binaries
- Windows x64
- Linux x64

### Build from source only
- macOS x64 (Intel)
- macOS arm64 (Apple Silicon)

### PowerShell Version Requirements
- PowerShell 7.2+ (Core only)
- IFeedbackProvider requires 7.4+, but the module works with degraded functionality on 7.2-7.3
