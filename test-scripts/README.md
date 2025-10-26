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

**test-ipc-path.ps1**
- Tests if ArgumentCompleter is using IPC or local fallback
- Checks IPC connectivity with debug tool
- Calls completer exe directly to see debug output
- Verifies PSCUE_DEBUG environment variable enables logging

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

### Cache and Learning Tests

**test-cache-learning.ps1**
- Demonstrates the complete cache learning flow
- Shows cache starting empty
- Triggers Tab completion to populate cache
- Executes commands to trigger learning via FeedbackProvider
- Shows cache statistics and entries

**test-cache-debug.ps1**
- Tests cache with debug logging enabled
- Checks IPC connectivity
- Triggers completions via TabExpansion2
- Tests query-ipc command directly
- Compares cache state before and after operations
- Checks for debug log file

**test-with-debug-enabled.ps1**
- Main debug test with PSCUE_DEBUG=1
- Clears old log before testing
- Tests IPC connectivity
- Triggers TabExpansion2 and checks log output
- Shows whether IPC or local completions are used
- Displays cache state after completions

### ArgumentCompleter Registration Tests

**test-completer-registration.ps1**
- Verifies ArgumentCompleter registration for native commands
- Checks registered argument completers (requires PowerShell cmdlet)
- Tests TabExpansion2 directly
- Calls pscue-completer.exe directly to compare
- Checks for conflicting modules (e.g., posh-git)

**test-completer-invocation.ps1**
- Tests if PSCue ArgumentCompleter scriptblock is actually being invoked
- Creates debug log file to track invocations
- Manually registers completer with logging
- Verifies completer is called by TabExpansion2
- Shows invocation details (args, results)

**test-debug-registration.ps1**
- Tests ArgumentCompleter registration with debug output
- Enables PSCUE_DEBUG to see registration process
- Shows all commands being registered
- Tests TabExpansion2 with debug enabled

**test-what-completions.ps1**
- Analyzes what completions TabExpansion2 actually returns
- Shows detailed completion information (CompletionText, ToolTip, ResultType)
- Determines if completions are from PSCue or PowerShell defaults
- Distinguishes between ParameterValue (PSCue) and ProviderItem (file/directory)

### Diagnostic Tests

**test-check-completer-log.ps1**
- Checks the completer log file location
- Displays last 30 lines of log
- Highlights IPC-related and local fallback messages
- Useful for diagnosing IPC connectivity issues

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

### Cache Learning Test
```powershell
# See how the cache populates and learns from usage
pwsh -NoProfile -File test-scripts/test-cache-learning.ps1
```

### Debug IPC Issues
```powershell
# Enable debug logging to diagnose IPC problems
pwsh -NoProfile -File test-scripts/test-with-debug-enabled.ps1

# Check the completer log
pwsh -NoProfile -File test-scripts/test-check-completer-log.ps1
```

### Verify Completer Registration
```powershell
# Check if ArgumentCompleter is properly registered
pwsh -NoProfile -File test-scripts/test-completer-registration.ps1

# Test if completer is actually being invoked
pwsh -NoProfile -File test-scripts/test-completer-invocation.ps1
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
- Debug logging is controlled by the `PSCUE_DEBUG=1` environment variable
- The completer log file is located at: `$env:LOCALAPPDATA/PSCue/log.txt` (Windows)
- Set `$env:PSCUE_PID = $PID` in PowerShell to help debug tools find the IPC server

## Automated Tests

For automated unit and integration tests, see:
- `test/PSCue.ArgumentCompleter.Tests/`
- `test/PSCue.Module.Tests/`
