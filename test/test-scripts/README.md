# PSCue Test Scripts

This directory contains PowerShell test scripts for manual/interactive verification of PSCue behavior. Automated coverage lives in `test/PSCue.ArgumentCompleter.Tests/` and `test/PSCue.Module.Tests/`; start there for unit-test-style coverage.

> **Historical note:** Some scripts in this directory reference the old IPC and `CompletionCache` components that were removed along the way. They are kept as historical reference and will not work against the current module.

## Active scripts

The following scripts work with the current PSCue architecture:

- **test-module-functions.ps1** — comprehensive check of the exported PowerShell functions
- **test-database-functions.ps1** — exercises `Get-PSCueDatabaseStats` / `Get-PSCueDatabaseHistory`
- **test-empty-state.ps1** — validates `Get-*` functions on a fresh install (no learned data)
- **test-feedback-provider.ps1** — tests the learning / error-suggestion path (requires PowerShell 7.4+)
- **test-inline-predictions.ps1** — interactive harness for inline predictions via PSReadLine

## Usage

### Module functions

```powershell
# Comprehensive check of exported functions
pwsh -NoProfile -File test-scripts/test-module-functions.ps1

# Database query functions
pwsh -NoProfile -File test-scripts/test-database-functions.ps1

# Fresh-install / empty-state checks
pwsh -NoProfile -File test-scripts/test-empty-state.ps1
```

### Inline predictions (interactive)

```powershell
# Must be run in an interactive session (not via -File)
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1
. test-scripts/test-inline-predictions.ps1
```

### Feedback provider (PowerShell 7.4+)

```powershell
pwsh -NoProfile -File test-scripts/test-feedback-provider.ps1
```

Requires PowerShell 7.4+ with the `PSFeedbackProvider` experimental feature enabled.

## Notes

- All scripts assume PSCue is installed to `~/.local/pwsh-modules/PSCue/`
- Some scripts require an interactive session (PSReadLine features)
- Debug logging is controlled by `$env:PSCUE_DEBUG = "1"`
- Completer log location: `$env:LOCALAPPDATA/PSCue/log.txt` (Windows)

## Automated tests

PSCue has xUnit test coverage in `test/PSCue.ArgumentCompleter.Tests/` and `test/PSCue.Module.Tests/`. See their README files for scope and run `dotnet test` from the repo root for the full suite, or filter:

```powershell
dotnet test --filter "FullyQualifiedName~WorkflowLearner"
dotnet test --filter "FullyQualifiedName~Pcd"
```
