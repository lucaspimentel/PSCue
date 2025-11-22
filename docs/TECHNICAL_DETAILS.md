# PSCue Technical Details

**Last Updated**: 2025-11-14

This document provides technical details about PSCue's architecture, implementation, and internal workings. For user-facing documentation, see [README.md](README.md). For development guidelines, see [CLAUDE.md](CLAUDE.md).

## Table of Contents

- [Multi-Word Prediction Suggestions](#multi-word-prediction-suggestions)
- [Navigation Path Learning](#navigation-path-learning)
- [PowerShell Module Functions](#powershell-module-functions)
- [Architecture Diagram](#architecture-diagram)
- [Performance Metrics](#performance-metrics)
- [Cross-Platform Compatibility](#cross-platform-compatibility)
- [Key Technical Decisions](#key-technical-decisions)

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

Module initialization registers subsystems automatically:

```csharp
public void OnImport()
{
    // Initialize module state
    PSCueModule.KnowledgeGraph = new ArgumentGraph(...);
    PSCueModule.CommandHistory = new CommandHistory(...);
    PSCueModule.Persistence = new PersistenceManager(...);

    // Register predictors
    RegisterSubsystem(new CommandPredictor());
    RegisterSubsystem(new FeedbackProvider());
}

public void OnRemove(PSModuleInfo psModuleInfo)
{
    // Save learned data
    PSCueModule.Persistence?.SaveAsync().Wait();

    // Unregister subsystems
    foreach (var (kind, id) in _subsystems)
    {
        SubsystemManager.UnregisterSubsystem(kind, id);
    }
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

PSCue provides 7 native PowerShell functions for testing, diagnostics, and management. These replace the previous `PSCue.Debug` CLI tool with direct in-process access.

### Learning System Management

**`Get-PSCueLearning [-Command <string>] [-AsJson]`**
- View learned command data from memory
- Filter by specific command
- Shows usage counts, scores, and top arguments

**`Clear-PSCueLearning [-WhatIf] [-Confirm]`**
- Clear all learned data (memory + database)
- High-impact confirmation required
- Shows counts of cleared data

**`Export-PSCueLearning -Path <string>`**
- Export learned data to JSON file
- For backup or migration scenarios

**`Import-PSCueLearning -Path <string> [-Merge]`**
- Import learned data from JSON file
- Replace or merge with current data

**`Save-PSCueLearning`**
- Force immediate save to database
- Bypasses auto-save timer

### Database Management

**`Get-PSCueDatabaseStats [-Detailed] [-AsJson]`**
- View SQLite database statistics
- Shows totals and top commands
- Optional detailed per-command breakdown

**`Get-PSCueDatabaseHistory [-Last <n>] [-Command <string>] [-AsJson]`**
- Query command history from database
- Filter by command name
- Defaults to last 20 entries

### Debugging & Diagnostics

**`Test-PSCueCompletion -InputString <string> [-IncludeTiming]`**
- Test completion generation locally
- Shows up to 20 completions with descriptions
- Optional timing information

**`Get-PSCueModuleInfo [-AsJson]`**
- Module diagnostics and status
- Version, configuration, component status
- Learning and database statistics

### Usage Examples

```powershell
# View and manage learning data
Get-PSCueLearning -Command kubectl
Export-PSCueLearning -Path ~/backup.json
Save-PSCueLearning

# Query database directly
Get-PSCueDatabaseStats -Detailed
Get-PSCueDatabaseHistory -Last 50 -Command "docker"

# Test completions and diagnostics
Test-PSCueCompletion -InputString "git checkout ma" -IncludeTiming
Get-PSCueModuleInfo
```

### Benefits Over CLI Tools

- Direct in-process access (no IPC overhead)
- PowerShell-native patterns (objects, pipeline, tab completion)
- Comprehensive help via `Get-Help <function>`
- Standard cmdlet parameters (`-WhatIf`, `-Confirm`, `-Verbose`)
- Better discoverability via `Get-Command -Module PSCue`

## Architecture Diagram

```
┌──────────────────────────────────────────────────────────────┐
│ PowerShell Session                                           │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  PSCue.Module.dll (Long-lived)                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ CommandPredictor (ICommandPredictor)                   │ │
│  │ - Provides inline suggestions                          │ │
│  │ - Uses PSCue.Shared for completions                    │ │
│  │ - Skips dynamic arguments for speed                    │ │
│  │                                                        │ │
│  │ FeedbackProvider (IFeedbackProvider)                   │ │
│  │ - Learns from successful commands                      │ │
│  │ - Updates cache scores                                 │ │
│  │ - Captures working directory                           │ │
│  │                                                        │ │
│  │ CompletionCache                                        │ │
│  │ - ConcurrentDictionary<string, CacheEntry>            │ │
│  │ - 5-minute expiration                                  │ │
│  │ - Usage tracking (score, hit count)                   │ │
│  │                                                        │ │
│  │ Learning System                                        │ │
│  │ - ArgumentGraph (knowledge graph)                      │ │
│  │ - CommandHistory (ring buffer)                         │ │
│  │ - GenericPredictor (context-aware)                     │ │
│  │ - PersistenceManager (SQLite)                          │ │
│  │                                                        │ │
│  │ PowerShell Functions (10 exported)                     │ │
│  │ - Get-PSCueCache, Clear-PSCueCache                     │ │
│  │ - Get-PSCueLearning, Export/Import/Save               │ │
│  │ - Get-PSCueDatabaseStats/History                       │ │
│  │ - Test-PSCueCompletion, Get-PSCueModuleInfo           │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ Tab Press (independent process)                              │
├──────────────────────────────────────────────────────────────┤
│  pscue-completer.exe (Short-lived, NativeAOT)               │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ CommandCompleter.GetCompletions()                      │ │
│  │ - Uses PSCue.Shared (compiled in)                      │ │
│  │ - Includes dynamic arguments (git branches, etc.)      │ │
│  │ - Fast startup (<10ms)                                 │ │
│  │ - Complete local computation                           │ │
│  │ - No external dependencies                             │ │
│  └────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

## Test Results

### Build
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.31
```

### Tests
```
Test Run Successful.
Total tests: 65
     Passed: 62
    Skipped: 3 (platform-specific)
 Total time: 0.4949 Seconds
```

### Manual Testing
```powershell
PS> Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1
PS> Start-Sleep -Milliseconds 500

PS> # Test IPC connectivity
PS> $pipeName = "PSCue-$PID"
PS> $pc = [System.IO.Pipes.NamedPipeClientStream]::new(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
PS> $pc.Connect(100)
PS> $pc.IsConnected
True  ✅

PS> # Test Tab completion
PS> TabExpansion2 'git che' 7
CompletionText  ToolTip
--------------  -------
checkout        Switch branches or restore working tree files
cherry-pick     Apply the changes introduced by some existing commits
```

## Performance Metrics

| Metric | Target | Achieved | Notes |
|--------|--------|----------|-------|
| ArgumentCompleter Startup | <10ms | <10ms ✅ | NativeAOT critical for responsiveness |
| Tab Completion Response | <50ms | 11-15ms ✅ | No hard timeout enforced by PowerShell |
| **Inline Prediction Response** | **<20ms** | **Varies** ⚠️ | **Hard limit enforced by PowerShell** |
| Cache Access | <1ms | <1ms ✅ | |
| Module Function Calls | <5ms | <5ms ✅ | |
| Module Loading | <100ms | <80ms ✅ | |
| Build Time | <5s | 1.3s ✅ | |
| Test Execution (252 tests) | <5s | <3s ✅ | |
| Module Installation | <60s | ~30s ✅ | |
| NativeAOT Warnings | 0 | 0 ✅ | |

### Critical Performance Constraint: ICommandPredictor 20ms Timeout

**PowerShell enforces a hardcoded 20ms timeout for `ICommandPredictor.GetSuggestion()`**:

- **Source**: `PowerShell/src/System.Management.Automation/engine/Subsystem/PredictionSubsystem/CommandPrediction.cs`
- **Mechanism**: `Task.WhenAny(predictorTask, Task.Delay(20))` - any predictor not responding in 20ms is silently ignored
- **Not configurable**: Cannot be changed without recompiling PowerShell (as of 7.5, 2025)
- **Impact**: Any expensive computation (ML inference, database queries) MUST complete in <20ms or be pre-computed asynchronously

**Implications**:
- ✅ **Tab completion** (`Register-ArgumentCompleter`): No hard timeout, 50-100ms acceptable
- ⚠️ **Inline predictions** (`ICommandPredictor`): **20ms hard limit**, predictions discarded if slower
- 🔧 **ML features**: Must use background pre-computation + caching to stay under 20ms

**References**:
- [PSReadLine #4029](https://github.com/PowerShell/PSReadLine/issues/4029) - Feature request to make timeout configurable
- See `./ML-PREDICTIONS.md` for architectural strategies to handle this constraint

**Note:** Simplified architecture provides faster, more reliable performance.

## Source Code Organization

### Key Files

**Module Components:**
- `src/PSCue.Module/ModuleInitializer.cs` - Module initialization and subsystem registration
- `src/PSCue.Module/PSCueModule.cs` - Static module state container
- `src/PSCue.Module/CommandPredictor.cs` - Inline predictions (ICommandPredictor)
- `src/PSCue.Module/FeedbackProvider.cs` - Learning from execution (IFeedbackProvider)
- `src/PSCue.Module/CompletionCache.cs` - Intelligent cache with usage tracking

**Learning System:**
- `src/PSCue.Module/ArgumentGraph.cs` - Knowledge graph with path normalization
- `src/PSCue.Module/CommandHistory.cs` - Ring buffer for recent commands
- `src/PSCue.Module/GenericPredictor.cs` - Context-aware suggestions
- `src/PSCue.Module/PersistenceManager.cs` - SQLite-based cross-session persistence

**PowerShell Functions:**
- `module/Functions/CacheManagement.ps1` - Cache management functions
- `module/Functions/LearningManagement.ps1` - Learning system functions
- `module/Functions/DatabaseManagement.ps1` - Database query functions
- `module/Functions/Debugging.ps1` - Testing and diagnostics functions

**ArgumentCompleter:**
- `src/PSCue.ArgumentCompleter/Program.cs` - Entry point for Tab completion
- `src/PSCue.Shared/CommandCompleter.cs` - Main completion orchestrator
- `src/PSCue.Shared/KnownCompletions/` - Command-specific completions

**Testing:**
- `test/PSCue.ArgumentCompleter.Tests/` - 140 tests for completion logic
- `test/PSCue.Module.Tests/` - 112 tests for predictor, feedback, learning, persistence

## Cross-Platform Compatibility

PSCue works seamlessly across platforms:

| Platform | Status | Notes |
|----------|--------|-------|
| Windows x64 | ✅ Tested | Full support, all features |
| Linux x64 | ✅ Tested | Full support, case-sensitive paths |
| macOS arm64 | ✅ Tested | Full support, Apple Silicon |

**Platform-specific considerations:**
- Path separators handled automatically (`\` on Windows, `/` on Unix)
- Case sensitivity respects platform defaults
- SQLite database works identically on all platforms
- PowerShell functions work on PowerShell 7.2+ (all platforms)

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
- ❌ **No SQLite access** - NativeAOT executable, no database dependency
- ✅ Computes completions from static/dynamic sources only
- ✅ Can include dynamic arguments (git branches, scoop packages, etc.)
- ✅ No 20ms timeout constraint

**CommandPredictor (Inline predictions)**:
- ✅ **Has SQLite access** via `PersistenceManager`
- ✅ Loads learned data from database on startup
- ✅ Provides inline suggestions via `ICommandPredictor`
- ⚠️ **20ms hard timeout** - expensive queries must be pre-cached

**Implication for ML Features**:
- ML predictions in inline suggestions require background pre-computation
- ML predictions in Tab completion can be synchronous (no timeout)
- See `./ML-PREDICTIONS.md` for detailed architectural strategies

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
- `ConcurrentDictionary` provides thread-safe caching without explicit locks
- Cache expiration (5 minutes) prevents stale data
- Generic learning uses frequency × recency scoring (60/40 split)
- Path normalization cached for performance

### Error Handling
- Module functions use standard PowerShell error handling
- Learning system has privacy filters for sensitive commands
- Database errors logged but don't crash module
- All tests pass with comprehensive error scenarios covered

---

## Directory Structure

```
PSCue/
├── src/
│   ├── PSCue.ArgumentCompleter/         # NativeAOT executable for Tab completion
│   │   ├── PSCue.ArgumentCompleter.csproj
│   │   ├── Program.cs                   # Entry point
│   │   └── AssemblyInfo.cs              # NativeAOT trim settings
│   │
│   ├── PSCue.Module/                    # DLL for ICommandPredictor + IFeedbackProvider
│   │   ├── PSCue.Module.csproj
│   │   ├── ModuleInitializer.cs         # IModuleAssemblyInitializer - auto-registers
│   │   ├── PSCueModule.cs               # Static module state container
│   │   ├── CommandPredictor.cs          # ICommandPredictor implementation
│   │   ├── FeedbackProvider.cs          # IFeedbackProvider - learns from execution
│   │   ├── CompletionCache.cs           # Cache with usage tracking
│   │   ├── ArgumentGraph.cs             # Knowledge graph
│   │   ├── CommandHistory.cs            # Ring buffer
│   │   ├── GenericPredictor.cs          # Context-aware suggestions
│   │   └── PersistenceManager.cs        # SQLite persistence
│   │
│   └── PSCue.Shared/                    # Shared completion logic
│       ├── PSCue.Shared.csproj
│       ├── CommandCompleter.cs          # Main completion orchestrator
│       ├── Logger.cs                    # Debug logging (concurrent write support)
│       ├── Helpers.cs                   # Utility functions
│       ├── Completions/                 # Completion framework
│       │   ├── ICompletion.cs
│       │   ├── Command.cs
│       │   ├── CommandParameter.cs
│       │   ├── StaticArgument.cs
│       │   └── DynamicArgument.cs
│       └── KnownCompletions/            # Command-specific completions
│           ├── GitCommand.cs
│           ├── GhCommand.cs
│           ├── ScoopCommand.cs
│           ├── WingetCommand.cs
│           └── Azure/
│               ├── AzCommand.cs
│               ├── AzdCommand.cs
│               └── FuncCommand.cs
│
├── module/
│   ├── PSCue.psd1                       # Module manifest
│   ├── PSCue.psm1                       # Module script
│   └── Functions/                       # PowerShell functions (Phase 16)
│       ├── CacheManagement.ps1
│       ├── LearningManagement.ps1
│       ├── DatabaseManagement.ps1
│       └── Debugging.ps1
│
├── test/
│   ├── PSCue.ArgumentCompleter.Tests/
│   │   └── PSCue.ArgumentCompleter.Tests.csproj
│   └── PSCue.Module.Tests/
│       └── PSCue.Module.Tests.csproj
│
├── scripts/
│   ├── install-local.ps1                # Build from source and install
│   └── install-remote.ps1               # Download and install from GitHub release
│
├── PSCue.slnx                           # Solution file
├── README.md
├── LICENSE
└── .gitignore
```

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

3. **Test Projects**:
   - Build and run tests for both ArgumentCompleter and Module
   - Total: 315 tests (140 ArgumentCompleter + 175 Module)

### Build Commands

```bash
# Build ArgumentCompleter for current platform
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Build Module (managed DLL)
dotnet build src/PSCue.Module/ -c Release -f net9.0

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
   - Module files: `PSCue.psd1`, `PSCue.psm1`
   - PowerShell functions: `Functions/`
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

1. **Build & Test (Matrix)**:
   - **Platforms**: ubuntu-latest, windows-latest, macos-latest
   - **Steps**:
     - Checkout code
     - Setup .NET 9.0 SDK
     - Restore dependencies: `dotnet restore`
     - Build solution: `dotnet build --configuration Release`
     - Run tests: `dotnet test --configuration Release --no-build --verbosity normal`
     - Upload test results as artifacts

2. **Lint & Format**:
   - Check code formatting: `dotnet format --verify-no-changes`
   - Run static analysis (optional): `dotnet analyze`

**Status Badge**: Add to README.md
```markdown
[![CI](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml/badge.svg)](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml)
```

### Release Workflow (.github/workflows/release.yml)

**Triggers**:
- Manual workflow dispatch (workflow_dispatch)
- Git tags matching `v*` (e.g., `v1.0.0`)

**Jobs**:

1. **Build Native Binaries (Matrix)**:
   - **Matrix dimensions**:
     - Platform: windows, macos, linux
     - Architecture: x64, arm64 (macOS only)
   - **Steps**:
     - Checkout code
     - Setup .NET 9.0 SDK
     - Publish ArgumentCompleter for each RID
     - Build CommandPredictor DLL
     - Copy module files (PSCue.psd1, PSCue.psm1, Functions/)
     - Create platform-specific archives (zip for Windows, tar.gz for others)
     - Generate checksums (SHA256) for each archive
     - Upload archives as artifacts

2. **Create GitHub Release**:
   - **Depends on**: Build Native Binaries job
   - **Steps**:
     - Download all build artifacts
     - Extract version from tag (e.g., `v1.0.0` → `1.0.0`)
     - Create GitHub release using `softprops/action-gh-release@v1`
     - Attach all platform archives + checksums
     - Update `latest` tag (for remote installer)

**Release Assets Structure**:
```
PSCue-win-x64.zip
PSCue-osx-x64.tar.gz
PSCue-osx-arm64.tar.gz
PSCue-linux-x64.tar.gz
checksums.txt
```

**Each archive contains**:
```
pscue-completer[.exe]      # Native executable
PSCue.Module.dll           # Module assembly
PSCue.psd1                 # Module manifest
PSCue.psm1                 # Module script
Functions/                 # PowerShell functions
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
- `PSCue.Module.dll` (CommandPredictor module)
- `PSCue.Shared.dll` (optional shared library, future)

### Namespaces
- `PSCue.ArgumentCompleter.*`
- `PSCue.Module.*`
- `PSCue.Shared.*`

### Module Name
- `PSCue` (PowerShell module name)

### PowerShell Functions (10 exported)
- **Cache**: `Get-PSCueCache`, `Clear-PSCueCache`, `Get-PSCueCacheStats`
- **Learning**: `Get-PSCueLearning`, `Clear-PSCueLearning`, `Export-PSCueLearning`, `Import-PSCueLearning`, `Save-PSCueLearning`
- **Database**: `Get-PSCueDatabaseStats`, `Get-PSCueDatabaseHistory`
- **Debugging**: `Test-PSCueCompletion`, `Get-PSCueModuleInfo`

---

## Platform Support

### Tier 1 (Full Support)
- Windows x64
- macOS x64 (Intel)
- macOS arm64 (Apple Silicon)
- Linux x64

### Tier 2 (Possible Future)
- Linux arm64
- Windows arm64

### PowerShell Version Requirements
- PowerShell 7.2+ (Core only)
- IFeedbackProvider requires 7.4+, but module works with degraded functionality on 7.2-7.3
