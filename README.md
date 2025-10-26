# PSCue

[![CI](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml/badge.svg)](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml)

**PSCue** is a unified PowerShell module that provides intelligent command-line completion and prediction for PowerShell Core (7.2+). It combines fast Tab completion with inline command suggestions to enhance your PowerShell experience.

## Features

- **🚀 Fast Tab Completion**: Native AOT executable for <10ms startup time
- **💡 Inline Predictions**: Smart command suggestions as you type using `ICommandPredictor`
- **⚡ IPC Communication**: ArgumentCompleter and CommandPredictor share state via Named Pipes for intelligent caching
- **🧠 Learning System**: Adapts to your command patterns over time (PowerShell 7.4+ with `IFeedbackProvider`)
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

PSCue provides intelligent completions for:

- **Git**: `git` - branches, tags, remotes, files, subcommands
- **GitHub CLI**: `gh` - repos, PRs, issues, workflows
- **Azure CLI**: `az` - resource groups, subscriptions, commands
- **Azure Developer CLI**: `azd` - environments, services
- **Azure Functions**: `func` - function apps, deployment
- **VS Code**: `code` - workspaces, extensions, files
- **Scoop** (Windows): `scoop` - apps, buckets
- **Winget** (Windows): `winget` - packages, sources
- **Chezmoi**: `chezmoi` - dotfile management commands
- **Tree alternatives**: `tre`, `lsd` - directory navigation
- **Disk usage**: `dust` - directory analysis

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
scoop install no<Tab>   # Completes to: nodejs, notepadplusplus, etc.
az group list --<Tab>   # Shows available flags: --output, --query, etc.
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
- **Features**: Fast, standalone, communicates with Predictor via Named Pipes for caching

### CommandPredictor (Long-lived)
- **Binary**: `PSCue.Module.dll` (Managed)
- **Purpose**: Provides inline suggestions via `ICommandPredictor`
- **Lifetime**: Loaded once with PowerShell module
- **Features**: IPC server, intelligent cache, shared completion logic

### Shared Completion Logic
- **Binary**: `PSCue.Shared.dll` (Managed)
- **Purpose**: Contains all command completion logic
- **Used by**: Both ArgumentCompleter (via NativeAOT compilation) and CommandPredictor
- **Benefits**: Consistent suggestions, easier maintenance, avoids NativeAOT reference issues

```
┌─────────────────────────────────────┐
│  PowerShell Session                 │
├─────────────────────────────────────┤
│  PSCue.Module.dll        │
│  - ICommandPredictor (suggestions)  │
│  - IFeedbackProvider (learning)     │
│  - IPC Server (Named Pipes)         │
│  - CompletionCache (5-min TTL)      │
│  - Uses PSCue.Shared.dll            │
└─────────────────────────────────────┘
           ↕ IPC (Named Pipes)
┌─────────────────────────────────────┐
│  Tab Completion                     │
├─────────────────────────────────────┤
│  pscue-completer.exe                │
│  - Fast startup (<10ms)             │
│  - IPC Client (with fallback)       │
│  - Uses PSCue.Shared.dll (compiled) │
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
4. **Future-Ready**: Designed for IPC communication and learning feedback loop
5. **CI/CD Pipeline**: Automated testing and releases for all platforms

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

```powershell
# All tests
dotnet test

# Specific project
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/

# With verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Testing Completions Manually

Use the PSCue.Debug tool:

```powershell
# Test local completion logic (no IPC)
dotnet run --project src/PSCue.Debug/ -- query-local "git checkout ma"

# Test IPC completion request (requires PSCue loaded in PowerShell)
dotnet run --project src/PSCue.Debug/ -- query-ipc "git checkout ma"

# Test IPC connectivity and measure round-trip time
dotnet run --project src/PSCue.Debug/ -- ping

# Show cache statistics
dotnet run --project src/PSCue.Debug/ -- stats

# Inspect cached completions (optionally filtered)
dotnet run --project src/PSCue.Debug/ -- cache --filter git
```

**Note**: All commands show timing statistics (e.g., `Time: 11.69ms`). The IPC commands (`query-ipc`, `ping`, `stats`, `cache`) automatically discover running PowerShell processes with PSCue loaded.

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
- [x] **Phase 8**: IPC Communication Layer
  - Named Pipe server/client for ArgumentCompleter ↔ Predictor communication
  - Shared completion cache for consistency and performance
  - <5ms IPC connection timeout achieved
- [x] **Phase 9**: Learning System & Error Suggestions
  - Full `IFeedbackProvider` implementation (PowerShell 7.4+)
  - Usage tracking and priority scoring
  - Personalized completions based on command history
  - Error recovery suggestions for git commands

### Future Phases

- **Phase 10**: Enhanced Learning & Distribution
  - Enhanced learning algorithms (frequency × recency scoring)
  - Error suggestions for more commands (gh, az, scoop)
  - Cross-session learning (persistent cache)
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

## Troubleshooting

### Tab completions not working

1. Verify module is loaded: `Get-Module PSCue`
2. Check completer registration: `Get-ArgumentCompleter`
3. Test executable directly: `pscue-completer.exe "ma" "git checkout ma" 15`
4. **Check if IPC is working** (cache requires IPC):
   ```powershell
   # Set PSCUE_PID to help debug tools find your session
   $env:PSCUE_PID = $PID

   # Test IPC connectivity
   dotnet run --project src/PSCue.Debug/ -- ping

   # Check cache state
   dotnet run --project src/PSCue.Debug/ -- cache
   ```
5. **Enable debug logging** to diagnose issues:
   ```powershell
   $env:PSCUE_DEBUG = "1"
   # Trigger a completion, then check the log
   # Log location: $env:LOCALAPPDATA/pwsh-argument-completer/log.txt (Windows)
   ```
6. Look for "Using IPC completions" vs "Using local completions" in the log:
   - **IPC completions** = cache is working ✅
   - **Local completions** = fallback mode (no caching) ⚠️

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
3. Test predictor manually:
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
- **Documentation**:
  - [ICommandPredictor API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor)
  - [IFeedbackProvider API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider)
  - [Register-ArgumentCompleter](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/register-argumentcompleter)
