# PSCue

[![CI](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml/badge.svg)](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml)

**PSCue** is a unified PowerShell module that provides intelligent command-line completion and prediction for PowerShell Core (7.2+). It combines fast Tab completion with inline command suggestions to enhance your PowerShell experience.

## Features

- **🚀 Fast Tab Completion**: Native AOT executable for <10ms startup time
- **💡 Inline Predictions**: Smart command suggestions as you type using `ICommandPredictor`
- **🧠 Learning System**: Adapts to your command patterns over time (PowerShell 7.4+ with `IFeedbackProvider`)
- **🔌 Cross-platform**: Windows, macOS (Apple Silicon), and Linux support
- **📦 Zero Configuration**: Works out of the box after installation

### Two-Component Design

PSCue uses a dual-architecture approach for optimal performance:

1. **ArgumentCompleter** (`pscue-completer.exe`) - NativeAOT executable for instant Tab completion
2. **CommandPredictor** (`PSCue.CommandPredictor.dll`) - Long-lived managed DLL for inline suggestions

This architecture enables:
- Sub-10ms Tab completion response time
- Persistent state and caching across completions
- Consistent suggestions between Tab completion and inline predictions
- Future IPC-based learning feedback loop (planned)

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

### Learning System (PowerShell 7.4+)

PSCue learns from your command execution patterns:

```powershell
# You frequently use: git commit -am "message"
# PSCue learns this pattern and prioritizes -am flag in future suggestions
```

**Note**: Learning features require PowerShell 7.4+ and the `PSFeedbackProvider` experimental feature:

```powershell
Enable-ExperimentalFeature PSFeedbackProvider
```

## Architecture

PSCue uses a two-component architecture optimized for both speed and intelligence:

### ArgumentCompleter (Short-lived)
- **Binary**: `pscue-completer.exe` (NativeAOT)
- **Purpose**: Handles Tab completion via `Register-ArgumentCompleter`
- **Lifetime**: Launches on each Tab press (~10ms startup)
- **Features**: Fast, standalone, can optionally communicate with Predictor via IPC (future)

### CommandPredictor (Long-lived)
- **Binary**: `PSCue.CommandPredictor.dll` (Managed)
- **Purpose**: Provides inline suggestions via `ICommandPredictor`
- **Lifetime**: Loaded once with PowerShell module
- **Features**: Maintains cache, learns patterns, shares state

```
┌─────────────────────────────────────┐
│  PowerShell Session                 │
├─────────────────────────────────────┤
│  PSCue.CommandPredictor.dll        │
│  - ICommandPredictor (suggestions)  │
│  - IFeedbackProvider (learning)     │
│  - Future: IPC Server               │
└─────────────────────────────────────┘
           ↕ (Future IPC)
┌─────────────────────────────────────┐
│  Tab Completion                     │
├─────────────────────────────────────┤
│  pscue-completer.exe                │
│  - Fast startup (<10ms)             │
│  - Local completion logic           │
│  - Future: IPC Client               │
└─────────────────────────────────────┘
```

## Requirements

- **PowerShell**: 7.2 or later (Core only, not Windows PowerShell 5.1)
- **Operating System**:
  - Windows x64
  - macOS arm64 (Apple Silicon)
  - Linux x64
- **Optional**: PowerShell 7.4+ for learning features (`IFeedbackProvider`)

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
dotnet build src/PSCue.CommandPredictor/ -c Release

# Run tests
dotnet test
```

### Running Tests

```powershell
# All tests
dotnet test

# Specific project
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.CommandPredictor.Tests/

# With verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Testing Completions Manually

Use the CLI testing tool:

```powershell
dotnet run --project src/PSCue.Cli/ -- "git checkout ma"
```

Or test in PowerShell directly:

```powershell
Import-Module ./module/PSCue.psd1
TabExpansion2 'git checkout ma' 15
```

## Roadmap

### Current Phase: Documentation ✅

- [x] Comprehensive README
- [ ] LICENSE file
- [ ] CONTRIBUTING.md (optional)

### Future Phases

- **Phase 8**: IPC Communication Layer
  - Named Pipe server/client for ArgumentCompleter ↔ Predictor communication
  - Shared completion cache for consistency and performance
  - <5ms IPC round-trip target

- **Phase 9**: Feedback Provider (Learning System)
  - Full `IFeedbackProvider` implementation
  - Usage tracking and priority scoring
  - Personalized completions based on command history

- **Phase 10**: Future Enhancements
  - ML-based prediction support
  - PowerShell Gallery publishing
  - Scoop/Homebrew package managers
  - Cross-session learning (persistent cache)

See [TODO.md](TODO.md) for detailed implementation plan.

## Contributing

Contributions are welcome! Please feel free to:

- Report bugs via [GitHub Issues](https://github.com/lucaspimentel/PSCue/issues)
- Submit pull requests for bug fixes or features
- Suggest new command completions
- Improve documentation

### Adding New Command Completions

To add completions for a new command:

1. Create a new file in `src/PSCue.ArgumentCompleter/KnownCompletions/YourCommand.cs`
2. Implement the `ICompletion` interface
3. Add tests in `test/PSCue.ArgumentCompleter.Tests/`
4. Register the completer in `module/PSCue.psm1`

See existing completions like `GitCommand.cs` or `ScoopCommand.cs` for examples.

## Troubleshooting

### Tab completions not working

1. Verify module is loaded: `Get-Module PSCue`
2. Check completer registration: `Get-ArgumentCompleter`
3. Test executable directly: `pscue-completer.exe "ma" "git checkout ma" 15`

### Inline predictions not appearing

1. Verify PSReadLine prediction is enabled:
   ```powershell
   Set-PSReadLineOption -PredictionSource HistoryAndPlugin
   ```
2. Check predictor is registered:
   ```powershell
   Get-PSSubsystem -Kind CommandPredictor
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
