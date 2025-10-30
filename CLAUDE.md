# PSCue - Quick Reference for AI Agents

## What This Is
PowerShell completion module combining Tab completion (NativeAOT) + inline predictions (managed DLL) + IPC caching layer with **generic learning** (Phase 11) and **cross-session persistence** (Phase 12).

**Phase 11 Complete**: Now learns from ALL commands (not just git/gh/scoop), with context-aware suggestions based on usage patterns.

**Phase 12 Complete**: Learned data persists across PowerShell sessions using SQLite with concurrent access support.

**Phase 13 Complete**: Directory-aware navigation suggestions for cd/Set-Location with smart caching and learning integration.

**Phase 15 Complete**: Test coverage improvements - added 67 new tests (CommandPredictor, FeedbackProvider, IpcServer). Total: 296 tests, 295 passing (1 skipped: complex timing scenario).

**Phase 16 In Progress**: PowerShell module functions replacing PSCue.Debug CLI. Phases 16.1-16.4 complete (10 functions implemented), 16.5-16.7 remaining (testing, docs, IPC removal).

**Supported Commands Update**: Added Graphite CLI (gt) with full Tab completion support for all subcommands (create, modify, submit, sync, log, etc.) and parameters. Also includes Windows Terminal (wt) support.

## Architecture
- **ArgumentCompleter** (`pscue-completer.exe`): NativeAOT exe, <10ms startup, computes completions locally with full dynamic arguments support
- **Module** (`PSCue.Module.dll`): Long-lived, hosts IPC server (for CommandPredictor), implements `ICommandPredictor` + `IFeedbackProvider` (7.4+)
- **IPC**: Named pipes (`PSCue-{PID}` for production, `PSCue-Test-{GUID}` for tests), used only by CommandPredictor for fast inline predictions
- **Learning System (Phase 11)**:
  - **CommandHistory**: Ring buffer tracking last 100 commands
  - **ArgumentGraph**: Knowledge graph of command → arguments with frequency + recency scoring
  - **ContextAnalyzer**: Detects command sequences and workflow patterns
  - **GenericPredictor**: Generates suggestions from learned data for ANY command
  - **Hybrid CommandPredictor**: Blends known completions + generic learning
- **Persistence (Phase 12)**:
  - **PersistenceManager**: SQLite-based cross-session storage
  - **Database Location**: `~/.local/share/PSCue/learned-data.db` (Linux/macOS), `%LOCALAPPDATA%\PSCue\learned-data.db` (Windows)
  - **Auto-save**: Every 5 minutes + on module unload
  - **Concurrent Access**: SQLite WAL mode handles multiple PowerShell sessions safely
  - **Additive Merging**: Frequencies summed, timestamps use max (most recent)

## Project Structure
```
src/
├── PSCue.ArgumentCompleter/    # NativeAOT exe for Tab completion
├── PSCue.Module/               # ICommandPredictor + IFeedbackProvider + IPC server
└── PSCue.Shared/               # Shared completion logic (avoid NativeAOT reference issues)
    ├── CommandCompleter.cs     # Main orchestrator
    ├── IpcProtocol.cs          # IPC definitions
    ├── KnownCompletions/       # Command-specific: git, gh, scoop, az, wt, etc.
    └── Completions/            # Framework: Command, Parameter, Argument nodes
        ├── Command.cs          # Command with Alias support
        ├── CommandParameter.cs # Parameter with Alias support
        └── ...
```

## Key Files & Line References
- `src/PSCue.Module/IpcServer.cs`: Named pipe server, cache handling, completion generation
- `src/PSCue.Module/IpcServer.cs:27`: Constructor accepting custom pipe name (for test isolation)
- `src/PSCue.Module/PersistenceManager.cs`: SQLite-based cross-session persistence (~470 lines)
- `src/PSCue.Module/Init.cs`: Module lifecycle (load on import, save on remove, auto-save timer)
- `src/PSCue.Shared/CommandCompleter.cs`: Completion orchestration
- `test/PSCue.Module.Tests/CommandPredictorTests.cs`: CommandPredictor.Combine tests (19 tests, Phase 15)
- `test/PSCue.Module.Tests/FeedbackProviderTests.cs`: FeedbackProvider tests (26 tests, Phase 15)
- `test/PSCue.Module.Tests/IpcServerErrorHandlingTests.cs`: Error handling & edge cases (10 tests, Phase 15)
- `test/PSCue.Module.Tests/IpcServerConcurrencyTests.cs`: Concurrent request handling (7 tests, Phase 15)
- `test/PSCue.Module.Tests/IpcServerLifecycleTests.cs`: Server lifecycle & cleanup (10 tests, Phase 15)
- `test/PSCue.Module.Tests/PersistenceManagerTests.cs`: Unit tests for persistence (10 tests)
- `test/PSCue.Module.Tests/PersistenceConcurrencyTests.cs`: Multi-session concurrency (11 tests)
- `test/PSCue.Module.Tests/PersistenceEdgeCaseTests.cs`: Edge cases & error handling (18 tests)
- `test/PSCue.Module.Tests/PersistenceIntegrationTests.cs`: End-to-end integration (15 tests)

## Common Tasks
```bash
# Build
dotnet build src/PSCue.Module/ -c Release -f net9.0
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Test (296 tests total: 91 ArgumentCompleter + 205 Module including Phases 11-15)
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/

# Run specific test groups
dotnet test --filter "FullyQualifiedName~Persistence"
dotnet test --filter "FullyQualifiedName~FeedbackProvider"
dotnet test --filter "FullyQualifiedName~CommandPredictor"
dotnet test --filter "FullyQualifiedName~IpcServer"

# Install locally
./scripts/install-local.ps1

# PowerShell Module Functions (Phase 16 - replaces PSCue.Debug)
Get-PSCueCache [-Filter <string>] [-AsJson]        # View cached completions
Clear-PSCueCache [-WhatIf] [-Confirm]              # Clear cache
Get-PSCueCacheStats [-AsJson]                      # Cache statistics
Get-PSCueLearning [-Command <string>] [-AsJson]    # View learned data
Clear-PSCueLearning [-WhatIf] [-Confirm]           # Clear learned data
Export-PSCueLearning -Path <path>                  # Export to JSON
Import-PSCueLearning -Path <path> [-Merge]         # Import from JSON
Save-PSCueLearning                                 # Force save to disk
Test-PSCueCompletion -InputString <string>         # Test completions
Get-PSCueModuleInfo [-AsJson]                      # Module diagnostics

# Debug (Legacy - PSCue.Debug CLI, will be removed in Phase 16.7)
dotnet run --project src/PSCue.Debug/ -- query-ipc "git checkout ma"
dotnet run --project src/PSCue.Debug/ -- stats
dotnet run --project src/PSCue.Debug/ -- cache --filter git
```

## Key Technical Decisions
1. **NativeAOT for ArgumentCompleter**: <10ms startup required for Tab responsiveness
2. **Shared logic in PSCue.Shared**: NativeAOT exe can't be referenced by Module.dll at runtime
3. **No IPC in ArgumentCompleter**: Tab completion always computes locally with full dynamic arguments. Fast enough (<50ms) and simpler.
4. **IPC only for CommandPredictor**: Named pipes for inline predictions cache (cross-platform, <5ms round-trip)
5. **Test isolation**: Each test gets unique pipe name (`PSCue-Test-{GUID}`) to avoid conflicts
6. **NestedModules in manifest**: Required for `IModuleAssemblyInitializer` to trigger
7. **Concurrent logging**: `FileShare.ReadWrite` + `AutoFlush` for multi-process debug logging

## Performance Targets
- ArgumentCompleter startup: <10ms
- IPC round-trip: <5ms
- Cache hit: <1ms
- Total Tab completion: <50ms

## Supported Commands
git, gh, gt, az, azd, func, code, scoop, winget, wt, chezmoi, tre, lsd, dust, cd (Set-Location/sl/chdir)

**Plus**: Generic learning works for ANY command (kubectl, docker, cargo, npm, etc.)

## When Adding Features
- Put shared completion logic in `PSCue.Shared`
- DynamicArguments (git branches, scoop packages) are only used by ArgumentCompleter locally, not over IPC
- Write tests with unique pipe names: `new IpcServer($"PSCue-Test-{Guid.NewGuid():N}")`
- Update cache scores via `CompletionCache.IncrementUsage()` in `IFeedbackProvider`
- **Command aliases**: Use `Alias` property on `Command` class, include in tooltip like `"Create a new tab (alias: nt)"`
- **Parameter aliases**: Use `Alias` property on `CommandParameter` class, include in tooltip like `"Only list directories (-d)"`

## Testing Patterns
```csharp
// Test with unique pipe name to avoid conflicts
private readonly string _pipeName = $"PSCue-Test-{Guid.NewGuid():N}";
private readonly IpcServer _server;

public TestClass() {
    _server = new IpcServer(_pipeName);  // Custom pipe name
    Thread.Sleep(100);  // Give server time to start
}

private async Task<IpcResponse> SendRequest(IpcRequest request) {
    using var client = new NamedPipeClientStream(".", _pipeName, ...);
    // ... send request
}
```

## Common Pitfalls
1. **Test hangs**: Tests sharing same pipe name → Use unique names per test instance
2. **IPC not working for predictions**: Check `$env:PSCUE_PID = $PID` is set in PowerShell session (only affects inline predictions, not Tab)
3. **ArgumentCompleter slow**: DynamicArguments (git branches, scoop packages) are computed on every Tab press. This is expected and fast (<50ms).
4. **NativeAOT reference errors**: Put shared code in PSCue.Shared, not ArgumentCompleter

## Documentation
- **Implementation status**:
  - Active work: See `TODO.md`
  - Completed phases: See `COMPLETED.md` (Phases 1-13, 15 archived)
- Full details: See `docs/ARCHITECTURE.md` and `docs/TROUBLESHOOTING.md`
- Bug fix history: See git log and commit messages
- API docs: [ICommandPredictor](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor), [IFeedbackProvider](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider)

## Configuration (Environment Variables)
```powershell
# Disable generic learning entirely
$env:PSCUE_DISABLE_LEARNING = "true"

# Learning configuration (defaults shown)
$env:PSCUE_HISTORY_SIZE = "100"          # Command history size
$env:PSCUE_MAX_COMMANDS = "500"          # Max commands to track
$env:PSCUE_MAX_ARGS_PER_CMD = "100"      # Max arguments per command
$env:PSCUE_DECAY_DAYS = "30"             # Score decay period (days)

# Privacy: ignore sensitive commands (comma-separated wildcards)
$env:PSCUE_IGNORE_PATTERNS = "aws *,*secret*,*password*"
```

## Platform Support
Windows x64, macOS arm64, Linux x64 (PowerShell 7.2+, IFeedbackProvider requires 7.4+)
- when adding support for new commands, add the completer registration in module/PSCue.psm1