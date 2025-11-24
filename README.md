# PSCue

[![CI](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml/badge.svg)](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml)

**PSCue** is a unified PowerShell module that provides intelligent command-line completion and prediction for PowerShell Core (7.2+). It combines fast Tab completion with inline command suggestions to enhance your PowerShell experience.

## Features

- **🚀 Fast Tab Completion**: Native AOT executable for <10ms startup time
- **💡 Inline Predictions**: Smart command suggestions as you type using `ICommandPredictor`
- **🎯 Multi-Word Suggestions**: Shows common argument combinations (e.g., `git checkout master`)
- **🤖 ML-Based Predictions**: N-gram sequence learning predicts your next command (e.g., `git add` → `git commit`)
- **🔄 Workflow Learning**: Automatically learns command sequences and predicts next command based on your usage patterns
- **📁 Smart Directory Navigation**: `pcd` command with intelligent tab completion, fuzzy matching, and exact match prioritization
- **🔗 Symlink Resolution**: Automatically resolves symlinks, junctions, and directory links to prevent duplicate suggestions
- **🧹 Smart Filtering**: Filters cache/metadata directories (`.codeium`, `node_modules`, `bin`, etc.) for cleaner suggestions
- **⚡ PowerShell Module Functions**: Native PowerShell functions for learning, database, workflow, and navigation management
- **🧠 Universal Learning System**: Learns from ALL commands (not just pre-configured ones) and adapts to your workflow patterns
- **🔒 Privacy & Security**: Built-in sensitive data detection - never learns commands with API keys, passwords, or tokens
- **💾 Cross-Session Persistence**: Learning data persists across PowerShell sessions using SQLite with concurrent session support
- **🎯 Context-Aware Suggestions**: Detects command sequences and boosts relevant suggestions based on recent activity
- **⏱️ Time-Aware Predictions**: Adjusts workflow suggestions based on typical timing between commands
- **⚡ High Performance**: <1ms overhead for learning, <20ms total prediction time (within PowerShell's timeout)
- **🆘 Error Suggestions**: Provides helpful recovery suggestions when commands fail (e.g., git errors)
- **🔌 Cross-platform**: Windows (x64) and Linux (x64) support
- **📦 Zero Configuration**: Works out of the box after installation

### Two-Component Design

PSCue uses a dual-architecture approach for optimal performance:

1. **ArgumentCompleter** (`pscue-completer.exe`) - NativeAOT executable for instant Tab completion
2. **CommandPredictor** (`PSCue.Module.dll`) - Long-lived managed DLL for inline suggestions

This architecture enables:
- Sub-10ms Tab completion response time
- Fresh completions computed on every Tab press (includes dynamic data like git branches)
- Inline predictions learn from your command history
- Learning feedback loop via `IFeedbackProvider` (PowerShell 7.4+)
- Error recovery suggestions when commands fail
- Native PowerShell functions for direct module access (no external tools needed)

## Supported Commands

### Explicit Completions (Pre-configured)

PSCue provides detailed completions for these commands:

- **Git**: `git` - branches, tags, remotes, files, subcommands
- **GitHub CLI**: `gh` - repos, PRs, issues, workflows
- **Graphite CLI**: `gt` - stacked PRs, branch navigation, workflows
- **Azure CLI**: `az` - resource groups, subscriptions, commands
- **Azure Developer CLI**: `azd` - environments, services
- **Azure Functions**: `func` - function apps, deployment
- **VS Code**: `code` - workspaces, extensions, files
- **Scoop** (Windows): `scoop` - apps, buckets
- **Winget** (Windows): `winget` - packages, sources
- **Windows Terminal** (Windows): `wt` - tabs, panes, profiles
- **Chezmoi**: `chezmoi` - dotfile management commands
- **Tree alternatives**: `tre`, `lsd` - directory navigation
- **Disk usage**: `dust` - directory analysis
- **Navigation**: `cd`, `Set-Location`, `sl`, `chdir` - directory completion with smart caching

### Universal Learning

**NEW**: PSCue now learns from ANY command you use, even those not explicitly supported:

- **kubectl**, **docker**, **cargo**, **npm**, **dotnet**, **go**, **terraform**, and hundreds more
- Tracks which flags and arguments you use most frequently
- **Multi-word suggestions**: Shows common argument combinations (e.g., `git checkout master`, `docker run -it`)
- **ML-based command sequence prediction**: Learns which commands you typically run after others (e.g., `git add` → `git commit`)
- **Workflow learning**: Automatically learns command sequences and predicts next command with timing-aware confidence
- Detects command workflows and suggests next steps based on n-gram analysis
- Provides context-aware suggestions based on recent activity
- Fully automatic - no configuration needed

**Examples**:
- **Argument Learning**: After running `kubectl get pods`, `kubectl describe pod`, PSCue learns these patterns and suggests them next time you type `kubectl`
- **Multi-word**: After frequently running `git checkout master`, PSCue suggests the full `checkout master` combination alongside `checkout`
- **ML Prediction**: After typing `git add file.txt`, PSCue's ML engine predicts you'll likely run `git commit` next based on your historical command sequences
- **Workflow Prediction**: After running `cargo build` → `cargo test` 10+ times, PSCue predicts `cargo test` when you finish `cargo build`

## Installation

### From GitHub Releases (Recommended)

One-line installation from the latest release:

```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex
```

Install a specific version:

```powershell
$version = "1.0.0"; irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex
```

### From Source

Build and install from source (requires .NET 9.0 SDK):

```powershell
git clone https://github.com/lucaspimentel/PSCue.git
cd PSCue
./install-local.ps1
```

Both methods install to `~/.local/pwsh-modules/PSCue/`

## Setup

After installation, add these lines to your PowerShell profile (`$PROFILE`):

```powershell
# Import PSCue module
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1

# Enable inline predictions (combine history + PSCue suggestions)
Set-PSReadLineOption -PredictionSource HistoryAndPlugin
```

Then restart PowerShell or reload your profile:

```powershell
. $PROFILE
```

## Usage Examples

### Tab Completion

Press `Tab` after typing a partial command to cycle through completions:

```powershell
git checkout ma<Tab>    # Completes to branch names: main, master, etc.
gt create <Tab>         # Shows Graphite create options: --all, --message, etc.
scoop install no<Tab>   # Completes to: nodejs, notepadplusplus, etc.
az group list --<Tab>   # Shows available flags: --output, --query, etc.
cd src<Tab>             # Completes to subdirectories: src/, srcBackup/, etc.
cd ../<Tab>             # Shows sibling directories (parent's subdirectories)
cd ~/<Tab>              # Shows home directory subdirectories
```

### Smart Directory Navigation with `pcd`

**NEW**: PSCue includes an enhanced smart directory navigation command that learns from your `cd` usage with powerful fuzzy matching and intelligent scoring:

```powershell
pcd datadog<Tab>        # Shows learned directories with fuzzy matching
pcd datadog             # Best-match navigation: finds "D:\source\datadog" even without Tab
pcd ~                   # Home directory (well-known shortcut)
pcd ..                  # Parent directory (well-known shortcut)
```

The `pcd` (PowerShell Change Directory) command provides:

**Core Features**:
- **Native cd behavior**: Tab completion matches PowerShell's native `cd` exactly
  - Inserts: `.\childdir\` or `'.\dir with spaces\'` (with `.\ ` prefix, trailing separator, single quotes)
  - Displays: Clean directory names without prefixes/separators/quotes
  - Uses platform-appropriate separators (`\` on Windows, `/` on Unix)
- **Inline predictions**: See directory suggestions as you type (like other commands)
- **Directory name matching**: Type just the directory name to navigate from anywhere (e.g., `pcd dd-trace-dotnet` finds `D:\source\datadog\dd-trace-dotnet` from any location)
- **Exact match prioritization**: Exact directory name matches always rank first (100× boost by default)
- **Smart fuzzy matching**: Find directories with typos while rejecting unrelated matches (70% minimum similarity, LCS check for long queries)
- **Recursive filesystem search**: Always enabled for thorough discovery (depth-controlled for performance)
- **Smart filtering**:
  - Excludes non-existent directories (both tab and predictor)
  - Filters cache/metadata directories (`.codeium`, `.claude`, `.dotnet`, `node_modules`, `bin`, `obj`, etc.)
  - Explicit typing overrides filter (typing `.claude` shows `.claude/` directories)
- **Symlink resolution**: Automatically resolves symlinks, junctions, and directory links to prevent duplicates
- **Well-known shortcuts**: Instant access to `~` (home), `..` (parent) - only suggested for relative paths
- **Frecency scoring**: Balances frequency + recency + distance for better suggestions
- **Distance scoring**: Prefers directories near your current location
- **Best-match navigation**: `pcd datadog` automatically finds best match if exact path doesn't exist
  - Searches top 200 learned paths for comprehensive coverage
  - Tries all suggestions in order until finding one that exists (handles stale data)
  - Shows helpful error messages instead of PowerShell errors for non-existent paths
- **Path normalization**: All paths include trailing `\` to match PowerShell's native behavior

**Advanced Scoring Algorithm**:
- **Match quality** (10%): Exact > Prefix > Fuzzy matching
- **Frequency** (50%): How often you visit this directory
- **Recency** (30%): When you last visited (exponential decay)
- **Distance** (20%): Proximity to current directory (parent/child/sibling)

**Configuration** via environment variables:
```powershell
# Scoring weights (customize to your workflow)
$env:PSCUE_PCD_FREQUENCY_WEIGHT = "0.5"  # Default: 50% weight
$env:PSCUE_PCD_RECENCY_WEIGHT = "0.3"    # Default: 30% weight
$env:PSCUE_PCD_DISTANCE_WEIGHT = "0.2"   # Default: 20% weight

# Exact match boost (ensures exact matches rank first)
$env:PSCUE_PCD_EXACT_MATCH_BOOST = "100.0"       # Default: 100× multiplier for exact matches

# Fuzzy matching quality (prevents unrelated matches like dd-trace-js matching dd-trace-dotnet)
$env:PSCUE_PCD_FUZZY_MIN_MATCH_PCT = "0.7"       # Default: 70% similarity required (0.5-0.9 range)

# Recursive filesystem search (always enabled when true, depth-controlled)
$env:PSCUE_PCD_RECURSIVE_SEARCH = "true"         # Default: true (set to false to disable)
$env:PSCUE_PCD_MAX_DEPTH = "3"                   # Tab completion depth (default: 3)
$env:PSCUE_PCD_PREDICTOR_MAX_DEPTH = "1"         # Inline predictor depth (default: 1, faster)

# Cache/metadata directory filtering (filters noise like .codeium, node_modules, bin, obj)
$env:PSCUE_PCD_ENABLE_DOT_DIR_FILTER = "true"    # Default: true (set to false to disable)
$env:PSCUE_PCD_CUSTOM_BLOCKLIST = ".myapp,temp"  # Optional: additional patterns to filter (comma-separated)
```

Example workflows:
```powershell
# Normal navigation - PSCue learns your patterns
cd D:\source\datadog\dd-trace-dotnet
cd D:\source\lucaspimentel\PSCue

# Inline predictions (as you type)
pcd                     # Shows inline suggestions: ../datadog, ./src, etc.
pcd d                   # Filters suggestions as you type

# Smart tab completion with fuzzy matching (matches native cd behavior)
pcd dat<Tab>            # Completes: pcd ..\datadog\     (shown in list: "datadog")
pcd src<Tab>            # Completes: pcd .\src\          (shown in list: "src")
pcd 'dir with spaces'<Tab> # Completes: pcd '.\dir with spaces\' (single quotes, shown: "dir with spaces")
pcd trace<Tab>          # Suggests: dd-trace-dotnet (substring match)
pcd datdog<Tab>         # Suggests: ../datadog (fuzzy match, typo tolerant)

# Filesystem discovery
pcd newfolder<Tab>      # Shows unlearned directories via recursive search (depth=3 for tab)
pcd doc<Tab>                  # Shows relative: ../documentation

# Unlearned directories appear via filesystem search
pcd D:\source\datadog\t<Tab>  # Shows: D:\source\datadog\toaster\ (even if never visited)

# Best-match navigation (no Tab needed!)
pcd datadog             # Navigates to "D:\source\datadog" automatically
                        # Shows: "No exact match, navigating to: D:\source\datadog"

# Directory name matching - navigate from anywhere!
cd C:\Users\Lucas.Pimentel
pcd dd-trace-dotnet     # Finds "D:\source\datadog\dd-trace-dotnet" even from different drive/location
                        # Matches directory name regardless of full path

# Robustness - helpful errors instead of crashes
pcd nonexistent-project # Shows: "No learned directory matches 'nonexistent-project'."
                        #         "Tip: Navigate to directories to teach PSCue, or use tab completion."

# Well-known shortcuts (highest priority)
pcd ~                   # Home directory
pcd ..                  # Parent directory
```

**Note**: Full paths are still shown in tooltips for reference.

**Performance**: Tab completion <10ms, Best-match resolution <50ms

### Inline Predictions

Type commands and see suggestions appear in gray text (powered by `ICommandPredictor`):

```powershell
git commit              # Suggests: -m "message" (based on history)
gh pr create            # Suggests: --title "..." --body "..."
pcd                     # Suggests: ../datadog, ./src, etc. (learned directories)
```

Press `→` (right arrow) to accept the suggestion.

### Learning System & Error Suggestions (PowerShell 7.4+)

PSCue learns from your command usage and provides helpful suggestions when commands fail:

**Silent Learning**: When you successfully execute commands, PSCue automatically increases priority scores for the flags and options you use most frequently.

**Error Recovery**: When commands fail, PSCue provides contextual suggestions. For example, if a git command fails:
```powershell
git checkout nonexistent-branch
# PSCue suggests:
# 💡 List all branches: git branch -a
# 💡 Create and switch to branch: git checkout -b <name>
```

**Requirements**: PowerShell 7.4+ with `PSFeedbackProvider` experimental feature enabled (see setup instructions below).

## Privacy & Security

PSCue protects your sensitive data with multi-layered filtering:

### Built-in Protection (Always Active)

Commands containing sensitive keywords are **never learned or stored**:
- Passwords: `*password*`, `*passwd*`
- API Keys: `*api*key*`, `*secret*`
- Tokens: `*token*`, `*oauth*`, `*bearer*`
- Credentials: `*credentials*`, `*private*key*`

### Heuristic Detection (Always Active)

PSCue automatically detects and ignores commands with:
- **GitHub/Stripe keys**: `sk_`, `pk_`, `ghp_`, `gho_`, etc.
- **AWS access keys**: `AKIA...`
- **JWT tokens**: `eyJ...`
- **Bearer tokens**: `Bearer ...`
- **Long secrets**: Base64/hex strings (40+ characters, outside quotes)

### Examples of Protected Commands

```powershell
# These commands will NEVER be learned:
export API_KEY=sk_test_1234567890...           # Stripe key detected
aws configure set aws_secret_access_key ...    # "secret" keyword
gh auth login ghp_abcdef123456...              # GitHub token pattern
curl -H "Authorization: Bearer eyJ..."         # JWT token pattern
git commit -m "password is hunter2"            # "password" keyword
```

### Custom Filtering

Add your own patterns via environment variable:

```powershell
# In your PowerShell profile:
$env:PSCUE_IGNORE_PATTERNS = "aws *,terraform *,*mycompany*"
```

Patterns use wildcards: `*` matches any characters.

**Note**: Built-in patterns cannot be disabled to ensure baseline privacy protection.

## Architecture

PSCue uses a two-component architecture optimized for both speed and intelligence.

> **For detailed technical information**, including caching strategy and implementation notes, see [TECHNICAL_DETAILS.md](docs/TECHNICAL_DETAILS.md).

### ArgumentCompleter (Short-lived)
- **Binary**: `pscue-completer.exe` (NativeAOT)
- **Purpose**: Handles Tab completion via `Register-ArgumentCompleter`
- **Lifetime**: Launches on each Tab press (~10ms startup)
- **Features**: Fast, standalone, computes completions locally with full dynamic arguments support (git branches, scoop packages, etc.)

### CommandPredictor (Long-lived)
- **Binary**: `PSCue.Module.dll` (Managed)
- **Purpose**: Provides inline suggestions via `ICommandPredictor`
- **Lifetime**: Loaded once with PowerShell module
- **Features**: Learning system (ArgumentGraph + CommandHistory), PowerShell module functions, context-aware predictions

### Shared Completion Logic
- **Binary**: `PSCue.Shared.dll` (Managed)
- **Purpose**: Contains all command completion logic
- **Used by**: Both ArgumentCompleter (via NativeAOT compilation) and CommandPredictor
- **Benefits**: Consistent suggestions, easier maintenance, avoids NativeAOT reference issues

```
┌─────────────────────────────────────┐
│  PowerShell Session                 │
├─────────────────────────────────────┤
│  PSCue.Module.dll                   │
│  - ICommandPredictor (suggestions)  │
│  - IFeedbackProvider (learning)     │
│  - PowerShell Functions             │
│  - ArgumentGraph (learning)         │
│  - CommandHistory (recent cmds)     │
│  - SQLite persistence               │
│  - Uses PSCue.Shared.dll            │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  Tab Completion (independent)       │
├─────────────────────────────────────┤
│  pscue-completer.exe                │
│  - Fast startup (<10ms)             │
│  - Fresh computation each time      │
│  - Uses PSCue.Shared.dll (compiled) │
│  - Includes dynamic args (full)     │
└─────────────────────────────────────┘
```

## Requirements

- **PowerShell**: 7.2 or later (Core only, not Windows PowerShell 5.1)
  - **7.4+ recommended** for learning features (`IFeedbackProvider`)
- **Operating System**:
  - Windows x64
  - macOS arm64 (Apple Silicon)
  - Linux x64

### Learning Features (PowerShell 7.4+)

PSCue includes an optional learning system that improves suggestions based on your usage patterns. This requires:

1. **PowerShell 7.4 or higher**
2. **PSFeedbackProvider experimental feature enabled**:
   ```powershell
   Enable-ExperimentalFeature -Name PSFeedbackProvider
   # Restart PowerShell after enabling
   ```

The learning system features:
- **Silent Learning**: Observes commands you execute successfully
- **Usage Tracking**: Increases priority scores for frequently-used completions
- **Personalization**: Makes your most-used options appear first in suggestions
- **Cross-Session Persistence**: Learning data persists across PowerShell sessions (stored in SQLite)
- **Multi-Session Safe**: Multiple PowerShell sessions can run concurrently without data loss
- **Error Recovery**: Provides helpful suggestions when commands fail (e.g., git errors)

**Common error suggestions include**:
- "not a git repository" → suggests `git init` or `git clone`
- "pathspec did not match" → suggests checking branches or creating new ones
- "uncommitted changes" → suggests commit, stash, or restore options
- "remote does not exist" → suggests listing or adding remotes
- "permission denied" → suggests SSH key check or HTTPS alternative

**Note**: PSCue works fine on PowerShell 7.2-7.3 without the learning features. The FeedbackProvider will simply not register on older versions.

## Comparison with Original Projects

PSCue consolidates and enhances two existing projects:

- **[pwsh-argument-completer](https://github.com/lucaspimentel/pwsh-argument-completer)** - Tab completion executable
- **[pwsh-command-predictor](https://github.com/lucaspimentel/pwsh-command-predictor)** - Inline prediction module

### Key Improvements

1. **Unified Module**: Single installation and configuration
2. **Consistent Completions**: Tab and inline suggestions use the same logic
3. **Better Performance**: Optimized build settings and caching strategy
4. **Universal Learning**: Learns from ANY command, not just pre-configured ones
5. **Cross-Session Persistence**: Learning data saved to SQLite, survives PowerShell restarts
6. **Concurrent Sessions**: Multiple PowerShell sessions share learned data safely
7. **CI/CD Pipeline**: Automated testing and releases for all platforms

### Migration from Original Projects

If you're using the original projects, you can switch to PSCue:

```powershell
# Uninstall old modules (if installed)
Remove-Module PowerShellArgumentCompleter -ErrorAction SilentlyContinue
Remove-Module PowerShellPredictor -ErrorAction SilentlyContinue

# Install PSCue
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex

# Update your $PROFILE to use PSCue instead
```

## Development

### Building from Source

```powershell
# Restore dependencies
dotnet restore

# Build ArgumentCompleter (NativeAOT)
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Build CommandPredictor (Managed DLL)
dotnet build src/PSCue.Module/ -c Release

# Run tests
dotnet test
```

### Running Tests

PSCue has comprehensive test coverage across ArgumentCompleter logic, CommandPredictor, FeedbackProvider, generic learning components, ML prediction, persistence, navigation, and integration scenarios.

```powershell
# All tests
dotnet test

# Specific project
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/

# Run ML prediction tests specifically
dotnet test --filter "FullyQualifiedName~SequencePredictor"

# With verbose output
dotnet test --logger "console;verbosity=detailed"

# Run integration tests for filtering scenarios
pwsh -NoProfile -File test/test-scripts/test-completion-filtering.ps1 all
```

### Testing Completions Manually

Use the **PowerShell module functions** for testing and debugging:

```powershell
Import-Module ./module/PSCue.psd1

# Test completion generation
Test-PSCueCompletion -InputString "git checkout ma"

# View learning data
Get-PSCueLearning -Command git
Get-PSCueDatabaseHistory -Command git -Last 10

# Get module diagnostics
Get-PSCueModuleInfo

# Or test Tab completion directly
TabExpansion2 'git checkout ma' 15
```

## Roadmap

### Current Status ✅

- [x] Tab completion working (ArgumentCompleter)
- [x] Inline predictions working (CommandPredictor)
- [x] Shared completion logic (PSCue.Shared)
- [x] Multi-platform CI/CD (Windows x64, Linux x64)
- [x] Comprehensive documentation
- [x] **Simplified Architecture** (Phase 16, completed 2025-01-30)
  - Removed IPC layer entirely (no longer needed)
  - ArgumentCompleter computes locally with full dynamic arguments
  - CommandPredictor uses direct in-process access
  - Simpler, faster, less code to maintain
- [x] **Learning System & Error Suggestions**
  - Full `IFeedbackProvider` implementation (PowerShell 7.4+)
  - Usage tracking and priority scoring
  - Personalized completions based on command history
  - Error recovery suggestions for git commands
- [x] **PowerShell Module Functions** (Phase 16)
  - 12 native PowerShell functions for learning, database, and workflow management
  - Direct in-process access (no external tools needed)
  - Pipeline support, tab completion, comprehensive help
  - Functions: Get-PSCueLearning, Clear-PSCueLearning, Export-PSCueLearning, Import-PSCueLearning, Save-PSCueLearning, Get-PSCueDatabaseStats, Get-PSCueDatabaseHistory, Get-PSCueWorkflows, Get-PSCueWorkflowStats, Clear-PSCueWorkflows, Export-PSCueWorkflows, Import-PSCueWorkflows, Test-PSCueCompletion, Get-PSCueModuleInfo

- [x] **Generic Command Learning** ✅ **COMPLETE**
  - Universal command learning (learns from ALL commands, not just pre-configured ones)
  - Enhanced learning algorithms (frequency × recency scoring: 60% frequency + 40% recency)
  - Context-aware suggestions based on recent command history
  - Command sequence detection for workflows (git add → commit → push, docker build → run, etc.)
  - Privacy controls via `PSCUE_IGNORE_PATTERNS` environment variable
  - Components: CommandHistory (ring buffer), ArgumentGraph (knowledge graph), ContextAnalyzer, GenericPredictor, Hybrid CommandPredictor
  - Comprehensive test coverage for all learning components

- **Test Coverage Improvements**
  - Comprehensive tests for critical components (CommandPredictor, FeedbackProvider)
  - Fixed the "pluginstall" bug with thorough CommandPredictor.Combine tests
  - Extensive FeedbackProvider tests covering command parsing, privacy filtering, and learning integration
  - Uses reflection to properly test internal PowerShell SDK components
  - High coverage for CommandPredictor and FeedbackProvider
  - Removed 44 IPC tests (Phase 16.7 - IPC layer removed)

### Configuration

```powershell
# Disable generic learning entirely
$env:PSCUE_DISABLE_LEARNING = "true"

# Learning configuration (defaults shown)
$env:PSCUE_HISTORY_SIZE = "100"          # Command history size
$env:PSCUE_MAX_COMMANDS = "500"          # Max commands to track
$env:PSCUE_MAX_ARGS_PER_CMD = "100"      # Max arguments per command
$env:PSCUE_DECAY_DAYS = "30"             # Score decay period (days)

# ML prediction configuration (enabled by default)
$env:PSCUE_ML_ENABLED = "true"           # Enable ML sequence predictions
$env:PSCUE_ML_NGRAM_ORDER = "2"          # N-gram order: 2=bigrams, 3=trigrams
$env:PSCUE_ML_NGRAM_MIN_FREQ = "3"       # Minimum frequency to suggest (occurrences)

# Workflow learning configuration (enabled by default)
$env:PSCUE_WORKFLOW_LEARNING = "true"            # Enable workflow learning
$env:PSCUE_WORKFLOW_MIN_FREQUENCY = "5"          # Min occurrences to suggest
$env:PSCUE_WORKFLOW_MAX_TIME_DELTA = "15"        # Max minutes between commands
$env:PSCUE_WORKFLOW_MIN_CONFIDENCE = "0.6"       # Min confidence threshold

# Privacy: ignore sensitive commands (comma-separated wildcards)
$env:PSCUE_IGNORE_PATTERNS = "aws *,*secret*,*password*"
```

**ML Prediction Performance:**
- Cache lookup: <1ms (fits within 20ms PowerShell prediction timeout)
- Total prediction with ML: <20ms (validated via performance tests)
- Memory usage: <5MB for 10,000 learned sequences
- Cross-session persistence: SQLite with additive merging for concurrent sessions

### Future Work

- **Distribution**
  - PowerShell Gallery publishing
  - Scoop package manager (Windows)
  - .editorconfig + format checking in CI
- **Advanced Features**
  - Error suggestions for more commands (gh, az, scoop)
  - Advanced ML with semantic embeddings (ONNX Runtime)
  - macOS support (currently Windows x64 and Linux x64 only)

See [TODO.md](TODO.md) for detailed implementation plan.

## Contributing

Contributions are welcome! Please feel free to:

- Report bugs via [GitHub Issues](https://github.com/lucaspimentel/PSCue/issues)
- Submit pull requests for bug fixes or features
- Suggest new command completions
- Improve documentation

### Adding New Command Completions

To add completions for a new command:

1. Create a new file in `src/PSCue.Shared/KnownCompletions/YourCommand.cs`
2. Implement the `ICompletion` interface
3. Add the command to `CommandCompleter.cs` switch statement
4. Add tests in `test/PSCue.ArgumentCompleter.Tests/`
5. Register the completer in `module/PSCue.psm1`

See existing completions like `GitCommand.cs` or `ScoopCommand.cs` for examples in `src/PSCue.Shared/KnownCompletions/`.

## Learning & Database Management

PSCue includes PowerShell functions for managing the learning system and inspecting persisted data:

### Learning System Management

```powershell
# View learned data (in-memory ArgumentGraph)
Get-PSCueLearning                    # Show all learned commands
Get-PSCueLearning -Command kubectl   # Filter by specific command
Get-PSCueLearning -AsJson            # Output as JSON

# Export/Import learned data (for backup or sharing across machines)
Export-PSCueLearning -Path ~/pscue-backup.json
Import-PSCueLearning -Path ~/pscue-backup.json           # Replace current data
Import-PSCueLearning -Path ~/pscue-backup.json -Merge    # Merge with current data

# Force save to disk (bypasses 5-minute auto-save timer)
Save-PSCueLearning

# Clear all learned data (memory + database)
Clear-PSCueLearning                  # Interactive confirmation (ConfirmImpact=High)
Clear-PSCueLearning -Confirm:$false  # Skip confirmation
Clear-PSCueLearning -Force           # Force delete database even if PSCue isn't initialized (recovery mode)
```

### Database Management (Direct SQLite Queries)

PSCue now includes functions to directly query the SQLite database, allowing you to inspect what's actually persisted on disk vs. what's in memory:

```powershell
# View database statistics
Get-PSCueDatabaseStats              # Show totals and top commands
Get-PSCueDatabaseStats -Detailed    # Show per-command stats with top arguments
Get-PSCueDatabaseStats -AsJson      # JSON output for scripting

# Query command history
Get-PSCueDatabaseHistory            # Show last 20 history entries
Get-PSCueDatabaseHistory -Last 50   # Show last 50 entries
Get-PSCueDatabaseHistory -Command "git"  # Filter by command name
Get-PSCueDatabaseHistory -AsJson    # JSON output

# Compare in-memory vs database (useful for debugging)
$inMemory = (Get-PSCueLearning).Count
$inDb = (Get-PSCueDatabaseStats).TotalCommands
Write-Host "In memory: $inMemory | In database: $inDb"
```

**Why They Might Differ:**
- Auto-save runs every 5 minutes
- Module just loaded (data not synced yet)
- Run `Save-PSCueLearning` to sync immediately

**Database Location:**
- Windows: `%LOCALAPPDATA%\PSCue\learned-data.db`
- Linux: `~/.local/share/PSCue/learned-data.db`

For detailed documentation on database functions, schema, and use cases, see [DATABASE-FUNCTIONS.md](docs/DATABASE-FUNCTIONS.md).

### Workflow Management

PSCue automatically learns command workflow patterns (which commands you typically run after others) and uses this to predict your next command:

```powershell
# View learned workflows
Get-PSCueWorkflows                      # Show all learned workflows
Get-PSCueWorkflows -Command "git add"   # Filter workflows starting from specific command
Get-PSCueWorkflows -AsJson              # Output as JSON

# View workflow statistics
Get-PSCueWorkflowStats                  # Summary: total transitions, unique commands
Get-PSCueWorkflowStats -Detailed        # Include top 10 workflows and database size
Get-PSCueWorkflowStats -AsJson          # JSON output

# Export/Import workflows (for backup or sharing)
Export-PSCueWorkflows -Path ~/workflows.json
Import-PSCueWorkflows -Path ~/workflows.json         # Replace current workflows
Import-PSCueWorkflows -Path ~/workflows.json -Merge  # Merge with existing workflows

# Clear workflow data
Clear-PSCueWorkflows                    # Interactive confirmation (ConfirmImpact=High)
Clear-PSCueWorkflows -Confirm:$false    # Skip confirmation
Clear-PSCueWorkflows -WhatIf            # Preview what would be cleared
```

**How Workflow Learning Works:**
- Automatically tracks command → next command transitions
- Records frequency and typical timing between commands
- Adjusts predictions based on time since last command
- Only suggests workflows seen 5+ times (configurable via `$env:PSCUE_WORKFLOW_MIN_FREQUENCY`)
- Filters out commands more than 15 minutes apart (configurable via `$env:PSCUE_WORKFLOW_MAX_TIME_DELTA`)

**Example Workflow:**
```powershell
# After running this sequence 10+ times:
cargo build
cargo test
git add .
git commit

# PSCue learns the pattern and predicts:
PS> cargo build<Enter>
PS> cargo █  # Inline suggestion: "cargo test" (based on workflow)

PS> cargo test<Enter>
PS> git █    # Inline suggestion: "git add" (cross-tool workflow)
```

### Debugging & Diagnostics

```powershell
# Test completion generation
Test-PSCueCompletion -InputString "git checkout ma"
Test-PSCueCompletion -InputString "kubectl get " -IncludeTiming

# Module diagnostics
Get-PSCueModuleInfo               # Show version, config, statistics
Get-PSCueModuleInfo -AsJson       # JSON output for scripting
```

All functions support:
- **Tab completion** on parameters
- **`Get-Help`** for detailed documentation and examples
- **Pipeline support** where applicable
- **`-WhatIf` and `-Confirm`** for destructive operations

## Troubleshooting

For comprehensive troubleshooting guidance, see [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

### Tab completions not working

1. Verify module is loaded: `Get-Module PSCue`
2. Check completer registration: `Get-ArgumentCompleter`
3. Test executable directly: `pscue-completer.exe "ma" "git checkout ma" 15`
4. **Enable debug logging** to diagnose issues:
   ```powershell
   $env:PSCUE_DEBUG = "1"
   # Trigger a completion, then check the log
   # Log location: $env:LOCALAPPDATA/PSCue/log.txt (Windows)
   ```
5. Check the log for completion activity - Tab completion always uses local computation

### Database initialization fails

If you see "Failed to initialize generic learning" errors when loading the module:

```powershell
# Force delete the corrupted database (works even when PSCue won't load)
Clear-PSCueLearning -Force

# Then reload the module
Remove-Module PSCue
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1
```

The `-Force` parameter bypasses initialization checks and directly deletes the SQLite database files (including WAL journal files).

### Inline predictions not appearing

1. Verify PSReadLine prediction is enabled:
   ```powershell
   Set-PSReadLineOption -PredictionSource HistoryAndPlugin
   ```
2. Check predictor is registered:
   ```powershell
   Get-PSSubsystem -Kind CommandPredictor | Select-Object -ExpandProperty Implementations
   # Should show: PSCue (01a1e2c5-fbc1-4cf3-8178-ac2e55232434)
   ```
3. Test predictor manually using module functions:
   ```powershell
   Import-Module PSCue
   Test-PSCueCompletion -InputString "git checkout main"
   Get-PSCueModuleInfo  # Check module status
   ```

### Platform-specific issues

- **Linux**: May need to set executable permissions: `chmod +x ~/.local/pwsh-modules/PSCue/pscue-completer`
- **Windows**: If execution policy blocks the module, run: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`
- **macOS**: Not currently supported (Windows x64 and Linux x64 only). You can build from source using `./install-local.ps1`

## License

MIT License - Copyright (c) 2024 Lucas Pimentel

See [LICENSE](LICENSE) file for full details.

## Acknowledgments

PSCue builds upon and consolidates:
- [pwsh-argument-completer](https://github.com/lucaspimentel/pwsh-argument-completer)
- [pwsh-command-predictor](https://github.com/lucaspimentel/pwsh-command-predictor)

Special thanks to the PowerShell team for the `ICommandPredictor` and `IFeedbackProvider` APIs.

## Links

- **Repository**: https://github.com/lucaspimentel/PSCue
- **Issues**: https://github.com/lucaspimentel/PSCue/issues
- **Releases**: https://github.com/lucaspimentel/PSCue/releases
- **Project Documentation**:
  - [TODO.md](TODO.md) - Current work and future plans
  - [COMPLETED.md](docs/COMPLETED.md) - Completed implementation phases
  - [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) - Common issues and solutions
  - [CLAUDE.md](CLAUDE.md) - Quick reference for AI agents
  - [DATABASE-FUNCTIONS.md](docs/DATABASE-FUNCTIONS.md) - Database query functions and schema
- **PowerShell API Documentation**:
  - [ICommandPredictor API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor)
  - [IFeedbackProvider API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider)
  - [Register-ArgumentCompleter](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/register-argumentcompleter)
