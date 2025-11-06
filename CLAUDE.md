# PSCue - Quick Reference for AI Agents

## What This Is
PowerShell completion module combining Tab completion (NativeAOT) + inline predictions (managed DLL) with generic learning and cross-session persistence.

**Key Features**:
- **Generic Learning**: Learns from ALL commands (not just predefined ones) with context-aware suggestions
- **Cross-Session Persistence**: SQLite database stores learned data across sessions
- **Directory-Aware Navigation**: Smart cd/Set-Location suggestions with path normalization
- **PowerShell Module Functions**: 7 functions for learning and database management (no IPC overhead)
- **Comprehensive Test Coverage**: Full test suite for all components

**Supported Commands**: git, gh, gt (Graphite), az, azd, func, code, scoop, winget, wt (Windows Terminal), chezmoi, tre, lsd, dust, cd/Set-Location

For completed work history, see COMPLETED.md.

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
- `src/PSCue.Module/SequencePredictor.cs`: N-gram ML prediction for command sequences (Phase 17.1)
- `src/PSCue.Module/PersistenceManager.cs`: SQLite-based cross-session persistence (~770 lines)
- `src/PSCue.Shared/CommandCompleter.cs`: Completion orchestration
- `module/Functions/LearningManagement.ps1`: PowerShell functions for learning system (Phase 16)
- `module/Functions/DatabaseManagement.ps1`: PowerShell functions for database queries (Phase 16)
- `module/Functions/Debugging.ps1`: PowerShell functions for testing/diagnostics (Phase 16)
- `test/PSCue.Module.Tests/CommandPredictorTests.cs`: CommandPredictor.Combine tests (Phase 14-15)
- `test/PSCue.Module.Tests/FeedbackProviderTests.cs`: FeedbackProvider tests (Phase 15)
- `test/PSCue.Module.Tests/ArgumentGraphTests.cs`: Path normalization tests (Phase 14)
- `test/PSCue.Module.Tests/GenericPredictorTests.cs`: Context-aware filtering tests (Phase 14)
- `test/PSCue.Module.Tests/SequencePredictorTests.cs`: N-gram predictor unit tests (Phase 17.1)
- `test/PSCue.Module.Tests/SequencePersistenceIntegrationTests.cs`: Sequence persistence tests (Phase 17.1)
- `test/PSCue.Module.Tests/SequencePerformanceTests.cs`: Performance benchmarks (<1ms, <20ms) (Phase 17.1)
- `test/PSCue.Module.Tests/PersistenceManagerTests.cs`: Unit tests for persistence
- `test/PSCue.Module.Tests/PersistenceConcurrencyTests.cs`: Multi-session concurrency
- `test/PSCue.Module.Tests/PersistenceEdgeCaseTests.cs`: Edge cases & error handling
- `test/PSCue.Module.Tests/PersistenceIntegrationTests.cs`: End-to-end integration

## Common Tasks
```bash
# Build
dotnet build src/PSCue.Module/ -c Release -f net9.0
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Test
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/

# Run specific test groups
dotnet test --filter "FullyQualifiedName~Persistence"
dotnet test --filter "FullyQualifiedName~FeedbackProvider"
dotnet test --filter "FullyQualifiedName~CommandPredictor"
dotnet test --filter "FullyQualifiedName~SequencePredictor"

# Install locally
./scripts/install-local.ps1

# PowerShell Module Functions (Phase 16 - replaces PSCue.Debug)
# Learning Management (in-memory + database)
Get-PSCueLearning [-Command <string>] [-AsJson]    # View learned data (in-memory)
Clear-PSCueLearning [-Force] [-WhatIf] [-Confirm]  # Clear learned data (memory + DB), -Force to delete DB directly
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
- Total Tab completion: <50ms
- Module function calls: <5ms
- Database queries: <10ms

## Supported Commands
git, gh, gt, az, azd, func, code, scoop, winget, wt, chezmoi, tre, lsd, dust, cd (Set-Location/sl/chdir)

**Plus**: Generic learning works for ANY command (kubectl, docker, cargo, npm, etc.)

## When Adding Features
- Put shared completion logic in `PSCue.Shared`
- DynamicArguments (git branches, scoop packages) are computed locally by ArgumentCompleter
- Learning happens automatically via `FeedbackProvider` - no manual tracking needed
- **Command aliases**: Use `Alias` property on `Command` class, include in tooltip like `"Create a new tab (alias: nt)"`
- **Parameter aliases**: Use `Alias` property on `CommandParameter` class, include in tooltip like `"Only list directories (-d)"`
  - **IMPORTANT**: Do NOT create separate parameter entries for short and long forms (e.g., `-d` and `--diff` as separate parameters)
  - Instead, define the long form with the short form as an alias: `new("--diff", "Compare files (-d)") { Alias = "-d" }`
  - This prevents duplicate suggestions and keeps the completion list clean

## Testing Patterns
```csharp
// Test module functions directly using PSCueModule static properties
[Fact]
public void TestLearningAccess()
{
    var graph = PSCueModule.KnowledgeGraph;
    Assert.NotNull(graph);

    // Test learning operations
    graph.RecordUsage("git", new[] { "status" }, null);
    var suggestions = graph.GetSuggestions("git", Array.Empty<string>());
    Assert.Contains(suggestions, s => s.Argument == "status");
}
```

## Common Pitfalls
1. **ArgumentCompleter slow**: DynamicArguments (git branches, scoop packages) are computed on every Tab press. This is expected and fast (<50ms).
2. **NativeAOT reference errors**: Put shared code in PSCue.Shared, not ArgumentCompleter
3. **Module functions return null**: Module may not be fully initialized. Check PSCueModule.KnowledgeGraph != null before use.
4. **Corrupted database prevents initialization**: Use `Clear-PSCueLearning -Force` to delete database files without requiring module initialization.
5. **Partial word completion**: When implementing predictor features, always check if the command line ends with a space. If not, the last word is being completed and suggestions should be filtered by `StartsWith(wordToComplete)`.

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

# ML prediction configuration (Phase 17.1: N-gram sequence predictor)
$env:PSCUE_ML_ENABLED = "true"           # Enable ML sequence predictions (default: true)
$env:PSCUE_ML_NGRAM_ORDER = "2"          # N-gram order: 2=bigrams, 3=trigrams (default: 2)
$env:PSCUE_ML_NGRAM_MIN_FREQ = "3"       # Minimum frequency to suggest (default: 3 occurrences)

# Privacy & Security: Command filtering
# BUILT-IN patterns (always active, cannot be disabled):
#   *password*, *passwd*, *secret*, *api*key*, *token*, *private*key*,
#   *credentials*, *bearer*, *oauth*
# HEURISTIC detection (always active):
#   - GitHub/Stripe keys (sk_, pk_, ghp_, gho_, etc.)
#   - AWS access keys (AKIA...)
#   - JWT tokens (eyJ...)
#   - Bearer tokens
#   - Long base64/hex strings (40+ chars, outside quotes)
#
# Additional user patterns (comma-separated wildcards):
$env:PSCUE_IGNORE_PATTERNS = "aws *,terraform *,*custom-secret*"
```

## Platform Support
**Supported**: Windows x64, Linux x64 (PowerShell 7.2+, IFeedbackProvider requires 7.4+)
**Not supported**: macOS (can build from source using install-local.ps1)

**CI/CD**: Automated builds and releases via GitHub Actions for win-x64 and linux-x64
- **Latest Release**: v0.2.0 (2025-11-05)
- **Installation**: `irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex`
- **Release Assets**: PSCue-win-x64.zip, PSCue-linux-x64.tar.gz, checksums.txt
- **Includes**: Functions/ directory fix from v0.1.0

When adding support for new commands, add the completer registration in module/PSCue.psm1