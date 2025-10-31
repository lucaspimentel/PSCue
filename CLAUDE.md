# PSCue - Quick Reference for AI Agents

## What This Is
PowerShell completion module combining Tab completion (NativeAOT) + inline predictions (managed DLL) with **generic learning** (Phase 11) and **cross-session persistence** (Phase 12).

**Phase 11 Complete**: Now learns from ALL commands (not just git/gh/scoop), with context-aware suggestions based on usage patterns.

**Phase 12 Complete**: Learned data persists across PowerShell sessions using SQLite with concurrent access support.

**Phase 13 Complete**: Directory-aware navigation suggestions for cd/Set-Location with smart caching and learning integration.

**Phase 14 Complete**: Enhanced cd/Set-Location learning with path normalization and context awareness:
- Normalizes all navigation paths to absolute form (handles ~, .., relative paths)
- Different path forms merge to same entry (cd ~/foo and cd ../foo → same learned path)
- Filters suggestions by current directory and partial path matching
- Boosts frequently visited paths (0.85-1.0 score vs 0.6 for filesystem)
- Adds trailing directory separator to match PowerShell native behavior
- Fixes absolute path handling in predictions (cd dotnet → cd D:\path\, not cd dotnet D:\path\)

**Phase 15 Complete**: Test coverage improvements - added 67 new tests (CommandPredictor, FeedbackProvider). Total: 296 tests passing (156 Module + 140 ArgumentCompleter).

**Phase 16 Complete**: PowerShell module functions replaced PSCue.Debug CLI. All IPC infrastructure removed for simpler architecture. 10 PowerShell functions implemented (cache, learning, database, debugging).

**Supported Commands Update**: Added Graphite CLI (gt) with full Tab completion support for all subcommands (create, modify, submit, sync, log, etc.) and parameters. Also includes Windows Terminal (wt) support.

## Architecture
- **ArgumentCompleter** (`pscue-completer.exe`): NativeAOT exe, <10ms startup, computes completions locally with full dynamic arguments support
- **Module** (`PSCue.Module.dll`): Long-lived, implements `ICommandPredictor` + `IFeedbackProvider` (7.4+), provides PowerShell module functions
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
├── PSCue.Module/               # ICommandPredictor + IFeedbackProvider + PowerShell functions
└── PSCue.Shared/               # Shared completion logic (avoid NativeAOT reference issues)
    ├── CommandCompleter.cs     # Main orchestrator
    ├── KnownCompletions/       # Command-specific: git, gh, scoop, az, wt, etc.
    └── Completions/            # Framework: Command, Parameter, Argument nodes
        ├── Command.cs          # Command with Alias support
        ├── CommandParameter.cs # Parameter with Alias support
        └── ...
```

## Key Files & Line References
- `src/PSCue.Module/ModuleInitializer.cs`: Module lifecycle, subsystem registration
- `src/PSCue.Module/PSCueModule.cs`: Static module state container for PowerShell functions
- `src/PSCue.Module/ArgumentGraph.cs`: Knowledge graph with path normalization (Phase 14)
- `src/PSCue.Module/GenericPredictor.cs`: Context-aware suggestions with path filtering (Phase 14)
- `src/PSCue.Module/CommandPredictor.cs`: Absolute path handling in Combine method (Phase 14)
- `src/PSCue.Module/PersistenceManager.cs`: SQLite-based cross-session persistence (~470 lines)
- `src/PSCue.Shared/CommandCompleter.cs`: Completion orchestration
- `module/Functions/CacheManagement.ps1`: PowerShell functions for cache management (Phase 16)
- `module/Functions/LearningManagement.ps1`: PowerShell functions for learning system (Phase 16)
- `module/Functions/DatabaseManagement.ps1`: PowerShell functions for database queries (Phase 16)
- `module/Functions/Debugging.ps1`: PowerShell functions for testing/diagnostics (Phase 16)
- `test/PSCue.Module.Tests/CommandPredictorTests.cs`: CommandPredictor.Combine tests (23 tests, Phase 14-15)
- `test/PSCue.Module.Tests/FeedbackProviderTests.cs`: FeedbackProvider tests (26 tests, Phase 15)
- `test/PSCue.Module.Tests/ArgumentGraphTests.cs`: Path normalization tests (28 tests, Phase 14)
- `test/PSCue.Module.Tests/GenericPredictorTests.cs`: Context-aware filtering tests (20 tests, Phase 14)
- `test/PSCue.Module.Tests/PersistenceManagerTests.cs`: Unit tests for persistence (10 tests)
- `test/PSCue.Module.Tests/PersistenceConcurrencyTests.cs`: Multi-session concurrency (11 tests)
- `test/PSCue.Module.Tests/PersistenceEdgeCaseTests.cs`: Edge cases & error handling (18 tests)
- `test/PSCue.Module.Tests/PersistenceIntegrationTests.cs`: End-to-end integration (15 tests)

## Common Tasks
```bash
# Build
dotnet build src/PSCue.Module/ -c Release -f net9.0
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Test (296 tests total: 140 ArgumentCompleter + 156 Module including Phases 11-15)
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/

# Run specific test groups
dotnet test --filter "FullyQualifiedName~Persistence"
dotnet test --filter "FullyQualifiedName~FeedbackProvider"
dotnet test --filter "FullyQualifiedName~CommandPredictor"

# Install locally
./scripts/install-local.ps1

# PowerShell Module Functions (Phase 16 - replaces PSCue.Debug)
# Cache Management (in-memory)
Get-PSCueCache [-Filter <string>] [-AsJson]        # View cached completions
Clear-PSCueCache [-WhatIf] [-Confirm]              # Clear cache
Get-PSCueCacheStats [-AsJson]                      # Cache statistics

# Learning Management (in-memory + database)
Get-PSCueLearning [-Command <string>] [-AsJson]    # View learned data (in-memory)
Clear-PSCueLearning [-WhatIf] [-Confirm]           # Clear learned data (memory + DB)
Export-PSCueLearning -Path <path>                  # Export to JSON
Import-PSCueLearning -Path <path> [-Merge]         # Import from JSON
Save-PSCueLearning                                 # Force save to disk

# Database Management (direct SQLite queries)
Get-PSCueDatabaseStats [-Detailed] [-AsJson]       # Database stats (reads DB directly)
Get-PSCueDatabaseHistory [-Last <n>] [-Command <name>] [-AsJson]  # Query DB history

# Debugging & Testing
Test-PSCueCompletion -InputString <string>         # Test completions
Get-PSCueModuleInfo [-AsJson]                      # Module diagnostics

# See DATABASE-FUNCTIONS.md for detailed database query examples
```

## Key Technical Decisions
1. **NativeAOT for ArgumentCompleter**: <10ms startup required for Tab responsiveness
2. **Shared logic in PSCue.Shared**: NativeAOT exe can't be referenced by Module.dll at runtime
3. **ArgumentCompleter computes locally**: Tab completion always computes locally with full dynamic arguments. Fast enough (<50ms) and simpler.
4. **NestedModules in manifest**: Required for `IModuleAssemblyInitializer` to trigger
5. **Concurrent logging**: `FileShare.ReadWrite` + `AutoFlush` for multi-process debug logging
6. **PowerShell module functions**: Direct in-process access, no IPC overhead (Phase 16)

## Performance Targets
- ArgumentCompleter startup: <10ms
- Cache hit: <1ms
- Total Tab completion: <50ms
- Module function calls: <5ms

## Supported Commands
git, gh, gt, az, azd, func, code, scoop, winget, wt, chezmoi, tre, lsd, dust, cd (Set-Location/sl/chdir)

**Plus**: Generic learning works for ANY command (kubectl, docker, cargo, npm, etc.)

## When Adding Features
- Put shared completion logic in `PSCue.Shared`
- DynamicArguments (git branches, scoop packages) are computed locally by ArgumentCompleter
- Update cache scores via `CompletionCache.IncrementUsage()` in `IFeedbackProvider`
- **Command aliases**: Use `Alias` property on `Command` class, include in tooltip like `"Create a new tab (alias: nt)"`
- **Parameter aliases**: Use `Alias` property on `CommandParameter` class, include in tooltip like `"Only list directories (-d)"`

## Testing Patterns
```csharp
// Test module functions directly using PSCueModule static properties
[Fact]
public void TestCacheAccess()
{
    var cache = PSCueModule.Cache;
    Assert.NotNull(cache);

    // Test cache operations
    cache.IncrementUsage("git|checkout", "main");
    var stats = cache.GetStatistics();
    Assert.True(stats.TotalHits > 0);
}
```

## Common Pitfalls
1. **ArgumentCompleter slow**: DynamicArguments (git branches, scoop packages) are computed on every Tab press. This is expected and fast (<50ms).
2. **NativeAOT reference errors**: Put shared code in PSCue.Shared, not ArgumentCompleter
3. **Module functions return null**: Module may not be fully initialized. Check PSCueModule.Cache != null before use.

## Documentation
- **Implementation status**:
  - Active work: See `TODO.md`
  - Completed phases: See `COMPLETED.md` (Phases 1-13, 15 archived)
- **Database functions**: See `DATABASE-FUNCTIONS.md` for detailed SQLite query examples and schema
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