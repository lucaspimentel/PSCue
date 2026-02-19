# PSCue - Quick Reference for AI Agents

## What This Is
PowerShell completion module combining Tab completion (NativeAOT) + inline predictions (managed DLL) with generic learning and cross-session persistence.

**Key Features**:
- **Parameter-Value Binding**: Understands parameter-value relationships (e.g., `-f net6.0`), provides context-aware suggestions (Phase 20)
- **Generic Learning**: Learns from ALL commands (not just predefined ones) with context-aware suggestions
- **Partial Command Predictions**: Frequency-based command suggestions as you type (e.g., "g" → "git", "gh", "gt")
- **Multi-Word Suggestions**: Shows common argument combinations (e.g., "git checkout master")
- **Workflow Learning**: Learns command sequences and predicts next command based on usage patterns
- **Cross-Session Persistence**: SQLite database stores learned data across sessions
- **Directory-Aware Navigation**: Smart cd/Set-Location suggestions with path normalization and symlink resolution
- **Symlink Deduplication**: Resolves symlinks, junctions, and directory links to prevent duplicate suggestions (Phase 21.1)
- **ML Sequence Prediction**: N-gram based next-command prediction
- **Privacy Protection**: Filters sensitive data (passwords, tokens, keys)
- **PowerShell Module Functions**: 14 functions for learning, database, workflow management, and smart navigation (no IPC overhead)
- **Smart Directory Navigation**: `pcd` command with inline predictions, relative paths, fuzzy matching, frecency scoring, and best-match navigation

**Supported Commands**: git, gh, gt (Graphite), az, azd, func, code, scoop, winget, wt (Windows Terminal), chezmoi, tre, lsd, dust, cd/Set-Location

For completed work history, see docs/COMPLETED.md.

## Architecture
- **ArgumentCompleter** (`pscue-completer.exe`): NativeAOT exe, <10ms startup, computes completions locally with full dynamic arguments support
- **Module** (`PSCue.Module.dll`): Long-lived, implements `ICommandPredictor` + `IFeedbackProvider` (7.4+), provides PowerShell module functions
- **Learning System**:
  - **CommandHistory**: Ring buffer tracking last 100 commands
  - **CommandParser**: Parses commands into typed arguments (Verb, Flag, Parameter, ParameterValue, Standalone) (Phase 20)
  - **ArgumentGraph**: Knowledge graph of command → arguments with frequency + recency scoring
    - **ArgumentSequences**: Tracks consecutive argument pairs for multi-word suggestions (up to 50 per command)
    - **ParameterStats**: Tracks parameters and their known values (Phase 20)
    - **ParameterValuePairs**: Tracks bound parameter-value pairs (Phase 20)
  - **ContextAnalyzer**: Detects command sequences and workflow patterns
  - **SequencePredictor**: ML-based n-gram prediction for next commands
  - **WorkflowLearner**: Learns command → next command transitions with timing data
  - **GenericPredictor**: Generates context-aware suggestions (values only after parameters, flags otherwise)
  - **Hybrid CommandPredictor**: Blends known completions + generic learning + ML predictions + workflow patterns
  - **PcdCompletionEngine**: Enhanced directory navigation with fuzzy matching, frecency scoring, distance awareness
- **Persistence**:
  - **PersistenceManager**: SQLite-based cross-session storage
  - **Database Location**: `~/.local/share/PSCue/learned-data.db` (Linux/macOS), `%LOCALAPPDATA%\PSCue\learned-data.db` (Windows)
  - **Tables**: commands, arguments, co_occurrences, flag_combinations, argument_sequences, command_history, command_sequences, workflow_transitions, parameters, parameter_values
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
- `src/PSCue.Module/CommandParser.cs`: Command line parser for parameter-value binding (Phase 20)
- `src/PSCue.Module/ArgumentGraph.cs`: Knowledge graph with path normalization + symlink resolution + argument sequences + parameter tracking (Phase 21.1)
- `src/PSCue.Module/GenericPredictor.cs`: Context-aware suggestions (values only after parameters, multi-word support)
- `src/PSCue.Module/CommandPredictor.cs`: Hybrid predictor with multi-word Combine support
- `src/PSCue.Module/SequencePredictor.cs`: N-gram ML prediction for command sequences
- `src/PSCue.Module/WorkflowLearner.cs`: Dynamic workflow learning with timing-aware predictions
- `src/PSCue.Module/PersistenceManager.cs`: SQLite-based cross-session persistence with 10 tables
- `src/PSCue.Module/PcdCompletionEngine.cs`: Enhanced PCD algorithm with fuzzy matching, frecency scoring, filesystem search, symlink resolution (Phases 17.6 + 17.9 + 19.0 + 21.1)
- `src/PSCue.Module/PcdConfiguration.cs`: Shared configuration for PCD (tab completion + predictor) (Phase 19.0 + 21.2)
- `src/PSCue.Module/PcdInteractiveSelector.cs`: Interactive directory selection with visual styling (color-coded indicators, decorative UI)
- `src/PSCue.Module/FeedbackProvider.cs`: Learns from command execution, records navigation paths with trailing separators
- `src/PSCue.Shared/CommandCompleter.cs`: Completion orchestration
- `module/Functions/LearningManagement.ps1`: PowerShell functions for learning system
- `module/Functions/DatabaseManagement.ps1`: PowerShell functions for database queries
- `module/Functions/WorkflowManagement.ps1`: PowerShell functions for workflow management (Phase 18.1)
- `module/Functions/PCD.ps1`: PowerShell smart directory navigation function (Phases 17.5 + 17.6 + 17.9)
- `module/Functions/Debugging.ps1`: PowerShell functions for testing/diagnostics
- `test/PSCue.Module.Tests/ArgumentGraphTests.cs`: Argument graph + sequence tracking tests
- `test/PSCue.Module.Tests/GenericPredictorTests.cs`: Generic predictor + multi-word tests
- `test/PSCue.Module.Tests/CommandPredictorTests.cs`: Command predictor + Combine tests
- `test/PSCue.Module.Tests/SequencePredictorTests.cs`: N-gram predictor unit tests
- `test/PSCue.Module.Tests/WorkflowLearnerTests.cs`: Workflow learning tests (Phase 18.1)
- `test/PSCue.Module.Tests/PCDTests.cs`: Smart directory navigation tests (Phase 17.5)
- `test/PSCue.Module.Tests/PcdEnhancedTests.cs`: Enhanced PCD algorithm tests with symlink resolution (Phases 17.6 + 17.9 + 21.1 + 21.2)
- `test/PSCue.Module.Tests/PcdMatchScoreTests.cs`: Unit tests for CalculateMatchScore directory name matching
- `test/PSCue.Module.Tests/PcdRobustnessTests.cs`: Tests for handling stale/non-existent paths gracefully
- `test/PSCue.Module.Tests/PcdInteractiveSelectorTests.cs`: Unit tests for interactive directory selection
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
./install-local.ps1

# Dev testing (isolated from production install)
./install-local.ps1 -Force -InstallPath D:\temp\PSCue-dev
# Then in a new PowerShell session:
#   $env:PSCUE_DATA_DIR = "D:\temp\PSCue-dev\data"
#   Import-Module "D:\temp\PSCue-dev\PSCue.psd1"
# Cleanup: Remove-Item -Recurse -Force D:\temp\PSCue-dev

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

# Smart Directory Navigation (Phases 17.5 + 17.6 + 17.7 + 17.9 + 19.0 + Bug Fixes + Native cd Behavior + Interactive Mode)
pcd [path]                                         # PowerShell Change Directory with inline predictions + tab completion
pcd -Interactive [-Top <int>]                      # Interactive selection menu (alias: -i)
Invoke-PCD [path]                                  # Long-form function name

# Features:
# - Inline predictions: Shows directory suggestions as you type (integrated with CommandPredictor)
# - Tab completion: Matches native cd behavior exactly
#   - CompletionText (inserted): .\ChildDir\ or '.\Dir With Spaces\' (relative with .\ prefix, trailing separator, single quotes)
#   - ListItemText (displayed): Clean directory names without prefixes/separators/quotes
#   - Tooltip: Full absolute path with match type indicator ([found], [learned], [fuzzy])
#   - Uses platform-appropriate separators (\ on Windows, / on Unix)
#   - Filesystem search: Shows unlearned directories via child + recursive search
# - Interactive selection: `pcd -i` shows visual menu to browse and select from learned directories
#   - Uses Spectre.Console for cross-platform interactive UI
#   - Visual styling: Decorative header with folder icon, color-coded usage indicators (green/yellow/grey dots), time-based coloring
#   - Display format: Bold paths with ~ shortening + usage stats (visits, last used time with recency colors)
#   - Keyboard navigation: Arrow keys, type to search (with search icon), Enter to select; "< Cancel >" option or Esc to cancel
#   - Highlight style: Cyan on grey background with bold for selected item
#   - Same frecency scoring as tab completion for consistency
#   - Filters out non-existent directories automatically
#   - Excludes current directory and .. parent shortcut (neither is useful to navigate to interactively)
#   - Default shows top 20, configurable via -Top parameter (range: 5-100)
#   - Timestamp tracking: All pcd navigations manually record under 'cd' command for accurate stats
# - Best-match navigation: `pcd <partial>` finds closest fuzzy match without tab
#   - Directory name matching: "dd-trace-dotnet" matches "D:\source\datadog\dd-trace-dotnet" from any location
#   - Searches top 200 learned paths (not just top 20) for better match coverage
#   - Loops through all suggestions until finding one that exists (robustness)
#   - Shows helpful errors instead of attempting Set-Location on non-existent paths
#   - Requests top 10 suggestions for better match reliability
#   - Uses deeper recursive search for thorough discovery
# - Filtering: Excludes non-existent directories (both tab and predictor), current directory, and parent when typing absolute paths
# - Path normalization: All paths end with trailing \ to prevent duplicates
# - Well-known shortcuts: ~, .. (only suggested for relative paths, not absolute)
# - Performance: <50ms tab completion, <10ms predictor
# - Code sharing: Unified configuration via PcdConfiguration class (Phase 19.0)
# - Robustness: Handles stale database entries, race conditions, and permission issues gracefully

# Algorithm (Phase 17.6 + 17.9 + 19.0 + 21.2 + Bug Fixes - PcdCompletionEngine):
# - Stage 1: Well-known shortcuts (~, ..) - skipped for absolute paths
# - Stage 2: Learned directories (searches top 200 paths for better coverage)
#   - Directory name matching: Checks both full path AND directory name for matches (PcdCompletionEngine.cs:559-589)
#   - Exact match boost: 100× multiplier ensures exact matches always appear first
#   - Cache filtering: Filters .codeium, .claude, .dotnet, node_modules, bin, obj, etc. (Phase 21.2)
#   - Parent directory filtered for absolute paths
# - Stage 3a: Direct filesystem search (non-recursive) - always enabled
#   - Cache filtering: Applied to discovered directories
# - Stage 3b: Recursive filesystem search - ALWAYS enabled when configured (depth-controlled)
#   - Tab completion: maxDepth=3 (thorough, can afford deeper search)
#   - Inline predictor: maxDepth=1 (fast, shallow search only)
#   - Cache filtering: Applied to recursively discovered directories
# - Cache/metadata filtering (Phase 21.2): Blocklisted directories are filtered UNLESS explicitly typed
#   - Default blocklist: .codeium, .claude, .dotnet, .nuget, .git, .vs, .vscode, .idea, node_modules, bin, obj, target, __pycache__, .pytest_cache
#   - Explicit typing overrides: typing ".claude" will show .claude directories
#   - Configurable via PSCUE_PCD_ENABLE_DOT_DIR_FILTER and PSCUE_PCD_CUSTOM_BLOCKLIST
# - Fuzzy matching (Phase 21.4): Substring + Levenshtein with quality controls
#   - Minimum similarity threshold: 70% (configurable via PSCUE_PCD_FUZZY_MIN_MATCH_PCT)
#   - Long query protection (>10 chars): Requires 60% continuous substring overlap (LCS algorithm)
#   - Prevents unrelated matches (e.g., "dd-trace-js" won't match "dd-trace-dotnet")
# - Frecency scoring: Configurable blend (default: 50% frequency, 30% recency, 20% distance)
# - Distance scoring: Parent (0.9), Child (0.85-0.5), Sibling (0.7), Ancestor (0.6-0.1)
# - Tab completion display (module/Functions/PCD.ps1:183-228):
#   - CompletionText: Relative paths with .\ prefix for child dirs, ..\ for siblings, absolute for others
#   - ListItemText: Clean names (e.g., "Screenshots" not ".\Screenshots\")
#   - Single quotes for paths with spaces, platform-appropriate separators
# - Path deduplication: Uses normalized absolute paths (with trailing \)
# - Existence checking: Filters out learned paths that no longer exist (both tab and predictor)
# - Trailing separator: All directory paths end with \ (both tab completion and predictor)

# Debugging & Testing
Test-PSCueCompletion -InputString <string>         # Test completions
Get-PSCueModuleInfo [-AsJson]                      # Module diagnostics

# See docs/DATABASE-FUNCTIONS.md for detailed database query examples
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

**Git Completion Features**:
- Hardcoded subcommands with detailed tooltips (add, commit, branch, etc.)
- Dynamic git aliases loaded via `git --list-cmds=alias` (tooltips show "Git alias")
- All git main commands via `git --list-cmds=main,nohelpers` (tooltips show "Git command")
- Git extensions from PATH via `git --list-cmds=others` (tooltips show "Git extension")

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
6. **Initialize methods must record baseline**: When adding new `Initialize*` methods in ArgumentGraph (used by PersistenceManager during load), always record the loaded values in `_baseline` dictionary. Without baseline tracking, delta calculations will return full counts, causing values to be added again on save, leading to exponential growth and eventual overflow.
7. **PCD exact match not first**: When frecency scoring dominates match quality, use a boost multiplier for exact matches. Small match score components (0.1) can be overwhelmed by frequency/recency scores. Solution: Apply 100× boost for exact matches (matchScore >= 1.0) to ensure they always rank first.
8. **Inconsistent trailing separators**: Directory completions must have trailing separators in BOTH tab completion (ArgumentCompleter) and inline predictions (ICommandPredictor). Check both code paths when modifying directory suggestion logic.
9. **Path normalization requires workingDirectory**: When calling `ArgumentGraph.RecordUsage()` for navigation commands (cd, sl, chdir), MUST provide the `workingDirectory` parameter. If null/empty, path normalization (including symlink resolution) is skipped. This causes duplicate entries for symlinked paths. Always pass a valid working directory for proper deduplication.
10. **PCD best-match returns 0 suggestions**: If `GetSuggestions()` returns no matches, check that `GetLearnedDirectories()` is requesting enough paths from ArgumentGraph. The default pool size should be 200+ to ensure less-frequently-used directories are searchable. Also verify `CalculateMatchScore()` checks BOTH full paths and directory names.
11. **PCD attempts Set-Location on non-existent path**: Always loop through ALL suggestions and verify existence before navigation. Never call `Set-Location` on paths that don't exist - show helpful error messages instead. This handles race conditions and stale database entries gracefully.
12. **PCD tab completion behavior**: CompletionText must match native cd exactly - use .\ prefix for child directories, ..\ for siblings, and single quotes for spaces. ListItemText should be clean (no prefixes/separators/quotes). See module/Functions/PCD.ps1:183-228 for implementation.
13. **Testing with non-existent paths**: Use `skipExistenceCheck: true` parameter in `PcdCompletionEngine.GetSuggestions()` when testing with mock/non-existent paths. Production code filters non-existent paths by default.
14. **Release builds missing dependencies**: The release workflow (.github/workflows/release.yml) MUST use `dotnet publish` (not `dotnet build`) for PSCue.Module to include all dependencies, especially the `runtimes/` directory with native SQLite libraries. `dotnet build` only outputs primary assemblies, while `dotnet publish` creates a complete deployable package. The remote install script (install-remote.ps1) recursively copies all directories from the release archive to handle `runtimes/`, `Functions/`, and any future subdirectories.
15. **install-local.ps1 dependency list**: The local install script has a hardcoded `$Dependencies` array listing DLLs to copy from the `publish/` directory. When adding new NuGet packages (e.g., Spectre.Console), you MUST add the DLL to this list or the module will fail at runtime with assembly-not-found errors. Consider replacing this with a bulk copy approach (like `install-remote.ps1` does) to avoid this pitfall.
16. **PCD interactive selector excludes current dir and `..`**: `PcdInteractiveSelector` explicitly filters out the current directory and the `..` parent shortcut. This is intentional — navigating to where you already are is useless, and `..` is always available via `pcd ..` directly. When testing, ensure the learned paths are not just the current directory or its parent.
17. **FeedbackProvider uses PowerShell `$PWD` for path normalization**: The `FeedbackProvider` uses the PowerShell `$PWD` variable (not `System.Environment.CurrentDirectory`) to get the current working directory for path normalization. This is important because `Set-Location` in PowerShell does not update the process CWD. Always use `PSCmdlet.SessionState.Path.CurrentLocation` or invoke `$PWD` via PowerShell when you need the true PowerShell working directory.
18. **Navigation timestamp tracking**: For navigation commands (cd, Set-Location, sl, chdir), the FeedbackProvider records the absolute destination path (from context.CurrentLocation after navigation) with trailing separator, not the relative path typed. The `pcd` function manually records navigations under the 'cd' command since FeedbackProvider only sees the 'pcd' command, not the internal Set-Location calls. Without this, pcd navigations wouldn't update learned directory timestamps.

## Documentation
- **Implementation status**:
  - Active work: See `TODO.md` (includes detailed Phase 18 workflow improvements roadmap)
  - Completed phases: See `docs/COMPLETED.md` (Phases 1-21 archived, includes all PCD quality improvements)
- **Database functions**: See `docs/DATABASE-FUNCTIONS.md` for detailed SQLite query examples and schema
- **Troubleshooting**: See `docs/TROUBLESHOOTING.md` for common issues and solutions
- Bug fix history: See git log and commit messages
- API docs: [ICommandPredictor](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor), [IFeedbackProvider](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider)

## Configuration (Environment Variables)
```powershell
# Custom data directory (database location)
# Useful for dev/test isolation from the production install
$env:PSCUE_DATA_DIR = "D:\temp\PSCue-dev\data"  # Database created at <dir>/learned-data.db

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

# Partial command predictions (Phase 17.8: Frequency-based command suggestions)
$env:PSCUE_PARTIAL_COMMAND_PREDICTIONS = "true"  # Enable partial command predictions (default: true)

# PCD (Smart Directory Navigation) configuration (Phases 17.5-17.9 + 19.0 + 21.2 + 21.3 + 21.4)
$env:PSCUE_PCD_FREQUENCY_WEIGHT = "0.5"          # Frecency scoring: frequency weight (default: 0.5)
$env:PSCUE_PCD_RECENCY_WEIGHT = "0.3"            # Frecency scoring: recency weight (default: 0.3)
$env:PSCUE_PCD_DISTANCE_WEIGHT = "0.2"           # Frecency scoring: distance weight (default: 0.2)
$env:PSCUE_PCD_MAX_DEPTH = "3"                   # Recursive search depth for tab completion (default: 3)
$env:PSCUE_PCD_PREDICTOR_MAX_DEPTH = "1"         # Recursive search depth for inline predictor (default: 1)
$env:PSCUE_PCD_RECURSIVE_SEARCH = "true"         # Enable recursive filesystem search (default: true)
$env:PSCUE_PCD_ENABLE_DOT_DIR_FILTER = "true"    # Filter cache/metadata directories (default: true)
$env:PSCUE_PCD_CUSTOM_BLOCKLIST = ".myapp,temp"  # Additional patterns to filter (comma-separated)
$env:PSCUE_PCD_EXACT_MATCH_BOOST = "100.0"       # Score multiplier for exact matches (default: 100.0)
$env:PSCUE_PCD_FUZZY_MIN_MATCH_PCT = "0.7"       # Minimum similarity for fuzzy matching (default: 0.7 = 70%)

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
- **Installation**: `irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex`
- **Release Assets**: PSCue-win-x64.zip, PSCue-linux-x64.tar.gz, checksums.txt
- **Key Features**: Multi-word predictions, dynamic workflow learning, smart directory navigation (pcd)

# Misc
- When adding support for new commands, add the completer registration in module/PSCue.psm1 as well
- when running ./install-local.ps1, always use -Force
- don't reference phases in code, e.g. "// Phase 20: Parse command to understand parameter-value context" or "// Add multi-word suggestions (Phase 17.4)"
