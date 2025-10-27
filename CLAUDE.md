# PSCue - Quick Reference for AI Agents

## What This Is
PowerShell completion module combining Tab completion (NativeAOT) + inline predictions (managed DLL) + IPC caching layer with learning.

## Architecture
- **ArgumentCompleter** (`pscue-completer.exe`): NativeAOT exe, <10ms startup, calls IPC for cached data, falls back to local
- **Module** (`PSCue.Module.dll`): Long-lived, hosts IPC server, implements `ICommandPredictor` + `IFeedbackProvider` (7.4+)
- **IPC**: Named pipes (`PSCue-{PID}` for production, `PSCue-Test-{GUID}` for tests), JSON protocol, <5ms round-trip
- **Cache**: Usage tracking, priority scoring, learns from command execution

## Project Structure
```
src/
├── PSCue.ArgumentCompleter/    # NativeAOT exe for Tab completion
├── PSCue.Module/               # ICommandPredictor + IFeedbackProvider + IPC server
└── PSCue.Shared/               # Shared completion logic (avoid NativeAOT reference issues)
    ├── CommandCompleter.cs     # Main orchestrator
    ├── IpcProtocol.cs          # IPC definitions
    ├── KnownCompletions/       # Command-specific: git, gh, scoop, az, etc.
    └── Completions/            # Framework: Command, Parameter, Argument nodes
```

## Key Files & Line References
- `src/PSCue.Module/IpcServer.cs`: Named pipe server, cache handling, completion generation
- `src/PSCue.Module/IpcServer.cs:27`: Constructor accepting custom pipe name (for test isolation)
- `src/PSCue.Shared/CommandCompleter.cs`: Completion orchestration
- `test/PSCue.Module.Tests/IpcServerIntegrationTests.cs:22`: Unique pipe name generation per test

## Common Tasks
```bash
# Build
dotnet build src/PSCue.Module/ -c Release -f net9.0
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Test (89 tests total: 62 ArgumentCompleter + 27 Module)
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/

# Install locally
./scripts/install-local.ps1

# Debug
dotnet run --project src/PSCue.Debug/ -- query-ipc "git checkout ma"
dotnet run --project src/PSCue.Debug/ -- stats
dotnet run --project src/PSCue.Debug/ -- cache --filter git
```

## Key Technical Decisions
1. **NativeAOT for ArgumentCompleter**: <10ms startup required for Tab responsiveness
2. **Shared logic in PSCue.Shared**: NativeAOT exe can't be referenced by Module.dll at runtime
3. **Named Pipes for IPC**: Cross-platform, <5ms round-trip, session-specific names
4. **Test isolation**: Each test gets unique pipe name (`PSCue-Test-{GUID}`) to avoid conflicts
5. **NestedModules in manifest**: Required for `IModuleAssemblyInitializer` to trigger
6. **`includeDynamicArguments` flag**: ArgumentCompleter=true (full), ICommandPredictor=false (fast)
7. **Concurrent logging**: `FileShare.ReadWrite` + `AutoFlush` for multi-process debug logging

## Performance Targets
- ArgumentCompleter startup: <10ms
- IPC round-trip: <5ms
- Cache hit: <1ms
- Total Tab completion: <50ms

## Supported Commands
git, gh, az, azd, func, code, scoop, winget, chezmoi, tre, lsd, dust

## When Adding Features
- Put shared completion logic in `PSCue.Shared`
- Use `includeDynamicArguments` flag for expensive operations (git branches, scoop packages)
- Write tests with unique pipe names: `new IpcServer($"PSCue-Test-{Guid.NewGuid():N}")`
- Update cache scores via `CompletionCache.IncrementUsage()` in `IFeedbackProvider`

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
2. **IPC not working**: Check `$env:PSCUE_PID = $PID` is set in PowerShell session
3. **ArgumentCompleter slow**: Check `includeDynamicArguments` flag usage
4. **NativeAOT reference errors**: Put shared code in PSCue.Shared, not ArgumentCompleter

## Documentation
- Full details: See `docs/ARCHITECTURE.md` and `docs/TROUBLESHOOTING.md`
- Implementation status: See `TODO.md`
- Bug fix history: See git log and commit messages
- API docs: [ICommandPredictor](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor), [IFeedbackProvider](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider)

## Platform Support
Windows x64, macOS arm64, Linux x64 (PowerShell 7.2+, IFeedbackProvider requires 7.4+)
