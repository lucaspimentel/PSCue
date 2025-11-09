# PSCue - Quick Reference for AI Agents

## What This Is
PowerShell completion module combining Tab completion (NativeAOT) + inline predictions (managed DLL) with generic learning and cross-session persistence.

**Key Features**:
- **Generic Learning**: Learns from ALL commands (not just predefined ones) with context-aware suggestions
- **Multi-Word Suggestions**: Shows common argument combinations (e.g., "git checkout master")
- **Workflow Learning**: Learns command sequences and predicts next command based on usage patterns (Phase 18.1)
- **Cross-Session Persistence**: SQLite database stores learned data across sessions
- **Directory-Aware Navigation**: Smart cd/Set-Location suggestions with path normalization
- **ML Sequence Prediction**: N-gram based next-command prediction
- **Privacy Protection**: Filters sensitive data (passwords, tokens, keys)
- **PowerShell Module Functions**: 14 functions for learning, database, workflow management, and smart navigation (no IPC overhead)
- **Smart Directory Navigation**: `pcd` command with inline predictions, relative paths, fuzzy matching, frecency scoring, and best-match navigation (Phases 17.5 + 17.6 + 17.7)

**Supported Commands**: git, gh, gt (Graphite), az, azd, func, code, scoop, winget, wt (Windows Terminal), chezmoi, tre, lsd, dust, cd/Set-Location

For completed work history, see COMPLETED.md.

## Architecture
- **ArgumentCompleter** (`pscue-completer.exe`): NativeAOT exe, <10ms startup, computes completions locally with full dynamic arguments support
- **Module** (`PSCue.Module.dll`): Long-lived, implements `ICommandPredictor` + `IFeedbackProvider` (7.4+), provides PowerShell module functions
- **Learning System**:
  - **CommandHistory**: Ring buffer tracking last 100 commands
  - **ArgumentGraph**: Knowledge graph of command → arguments with frequency + recency scoring
    - **ArgumentSequences**: Tracks consecutive argument pairs for multi-word suggestions (up to 50 per command)
  - **ContextAnalyzer**: Detects command sequences and workflow patterns
  - **SequencePredictor**: ML-based n-gram prediction for next commands
  - **WorkflowLearner**: Learns command → next command transitions with timing data (Phase 18.1)
  - **GenericPredictor**: Generates single-word and multi-word suggestions from learned data for ANY command
  - **Hybrid CommandPredictor**: Blends known completions + generic learning + ML predictions + workflow patterns
  - **PcdCompletionEngine**: Enhanced directory navigation with fuzzy matching, frecency scoring, distance awareness (Phase 17.6)
- **Persistence**:
  - **PersistenceManager**: SQLite-based cross-session storage
  - **Database Location**: `~/.local/share/PSCue/learned-data.db` (Linux/macOS), `%LOCALAPPDATA%\PSCue\learned-data.db` (Windows)
  - **Tables**: commands, arguments, co_occurrences, flag_combinations, argument_sequences, command_history, command_sequences, workflow_transitions
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
- `src/PSCue.Module/ArgumentGraph.cs`: Knowledge graph with path normalization + argument sequences (multi-word)
- `src/PSCue.Module/GenericPredictor.cs`: Context-aware suggestions with multi-word generation
- `src/PSCue.Module/CommandPredictor.cs`: Hybrid predictor with multi-word Combine support
- `src/PSCue.Module/SequencePredictor.cs`: N-gram ML prediction for command sequences
- `src/PSCue.Module/WorkflowLearner.cs`: Dynamic workflow learning with timing-aware predictions (Phase 18.1)
- `src/PSCue.Module/PersistenceManager.cs`: SQLite-based cross-session persistence with 8 tables
- `src/PSCue.Module/PcdCompletionEngine.cs`: Enhanced PCD algorithm with fuzzy matching, frecency scoring (Phase 17.6)
- `src/PSCue.Shared/CommandCompleter.cs`: Completion orchestration
- `module/Functions/LearningManagement.ps1`: PowerShell functions for learning system
- `module/Functions/DatabaseManagement.ps1`: PowerShell functions for database queries
- `module/Functions/WorkflowManagement.ps1`: PowerShell functions for workflow management (Phase 18.1)
- `module/Functions/PCD.ps1`: PowerShell smart directory navigation function (Phases 17.5 + 17.6 enhanced)
- `module/Functions/Debugging.ps1`: PowerShell functions for testing/diagnostics
- `test/PSCue.Module.Tests/ArgumentGraphTests.cs`: Argument graph + sequence tracking tests
- `test/PSCue.Module.Tests/GenericPredictorTests.cs`: Generic predictor + multi-word tests
- `test/PSCue.Module.Tests/CommandPredictorTests.cs`: Command predictor + Combine tests
- `test/PSCue.Module.Tests/SequencePredictorTests.cs`: N-gram predictor unit tests
- `test/PSCue.Module.Tests/WorkflowLearnerTests.cs`: Workflow learning tests (Phase 18.1)
- `test/PSCue.Module.Tests/PCDTests.cs`: Smart directory navigation tests (Phase 17.5)
- `test/PSCue.Module.Tests/PcdEnhancedTests.cs`: Enhanced PCD algorithm tests (Phase 17.6)
- `test/PSCue.Module.Tests/PersistenceManagerTests.cs`: Persistence unit tests
- `test/PSCue.Module.Tests/PersistenceConcurrencyTests.cs`: Multi-session concurrency tests
- `test/PSCue.Module.Tests/PersistenceIntegrationTests.cs`: End-to-end integration tests

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
dotnet test --filter "FullyQualifiedName~WorkflowLearner"

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

# Workflow Management (Phase 18.1)
Get-PSCueWorkflows [-Command <string>] [-AsJson]   # View learned workflows
Get-PSCueWorkflowStats [-Detailed] [-AsJson]       # Workflow statistics
Clear-PSCueWorkflows [-WhatIf] [-Confirm]          # Clear workflows (memory + DB)
Export-PSCueWorkflows -Path <path>                 # Export workflows to JSON
Import-PSCueWorkflows -Path <path> [-Merge]        # Import workflows from JSON

# Smart Directory Navigation (Phases 17.5 + 17.6 + 17.7 + recent fixes)
pcd [path]                                         # PowerShell Change Directory with inline predictions + tab completion
Invoke-PCD [path]                                  # Long-form function name

# Features:
# - Inline predictions: Shows directory suggestions as you type (integrated with CommandPredictor)
# - Relative paths: Converts to relative format when valid from current directory
#   - Shows .., ./src, ../sibling when on same drive and shorter than absolute
#   - Cross-drive paths always shown as absolute (e.g., D:\source\datadog\dd-trace-dotnet)
# - Tab completion: Shows fuzzy-matched directories with frecency scoring
#   - Display: Relative path in list, full path inserted for navigation
#   - Tooltip: Full path with match type indicator
# - Best-match navigation: `pcd <partial>` finds closest fuzzy match without tab
#   - Requests top 10 suggestions for better match reliability
# - Filtering: Excludes non-existent directories and current directory
# - Path normalization: All paths end with trailing \ to prevent duplicates
# - Well-known shortcuts: ~, .. (but not when already in that directory)
# - Performance: <10ms tab completion, <10ms predictor, <50ms best-match

# Algorithm (Phase 17.6 - PcdCompletionEngine):
# - Multi-stage: Well-known shortcuts → Learned directories → Optional recursive filesystem search
# - Fuzzy matching: Substring + Levenshtein distance for typo tolerance
# - Frecency scoring: Configurable blend (default: 50% frequency, 30% recency, 20% distance)
# - Distance scoring: Parent (0.9), Child (0.85-0.5), Sibling (0.7), Ancestor (0.6-0.1)
# - Relative path conversion: Only when on same drive/root and valid from current directory
# - Path deduplication: Uses normalized absolute paths (with trailing \)
# - Existence checking: Filters out learned paths that no longer exist

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
- PCD tab completion: <10ms
- PCD best-match navigation: <50ms

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
  - Active work: See `TODO.md` (includes detailed Phase 18 workflow improvements roadmap)
  - Completed phases: See `COMPLETED.md` (Phases 1-13, 15, 17.1-17.7, 18.1-18.2 archived)
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

# Workflow learning configuration (Phase 18.1: Dynamic workflow learning)
$env:PSCUE_WORKFLOW_LEARNING = "true"            # Enable workflow learning (default: true)
$env:PSCUE_WORKFLOW_MIN_FREQUENCY = "5"          # Min occurrences to suggest (default: 5)
$env:PSCUE_WORKFLOW_MAX_TIME_DELTA = "15"        # Max minutes between commands (default: 15)
$env:PSCUE_WORKFLOW_MIN_CONFIDENCE = "0.6"       # Min confidence threshold (default: 0.6)

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
- **Latest Release**: v0.3.0 (2025-11-09)
- **Installation**: `irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex`
- **Release Assets**: PSCue-win-x64.zip, PSCue-linux-x64.tar.gz, checksums.txt
- **Key Features**: Multi-word predictions, dynamic workflow learning, smart directory navigation (pcd)

When adding support for new commands, add the completer registration in module/PSCue.psm1
- don't mention TODO phases in code (like "// Add multi-word suggestions (Phase 17.4)")