# PSCue

[![CI](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml/badge.svg)](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml)

**PSCue** is a unified PowerShell module that provides intelligent command-line completion and prediction for PowerShell Core (7.2+). It combines fast Tab completion with inline command suggestions to enhance your PowerShell experience.

## Features

- **🚀 Fast Tab Completion**: Native AOT executable for <10ms startup time
- **💡 Inline Predictions**: Smart command suggestions as you type using `ICommandPredictor`
- **⚡ IPC Communication**: ArgumentCompleter and CommandPredictor share state via Named Pipes for intelligent caching
- **🧠 Universal Learning System**: Learns from ALL commands (not just pre-configured ones) and adapts to your workflow patterns
- **💾 Cross-Session Persistence**: Learning data persists across PowerShell sessions using SQLite
- **🎯 Context-Aware Suggestions**: Detects command sequences and boosts relevant suggestions based on recent activity
- **🆘 Error Suggestions**: Provides helpful recovery suggestions when commands fail (e.g., git errors)
- **🔌 Cross-platform**: Windows, macOS (Apple Silicon), and Linux support
- **📦 Zero Configuration**: Works out of the box after installation

### Two-Component Design

PSCue uses a dual-architecture approach for optimal performance:

1. **ArgumentCompleter** (`pscue-completer.exe`) - NativeAOT executable for instant Tab completion
2. **CommandPredictor** (`PSCue.Module.dll`) - Long-lived managed DLL for inline suggestions

This architecture enables:
- Sub-10ms Tab completion response time
- IPC-based communication for state sharing and intelligent caching
- Persistent cache across Tab completion requests
- Consistent suggestions between Tab completion and inline predictions
- Graceful fallback to local logic when IPC unavailable
- Learning feedback loop via `IFeedbackProvider` (PowerShell 7.4+)
- Error recovery suggestions when commands fail

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

### Universal Learning (Phase 11)

**NEW**: PSCue now learns from ANY command you use, even those not explicitly supported:

- **kubectl**, **docker**, **cargo**, **npm**, **dotnet**, **go**, **terraform**, and hundreds more
- Tracks which flags and arguments you use most frequently
- Detects command workflows (e.g., docker build → docker run)
- Provides context-aware suggestions based on recent activity
- Fully automatic - no configuration needed

**Example**: Never used kubectl before? After you run `kubectl get pods`, `kubectl describe pod`, etc., PSCue learns these patterns and will suggest them next time you type `kubectl`.

## Installation

### From GitHub Releases (Recommended)

One-line installation from the latest release:

```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

Install a specific version:

```powershell
$version = "1.0.0"; irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

### From Source

Build and install from source (requires .NET 9.0 SDK):

```powershell
git clone https://github.com/lucaspimentel/PSCue.git
cd PSCue

# PowerShell
./scripts/install-local.ps1

# Or from Windows Command Prompt
scripts\install-local.cmd
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

### Inline Predictions

Type commands and see suggestions appear in gray text (powered by `ICommandPredictor`):

```powershell
git commit              # Suggests: -m "message" (based on history)
gh pr create            # Suggests: --title "..." --body "..."
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

## Architecture

PSCue uses a two-component architecture optimized for both speed and intelligence.

> **For detailed technical information**, including IPC protocol details, caching strategy, and implementation notes, see [TECHNICAL_DETAILS.md](TECHNICAL_DETAILS.md).

### ArgumentCompleter (Short-lived)
- **Binary**: `pscue-completer.exe` (NativeAOT)
- **Purpose**: Handles Tab completion via `Register-ArgumentCompleter`
- **Lifetime**: Launches on each Tab press (~10ms startup)
- **Features**: Fast, standalone, computes completions locally with full dynamic arguments support (git branches, scoop packages, etc.)

### CommandPredictor (Long-lived)
- **Binary**: `PSCue.Module.dll` (Managed)
- **Purpose**: Provides inline suggestions via `ICommandPredictor`
- **Lifetime**: Loaded once with PowerShell module
- **Features**: IPC server for self-communication, intelligent cache, shared completion logic, skips dynamic arguments for speed

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
│  - IPC Server (for self/debug)      │
│  - CompletionCache (5-min TTL)      │
│  - Uses PSCue.Shared.dll            │
│  - Skips dynamic args (fast)        │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  Tab Completion (independent)       │
├─────────────────────────────────────┤
│  pscue-completer.exe                │
│  - Fast startup (<10ms)             │
│  - Local computation only           │
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
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex

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

PSCue has **269 unit tests** covering ArgumentCompleter logic, CommandPredictor, FeedbackProvider, IPC server behavior, cache filtering, generic learning components, persistence, navigation, and integration scenarios.

```powershell
# All tests (269 total: 91 ArgumentCompleter + 178 Module including Phases 11-15)
dotnet test

# Specific project
dotnet test test/PSCue.ArgumentCompleter.Tests/  # 91 tests
dotnet test test/PSCue.Module.Tests/             # 178 tests

# With verbose output
dotnet test --logger "console;verbosity=detailed"

# Run integration tests for filtering scenarios
pwsh -NoProfile -File test-scripts/test-completion-filtering.ps1 all
```

### Testing Completions Manually

Use the **PSCue.Debug** tool for testing and debugging:

```powershell
# Test local completion logic (no IPC)
dotnet run --project src/PSCue.Debug/ -- query-local "git checkout ma"

# Test IPC completion request (requires PSCue loaded in PowerShell)
dotnet run --project src/PSCue.Debug/ -- query-ipc "git checkout ma"

# Test IPC connectivity and measure round-trip time
dotnet run --project src/PSCue.Debug/ -- ping

# Show cache statistics
dotnet run --project src/PSCue.Debug/ -- stats

# Show cache statistics in JSON format
dotnet run --project src/PSCue.Debug/ -- stats --json

# Inspect cached completions (optionally filtered)
dotnet run --project src/PSCue.Debug/ -- cache --filter git

# Inspect cache in JSON format
dotnet run --project src/PSCue.Debug/ -- cache --filter git --json

# Clear all cached completions
dotnet run --project src/PSCue.Debug/ -- clear

# Show help with all commands
dotnet run --project src/PSCue.Debug/ -- help
```

**Features**:
- All commands show timing statistics (e.g., `Time: 11.69ms`)
- JSON output support for scripting (`--json` flag on stats/cache commands)
- Automatic PowerShell process discovery (finds PSCue-loaded sessions)
- Filter support for cache inspection (`--filter` flag)
- Clear cache command for testing
- Comprehensive test script: `test-scripts/test-pscue-debug.ps1`

Or test in PowerShell directly:

```powershell
Import-Module ./module/PSCue.psd1
TabExpansion2 'git checkout ma' 15
```

## Roadmap

### Current Status ✅

- [x] Tab completion working (ArgumentCompleter)
- [x] Inline predictions working (CommandPredictor)
- [x] Shared completion logic (PSCue.Shared)
- [x] Multi-platform CI/CD
- [x] Comprehensive documentation
- [x] **Phase 8**: IPC Communication Layer (simplified in 2025-01-27)
  - Named Pipe server in Module for CommandPredictor self-communication
  - IPC used only for inline predictions (fast cache access)
  - ArgumentCompleter simplified: always computes locally with full dynamic arguments
  - Clear separation: Tab = local, Inline predictions = IPC cache
- [x] **Phase 9**: Learning System & Error Suggestions
  - Full `IFeedbackProvider` implementation (PowerShell 7.4+)
  - Usage tracking and priority scoring
  - Personalized completions based on command history
  - Error recovery suggestions for git commands
- [x] **Phase 10**: Enhanced Debugging Tool (PSCue.Debug)
  - Commands: query-local, query-ipc, stats, cache, clear, ping, help
  - JSON output support for automation (--json flag)
  - PowerShell process auto-discovery
  - Filter support for cache inspection (--filter flag)
  - Timing statistics on all commands
  - Comprehensive test script: test-scripts/test-pscue-debug.ps1

- [x] **Phase 11**: Generic Command Learning ✅ **COMPLETE**
  - Universal command learning (learns from ALL commands, not just pre-configured ones)
  - Enhanced learning algorithms (frequency × recency scoring: 60% frequency + 40% recency)
  - Context-aware suggestions based on recent command history
  - Command sequence detection for workflows (git add → commit → push, docker build → run, etc.)
  - Privacy controls via `PSCUE_IGNORE_PATTERNS` environment variable
  - **65 new unit tests** covering all learning components
  - Components: CommandHistory (ring buffer), ArgumentGraph (knowledge graph), ContextAnalyzer, GenericPredictor, Hybrid CommandPredictor

- **Phase 15 (In Progress)**: Test Coverage Improvements
  - Added 45 comprehensive tests for critical components (CommandPredictor, FeedbackProvider)
  - **269 total tests passing** (91 ArgumentCompleter + 178 Module)
  - Fixed the "pluginstall" bug with 19 CommandPredictor.Combine tests
  - Added 26 FeedbackProvider tests covering command parsing, privacy filtering, and learning integration
  - Uses reflection to properly test internal PowerShell SDK components
  - All critical gaps addressed: CommandPredictor (95% coverage), FeedbackProvider (90% coverage)

### Configuration (Phase 11)

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

### Future Phases

- **Phase 12**: Advanced Features & Distribution
  - Error suggestions for more commands (gh, az, scoop)
  - ML-based prediction support
  - PowerShell Gallery publishing
  - Scoop/Homebrew package managers

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

## Cache & Learning Management

PSCue includes PowerShell functions for managing the completion cache and learning system:

### Cache Management

```powershell
# View cached completions
Get-PSCueCache                    # Show all cached completions
Get-PSCueCache -Filter git        # Filter by command
Get-PSCueCache -AsJson            # Output as JSON

# Cache statistics
Get-PSCueCacheStats               # Show cache stats (entries, hits, age)

# Clear cache
Clear-PSCueCache                  # Interactive confirmation
Clear-PSCueCache -Confirm:$false  # Skip confirmation
```

### Learning System Management (In-Memory)

```powershell
# View learned data (currently in memory)
Get-PSCueLearning                 # Show all learned commands
Get-PSCueLearning -Command kubectl # Filter by specific command
Get-PSCueLearning -AsJson         # Output as JSON

# Export/Import learned data (for backup or migration)
Export-PSCueLearning -Path ~/pscue-backup.json
Import-PSCueLearning -Path ~/pscue-backup.json          # Replace current data
Import-PSCueLearning -Path ~/pscue-backup.json -Merge   # Merge with current data

# Force save to disk (bypasses auto-save timer)
Save-PSCueLearning

# Clear all learned data
Clear-PSCueLearning               # Interactive confirmation (ConfirmImpact=High)
Clear-PSCueLearning -Confirm:$false
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
- Linux/macOS: `~/.local/share/PSCue/learned-data.db`

For detailed documentation on database functions, schema, and use cases, see [DATABASE-FUNCTIONS.md](DATABASE-FUNCTIONS.md).

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
3. Test IPC connectivity (used only for inline predictions):
   ```powershell
   $env:PSCUE_PID = $PID
   dotnet run --project src/PSCue.Debug/ -- ping
   ```
4. Test predictor manually:
   ```powershell
   pwsh -NoProfile -File test-inline-predictions.ps1  # If running from source
   ```

### Platform-specific issues

- **macOS**: Ensure you downloaded the `osx-arm64` build (Apple Silicon)
- **Linux**: May need to set executable permissions: `chmod +x ~/.local/pwsh-modules/PSCue/pscue-completer`
- **Windows**: If execution policy blocks the module, run: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`

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
  - [COMPLETED.md](COMPLETED.md) - Completed implementation phases (Phases 1-13, 15)
  - [CLAUDE.md](CLAUDE.md) - Quick reference for AI agents
  - [DATABASE-FUNCTIONS.md](DATABASE-FUNCTIONS.md) - Database query functions and schema
- **PowerShell API Documentation**:
  - [ICommandPredictor API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor)
  - [IFeedbackProvider API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider)
  - [Register-ArgumentCompleter](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/register-argumentcompleter)
