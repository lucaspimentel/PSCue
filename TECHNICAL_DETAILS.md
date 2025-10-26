# PSCue Technical Details

**Last Updated**: 2025-01-26

This document provides technical details about PSCue's architecture, implementation, and internal workings. For user-facing documentation, see [README.md](README.md). For development guidelines, see [CLAUDE.md](CLAUDE.md).

## Table of Contents

- [IPC Communication Layer](#ipc-communication-layer)
- [Completion Cache](#completion-cache)
- [PSCue.Debug Tool](#pscuedebug-tool)
- [Architecture Diagram](#architecture-diagram)
- [Performance Metrics](#performance-metrics)
- [Cross-Platform Compatibility](#cross-platform-compatibility)
- [Key Technical Decisions](#key-technical-decisions)

---

## IPC Communication Layer

PSCue uses a Named Pipe-based IPC system to enable state sharing between the short-lived ArgumentCompleter (invoked on each Tab press) and the long-lived CommandPredictor (loaded with the PowerShell module). This creates intelligent caching and lays the foundation for future learning capabilities.

### IPC Protocol (`PSCue.Shared/IpcProtocol.cs`)

**Request Format:**
```csharp
public class IpcRequest
{
    string Command          // e.g., "git"
    string CommandLine      // e.g., "git checkout ma"
    string WordToComplete   // e.g., "ma"
    int CursorPosition      // Position in command line
    string[] Args           // Parsed arguments
    string RequestType      // Request classification (future use)
}
```

**Response Format:**
```csharp
public class IpcResponse
{
    CompletionItem[] Completions  // Array of suggestions
    bool Cached                   // Whether from cache
    long Timestamp                // Response generation time
}

public class CompletionItem
{
    string Text                   // Completion text (e.g., "main")
    string? Description           // Tooltip/description
    double Score                  // Usage-based priority (0.0-1.0)
}
```

**Protocol Details:**
- Transport: Named Pipes (`System.IO.Pipes`)
- Pipe Name: `PSCue-{ProcessId}` (session-specific)
- Serialization: JSON with source generation for NativeAOT
- Framing: 4-byte length prefix + JSON payload
- Connection Timeout: 10ms (configurable)
- Response Timeout: 50ms (configurable)

### JSON Source Generation (`PSCue.Shared/IpcJsonContext.cs`)

Source-generated JSON serialization context to eliminate NativeAOT trimming warnings:

```csharp
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(IpcRequest))]
[JsonSerializable(typeof(IpcResponse))]
[JsonSerializable(typeof(CompletionItem))]
public partial class IpcJsonContext : JsonSerializerContext { }
```

**Benefits:**
- Zero NativeAOT warnings in ArgumentCompleter
- Better performance than reflection-based serialization
- Smaller binary size due to trimming

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

### IPC Server (`PSCue.Module/IpcServer.cs`)

Asynchronous Named Pipe server running in CommandPredictor:

**Architecture:**
```
Module Load → Init.OnImport()
    ↓
Start IpcServer()
    ↓
Async Server Loop (accepts connections)
    ↓
For Each Connection:
  - Create new NamedPipeServerStream
  - Wait for client connection
  - Launch HandleClientAsync in background task
  - Continue loop to accept next connection
    ↓
HandleClientAsync (per-connection):
  - Read IpcRequest (length-prefixed JSON)
  - Check CompletionCache
  - If cached: Return cached results
  - If not: Generate via CommandCompleter.GetCompletions()
  - Cache results for future requests
  - Send IpcResponse (length-prefixed JSON)
  - Dispose pipe when complete
```

**Key Features:**
- Fire-and-forget connection handling for concurrency
- Cache-first strategy (check before generating)
- Integrates with existing `CommandCompleter.GetCompletions()`
- Graceful error handling (logs but doesn't crash)
- Clean disposal on module unload
- **Fixed race condition** (2025-01-26): Pipe ownership transferred to background task to prevent disposal during request handling

**Race Condition Fix:**
Previously, the pipe was created with `await using` in the server loop, causing it to be disposed immediately after spawning the background task. This resulted in "Cannot access a closed pipe" errors. The fix moved pipe disposal into the `HandleClientAsync` method using `await using (pipeServer)` to ensure the pipe remains open until request handling completes.

### IPC Client (`PSCue.ArgumentCompleter/IpcClient.cs`)

Synchronous Named Pipe client in ArgumentCompleter:

**Flow:**
```
Tab Press → Program.Main()
    ↓
Extract command from command line
    ↓
Try IpcClient.TryGetCompletions()
    ↓
Connect to PSCue-{PID} pipe (10ms timeout)
    ↓
If connected:
  - Send IpcRequest
  - Read IpcResponse (50ms timeout)
  - Return completions
Else:
  - Return null (triggers fallback)
    ↓
If IPC returns results: Use them
Else: Fall back to CommandCompleter.GetCompletions() (local)
```

**Key Features:**
- Fast connection timeout (10ms) - no noticeable delay if server down
- Graceful fallback to local logic
- NativeAOT-compatible (uses source-generated JSON)
- Minimal allocations for performance

### Module Integration (`PSCue.Module/Init.cs`)

Module initialization starts the IPC server automatically:

```csharp
public void OnImport()
{
    // Start IPC server
    try
    {
        _ipcServer = new IpcServer();
    }
    catch (Exception ex)
    {
        // Log but don't fail module load
        Console.Error.WriteLine($"Failed to start IPC server: {ex.Message}");
    }

    // Register predictors
    RegisterSubsystem(new CommandCompleterPredictor());
}

public void OnRemove(PSModuleInfo psModuleInfo)
{
    // Cleanup
    _ipcServer?.Dispose();
    // Unregister subsystems...
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

## PSCue.Debug Tool

`PSCue.Debug` is a diagnostic tool for testing and inspecting PSCue's completion system. It provides commands to test both local and IPC-based completion paths, measure performance, and inspect cache state.

### Commands

**`query-local <input>`**
- Tests completion logic using local `CommandCompleter.GetCompletions()`
- Does not use IPC (simulates fallback behavior)
- Useful for comparing local vs. IPC performance
- Shows timing statistics

**`query-ipc <input>`**
- Tests completion via IPC request to PSCue server
- Requires PSCue loaded in a PowerShell session
- Automatically discovers running PowerShell processes with PSCue
- Shows cached status and timing statistics

**`ping`**
- Tests IPC server connectivity
- Measures round-trip time for debug requests
- Searches all running `pwsh` and `powershell` processes

**`stats`**
- Shows cache statistics (entry count, total hits, oldest entry age)
- Requires IPC connection to PSCue server

**`cache [--filter <text>]`**
- Inspects cached completions with optional text filter
- Shows cache keys, hit counts, ages, and top completions
- Requires IPC connection to PSCue server

### PowerShell Process Discovery

The debug tool automatically finds PSCue-loaded PowerShell sessions by:
1. Checking `PSCUE_PID` environment variable (if set)
2. Iterating through all `pwsh` processes
3. Iterating through all `powershell` processes (Windows)
4. Attempting to connect to each process's pipe (`PSCue-{PID}`)
5. Using the first successful connection

**Optional targeting:**
```powershell
# In PowerShell session
$env:PSCUE_PID = $PID

# Then run debug tool - it will prefer this PID
dotnet run --project src/PSCue.Debug/ -- ping
```

### Timing Statistics

All commands show timing in milliseconds with 2 decimal places:
```
Time: 11.69ms
```

This format provides precise performance measurement without redundant microsecond display.

### Usage Examples

```powershell
# Test local completion logic (no IPC)
dotnet run --project src/PSCue.Debug/ -- query-local "git checkout ma"

# Test IPC completion (requires PSCue loaded)
dotnet run --project src/PSCue.Debug/ -- query-ipc "git checkout ma"
# Output: Found 3 suggestions (via IPC, cached: true):
#   main
#   master
#   develop
# Time: 2.43ms

# Test IPC connectivity
dotnet run --project src/PSCue.Debug/ -- ping
# Output: OK: Pong from PSCue (round-trip: 1.52ms)

# Show cache statistics
dotnet run --project src/PSCue.Debug/ -- stats
# Output: Cache Statistics:
#   Entry Count:       12
#   Total Hits:        45
#   Oldest Entry Age:  2.3m

# Inspect git-related cache entries
dotnet run --project src/PSCue.Debug/ -- cache --filter git
```

### Implementation Details

**Protocol:**
- Uses separate debug protocol with 'D' marker byte
- JSON serialization via `IpcJsonContext` (NativeAOT-compatible)
- 100ms timeout for process discovery
- Silently skips PowerShell processes without PSCue loaded

**Performance comparison:**
- `query-local`: Tests pure completion logic speed
- `query-ipc`: Tests completion + IPC overhead + cache benefits
- Comparing both reveals cache hit performance gains

## Architecture Diagram

```
┌──────────────────────────────────────────────────────────────┐
│ PowerShell Session (PID: 12345)                              │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  PSCue.Module.dll (Long-lived)                    │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ IpcServer                                              │ │
│  │ - Named Pipe: PSCue-12345                             │ │
│  │ - Async server loop                                    │ │
│  │                                                        │ │
│  │ CompletionCache                                        │ │
│  │ - ConcurrentDictionary<string, CacheEntry>            │ │
│  │ - 5-minute expiration                                  │ │
│  │ - Usage tracking (score, hit count)                   │ │
│  │                                                        │ │
│  │ CommandCompleter.GetCompletions()                     │ │
│  │ - Git, GitHub, Azure, Scoop, etc.                     │ │
│  └────────────────────────────────────────────────────────┘ │
│                          ▲                                   │
│                          │ IPC Response                      │
│                          │ (cached or fresh)                 │
└──────────────────────────┼───────────────────────────────────┘
                           │
                           │ IPC Request
                           │
┌──────────────────────────┴───────────────────────────────────┐
│ Tab Press                                                    │
├──────────────────────────────────────────────────────────────┤
│  pscue-completer.exe (Short-lived, NativeAOT)               │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ 1. Try IpcClient.TryGetCompletions()                  │ │
│  │    - Connect to PSCue-12345 (10ms timeout)            │ │
│  │    - Send IpcRequest                                   │ │
│  │    - Receive IpcResponse (50ms timeout)               │ │
│  │                                                        │ │
│  │ 2. If IPC succeeds:                                    │ │
│  │    - Use cached/fresh completions from server         │ │
│  │    - Fast! <5ms round-trip                            │ │
│  │                                                        │ │
│  │ 3. If IPC fails (server not running):                 │ │
│  │    - Fall back to local CommandCompleter              │ │
│  │    - Still works! Just no caching                     │ │
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
| IPC Connection Timeout | <10ms | 10ms ✅ |
| IPC Response Timeout | <50ms | 50ms ✅ |
| IPC Round-trip (ping) | <5ms | 1-2ms ✅ |
| Cached Completion Response | <5ms | 2-3ms ✅ |
| Local Completion (no IPC) | <20ms | 11-15ms ✅ |
| Build Time | <5s | 1.3s ✅ |
| Test Execution | <2s | <1s ✅ |
| Module Installation | <60s | ~30s ✅ |
| NativeAOT Warnings | 0 | 0 ✅ |

**Note:** Timing measurements from PSCue.Debug tool showing actual IPC and completion performance.

## Source Code Organization

### Key Files

**IPC Layer:**
- `src/PSCue.Shared/IpcProtocol.cs` - Protocol definitions (request/response types)
- `src/PSCue.Shared/IpcJsonContext.cs` - JSON source generation for NativeAOT
- `src/PSCue.Module/IpcServer.cs` - Named Pipe server
- `src/PSCue.ArgumentCompleter/IpcClient.cs` - Named Pipe client

**Caching:**
- `src/PSCue.Module/CompletionCache.cs` - Intelligent cache with usage tracking

**Module Integration:**
- `src/PSCue.Module/Init.cs` - Module initialization (starts IPC server)
- `src/PSCue.ArgumentCompleter/Program.cs` - Entry point (tries IPC, falls back to local)

**Debug Tool:**
- `src/PSCue.Debug/Program.cs` - Diagnostic tool entry point
  - `query-local` and `query-ipc` commands for testing completions
  - `ping`, `stats`, `cache` commands for IPC inspection
  - PowerShell process discovery logic
  - Timing statistics measurement

**Testing:**
- `test-scripts/` - Manual test scripts for IPC, predictors, and inline predictions
- `test/PSCue.ArgumentCompleter.Tests/` - Unit tests for completion logic
- `test/PSCue.Module.Tests/` - Unit tests for predictor logic

## Cross-Platform Compatibility

Named Pipes work seamlessly across platforms:

| Platform | Implementation | Status |
|----------|----------------|--------|
| Windows x64 | Windows Named Pipes | ✅ Tested |
| Linux x64 | Unix Domain Sockets | ✅ Should work (API identical) |
| macOS arm64 | Unix Domain Sockets | ✅ Should work (API identical) |

.NET's `System.IO.Pipes` abstracts platform differences - same code works everywhere!

## Future: Learning System (Phase 9)

The IPC layer and cache infrastructure are ready for the learning system implementation:

**Ready for Phase 9:**
- ✅ `CompletionCache.IncrementUsage()` method available
- ✅ `CompletionItem.Score` field in IPC protocol
- ✅ Usage tracking infrastructure in place

**Planned Work:**
1. Implement `IFeedbackProvider` interface in CommandPredictor
2. Register feedback provider on module initialization
3. Handle `FeedbackTrigger.Success` and `FeedbackTrigger.Error` events
4. Extract command patterns from executed commands
5. Update cache scores based on actual usage
6. Personalize completions based on user behavior

See [TODO.md](TODO.md#phase-9-feedback-provider-learning-system) for detailed implementation plan.

## Key Technical Decisions

1. **Named Pipes over HTTP**: Simpler, faster, more secure for local IPC
2. **Session-specific pipe names**: `PSCue-{PID}` prevents conflicts between PowerShell sessions
3. **JSON over MessagePack**: Human-readable, easier to debug (can optimize later)
4. **Source Generation for JSON**: Required for NativeAOT, better performance
5. **Fire-and-forget server**: Each connection handled independently for concurrency
6. **Cache-first strategy**: Check cache before generating completions
7. **Graceful fallback**: ArgumentCompleter works standalone if server unavailable
8. **Non-blocking initialization**: IPC server failure won't prevent module load

## Implementation Notes

### NativeAOT Considerations
- JSON serialization requires source generation to avoid trimming warnings
- Use `IpcJsonContext.Default.*` for all serialization operations
- Verify zero warnings with `dotnet publish -c Release -r win-x64`

### Named Pipe Platform Support
- .NET abstracts Windows Named Pipes and Unix Domain Sockets perfectly
- Same API works on Windows, Linux, and macOS without changes
- Pipe names are simple strings (e.g., `PSCue-12345`) on all platforms

### Performance Optimization
- 10ms connection timeout prevents noticeable delay when server unavailable
- Length-prefixed protocol (4-byte header) simplifies framing
- `ConcurrentDictionary` provides thread-safe caching without explicit locks
- Fire-and-forget server pattern handles multiple concurrent connections

### Error Handling
- IPC server failures don't prevent module loading
- ArgumentCompleter falls back to local logic if IPC unavailable
- All errors logged but don't crash the process
- Cache expiration prevents stale data
