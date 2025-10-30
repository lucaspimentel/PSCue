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

### Completion Filtering Tests

**test-completion-filtering.ps1**
- **NEW**: Comprehensive test for IPC cache filtering behavior
- Tests both scoop and git completions with various prefixes
- Verifies filtering works correctly for both cached and fresh completions
- Can run specific test suites: `all`, `scoop`, or `git`
- **Covers the bugs fixed on 2025-10-27**:
  - Bug #1: `scoop h<tab>` returning all completions instead of filtered
  - Bug #2: `scoop <tab>` after `scoop h<tab>` returning only "h" completions
  - Bug #3: `scoop update <tab>` returning all scoop subcommands instead of update arguments
- Returns exit code 0 if all tests pass, 1 if any fail
- **Run**: `pwsh -NoProfile -File test-scripts/test-completion-filtering.ps1 all`

**test-scoop-update.ps1**
- Tests the `scoop update <tab>` bug fix
- Verifies that commands with trailing spaces navigate into subcommands correctly
- Shows that `scoop update <tab>` returns update arguments (e.g., `*` parameter), not scoop subcommands
- Interactive test using TabExpansion2

**test-scoop-update-debug.ps1**
- Debug version of scoop update test with logging enabled
- Uses PSCue.Debug tool to test completions
- Shows log output to diagnose completion behavior

**test-scoop-update-with-logging.ps1**
- Complete scoop update test with full logging
- Clears log before testing
- Shows Tab completion results
- Displays relevant log entries to verify behavior

**test-scoop-sequence.ps1**
- Tests the exact bug scenario: `scoop h<tab>` then `scoop <tab>`
- Verifies cache stores all completions, not just filtered ones
- Shows detailed test results with PASS/FAIL indicators
- Helps verify the IPC cache filtering fix is working

**test-with-clear-cache.ps1**
- Tests cache population after clearing
- Verifies cache is populated with ALL completions (unfiltered)
- Inspects cache contents using PSCue.Debug tool
- Useful for debugging cache storage issues

**test-cache-contents.ps1**
- Triggers a specific completion and inspects what gets cached
- Uses PSCue.Debug tool to inspect cache entries
- Shows completion count and top completions
- Helps verify cache is storing data correctly

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

**check-log.ps1**
- Simple utility to view PSCue log file
- Shows last 100 lines of the log
- Displays log file path
- Quick access to debug output

### PSCue.Debug Tool Tests

**test-pscue-debug.ps1**
- Comprehensive test script for the PSCue.Debug tool
- Tests all commands: query-local, query-ipc, stats, cache, clear, ping, help
- Validates JSON output format
- Tests both with and without IPC server running
- Shows summary of test results
- **This is the main test script for Phase 10 enhancements**

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

### PSCue.Debug Tool Test
```powershell
# Run comprehensive test of PSCue.Debug tool (Phase 10)
pwsh -NoProfile -File test-scripts/test-pscue-debug.ps1
```

**Tests include**:
- Help system (`help` command)
- Local completions (`query-local`)
- IPC connectivity (`ping`)
- Cache statistics (`stats`, with and without `--json`)
- Cache inspection (`cache`, with `--filter` and `--json`)
- Clear cache (`clear`)
- IPC completions (`query-ipc`)

**Features tested**:
- JSON output format for automation
- PowerShell process auto-discovery
- Filter support for cache inspection
- Timing statistics on all commands
- Graceful handling when IPC server unavailable

### Module Functions Tests (Phase 16)

**test-module-functions.ps1**
- Comprehensive test of all PowerShell module functions
- Checks PowerShell version and experimental features
- Verifies module initialization (Cache, KnowledgeGraph, CommandHistory, Persistence)
- Tests all Get- functions (Get-PSCueCache, Get-PSCueLearning, Get-PSCueCacheStats)
- Checks subsystem registration (CommandPredictor, FeedbackProvider)
- Provides setup instructions for generating learning data

**test-database-functions.ps1**
- Tests new database query functions (Get-PSCueDatabaseStats, Get-PSCueDatabaseHistory)
- Demonstrates reading directly from SQLite database
- Shows summary statistics vs. detailed per-command statistics
- Compares in-memory data vs. database-persisted data
- Helps diagnose sync issues between memory and disk

**test-empty-state.ps1**
- Validates that Get- functions work correctly with empty data
- Tests the bug fix for Get-PSCueLearning returning "1 item with empty values"
- Verifies all functions return proper empty results (0 items, empty arrays)
- Quick validation test for fresh installations

**Usage:**
```powershell
# Test all module functions (comprehensive)
pwsh -NoProfile -File test-scripts/test-module-functions.ps1

# Test database query functions
pwsh -NoProfile -File test-scripts/test-database-functions.ps1

# Quick test for empty state bug fix
pwsh -NoProfile -File test-scripts/test-empty-state.ps1
```

## Notes

- All scripts assume PSCue is installed to `~/.local/pwsh-modules/PSCue/`
- IPC tests require the module to be imported (which starts the IPC server)
- Some tests may show errors in non-interactive sessions (e.g., PSReadLine features)
- Run with `-Verbose` for detailed output where supported
- Debug logging is controlled by the `PSCUE_DEBUG=1` environment variable
- The completer log file is located at: `$env:LOCALAPPDATA/PSCue/log.txt` (Windows)
- Set `$env:PSCUE_PID = $PID` in PowerShell to help debug tools find the IPC server

## Automated Tests

PSCue has **296 unit tests** covering ArgumentCompleter logic, IPC server behavior, cache filtering, learning system, persistence, and integration scenarios.

For automated unit and integration tests, see:
- `test/PSCue.ArgumentCompleter.Tests/` - **91 tests**
  - CommandCompleter logic tests
  - Completion generation for all supported commands
  - **Windows Terminal (wt)**: 24 tests including alias support, partial matching, tooltip verification
  - **SetLocationCommand** (Phase 13): directory navigation, caching, context detection
  - Platform-specific tests (Windows/Linux/macOS)
- `test/PSCue.Module.Tests/` - **205 tests**
  - CompletionCache tests (cache key generation, get/set, hit counting)
  - IPC filtering tests (filtering behavior, cache storage, real-world scenarios)
  - IPC server integration tests (end-to-end request/response)
  - IPC server error handling, concurrency, and lifecycle tests (Phase 15)
  - CommandPredictor tests including Combine method (Phase 15)
  - FeedbackProvider tests for learning system (Phase 15)
  - Learning system tests (CommandHistory, ArgumentGraph, ContextAnalyzer, GenericPredictor)
  - Persistence tests (SQLite storage, concurrency, edge cases, integration)
  - IPC cache filtering and subcommand navigation tests

Run all tests:
```powershell
dotnet test  # All 296 tests
```
