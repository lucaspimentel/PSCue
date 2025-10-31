# PSCue Technical Details

**Last Updated**: 2025-10-30

This document provides technical details about PSCue's architecture, implementation, and internal workings. For user-facing documentation, see [README.md](README.md). For development guidelines, see [CLAUDE.md](CLAUDE.md).

## Table of Contents

- [Completion Cache](#completion-cache)
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

## Completion Cache

### Implementation (`PSCue.Module/CompletionCache.cs`)

Thread-safe, intelligent caching system:

**Features:**
- `ConcurrentDictionary<string, CacheEntry>` for thread safety
- Time-based expiration (5-minute default)
- Hit count tracking for statistics
- Usage score tracking (0.0 to 1.0)
- `IncrementUsage()` method for learning system
- Cache key generation from command context
- `GetStatistics()` for debugging

**Cache Strategy:**
- Cache Key: Command + normalized arguments (e.g., "git|checkout")
- Expiration: 5 minutes since last write
- Memory: Automatic cleanup via `RemoveExpired()`

---

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
    PSCueModule.Cache = new CompletionCache();
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

PSCue provides native PowerShell functions for testing, diagnostics, and management. These replace the previous `PSCue.Debug` CLI tool with direct in-process access.

### Cache Management

**`Get-PSCueCache [-Filter <string>] [-AsJson]`**
- View cached completions with optional filter
- Shows cache keys, completion counts, hit counts, and age
- Pipeline-friendly object output

**`Clear-PSCueCache [-WhatIf] [-Confirm]`**
- Clear all cached completions
- Interactive confirmation by default
- Shows count of removed entries

**`Get-PSCueCacheStats [-AsJson]`**
- View cache statistics
- Shows total entries, hits, average hits, oldest entry

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
- Cache, learning, and database statistics

### Usage Examples

```powershell
# View and manage cache
Get-PSCueCache -Filter git
Get-PSCueCacheStats
Clear-PSCueCache

# View and manage learning data
Get-PSCueLearning -Command kubectl
Export-PSCueLearning -Path ~/backup.json
Save-PSCueLearning

# Query database directly
Get-PSCueDatabaseStats -Detailed
Get-PSCueDatabaseHistory -Last 50 -Command "docker"

# Test completions
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

| Metric | Target | Achieved |
|--------|--------|----------|
| ArgumentCompleter Startup | <10ms | <10ms ✅ |
| Tab Completion Response | <50ms | 11-15ms ✅ |
| Cache Access | <1ms | <1ms ✅ |
| Module Function Calls | <5ms | <5ms ✅ |
| Module Loading | <100ms | <80ms ✅ |
| Build Time | <5s | 1.3s ✅ |
| Test Execution (252 tests) | <5s | <3s ✅ |
| Module Installation | <60s | ~30s ✅ |
| NativeAOT Warnings | 0 | 0 ✅ |

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

1. **Removed IPC Layer** (Phase 16.7): Simplified architecture, no inter-process communication
2. **Direct In-Process Access**: PowerShell functions access module state directly
3. **NativeAOT for ArgumentCompleter**: <10ms startup time for Tab completion
4. **Managed DLL for Module**: Full .NET capabilities for learning and persistence
5. **Shared Completion Logic** (`PSCue.Shared`): Consistency between Tab and inline predictions
6. **SQLite for Persistence**: Cross-session learning data with WAL mode for concurrency
7. **Static Module State** (`PSCueModule`): PowerShell functions access via static properties
8. **Path Normalization**: All navigation paths stored as absolute paths for consistency

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
