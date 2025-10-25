# PSCue Test Scripts

This directory contains PowerShell test scripts for manually testing PSCue functionality.

## Test Scripts

### IPC Communication Tests

**test-ipc.ps1**
- Comprehensive IPC functionality test
- Tests Named Pipe connectivity
- Tests Tab completion with ArgumentCompleter
- Tests CommandPredictor registration
- Checks inline predictions setup
- Includes debug output

**test-ipc-simple.ps1**
- Simplified IPC connectivity test
- Quick check for Named Pipe server
- Basic Tab completion test
- Minimal output for quick validation

### CommandPredictor Tests

**test-predictor.ps1**
- Tests CommandPredictor subsystem registration
- Verifies predictor is loaded and available
- Basic predictor functionality test

**test-manual-init.ps1**
- Tests manual IModuleAssemblyInitializer.OnImport() call
- Verifies predictor registration without full module load
- Useful for debugging initialization issues

**test-inline-predictions.ps1**
- Tests inline prediction functionality
- Requires interactive PowerShell session
- Tests PSReadLineOption PredictionSource setting
- Demonstrates inline suggestions as you type

**test-feedback-provider.ps1**
- Tests IFeedbackProvider registration and functionality
- **Requires PowerShell 7.4+ with PSFeedbackProvider experimental feature enabled**
- Checks PowerShell version compatibility
- Verifies experimental feature is enabled
- Confirms FeedbackProvider is registered as `PSCue.CommandCompleterFeedbackProvider`
- Tests both learning system (success events) and error suggestions (error events)
- Documents feedback provider behavior:
  - **Success events**: Silent learning, updates cache scores
  - **Error events**: Provides recovery suggestions (e.g., git errors)

## Usage

### Quick IPC Test
```powershell
# Run the simple IPC test
pwsh -NoProfile -File test-scripts/test-ipc-simple.ps1
```

### Comprehensive IPC Test
```powershell
# Run the full IPC test suite
pwsh -NoProfile -File test-scripts/test-ipc.ps1
```

### Interactive Inline Predictions Test
```powershell
# Must be run in interactive session (not with -File)
# Load the module first
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1

# Then run the test script
. test-scripts/test-inline-predictions.ps1
```

### Feedback Provider Test (PowerShell 7.4+)
```powershell
# Test the learning system and error suggestions
pwsh -NoProfile -File test-scripts/test-feedback-provider.ps1

# Or with full path on Windows
& "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -File test-scripts/test-feedback-provider.ps1
```

**Note**: This test requires:
- PowerShell 7.4 or higher
- PSFeedbackProvider experimental feature enabled (script will prompt if not enabled)
- PSCue module installed to `~/.local/pwsh-modules/PSCue/`

## Notes

- All scripts assume PSCue is installed to `~/.local/pwsh-modules/PSCue/`
- IPC tests require the module to be imported (which starts the IPC server)
- Some tests may show errors in non-interactive sessions (e.g., PSReadLine features)
- Run with `-Verbose` for detailed output where supported

## Automated Tests

For automated unit and integration tests, see:
- `test/PSCue.ArgumentCompleter.Tests/`
- `test/PSCue.Module.Tests/`
