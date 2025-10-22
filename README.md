# PSCue

PowerShell Completion and Prediction for PowerShell Core 7.2+

## Features

- **Tab Completion**: Fast argument completion for common commands
- **Inline Predictions**: Smart command suggestions as you type
- **Learning System**: Adapts to your command patterns (PowerShell 7.4+)

## Supported Commands

git, gh, az, azd, func, code, scoop, winget, chezmoi, tre, lsd, dust

## Installation

### From GitHub Releases (Recommended)

```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

### From Source

```powershell
git clone https://github.com/lucaspimentel/PSCue.git
cd PSCue
./scripts/install-local.ps1
```

## Setup

Add to your PowerShell profile (`$PROFILE`):

```powershell
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1
Set-PSReadLineOption -PredictionSource HistoryAndPlugin
```

## Requirements

- PowerShell 7.2 or later
- Windows, macOS (Apple Silicon), or Linux

## License

MIT License - see [LICENSE](LICENSE) file for details
