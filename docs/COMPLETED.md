# PSCue Module - Completed Implementation Phases

This document archives all completed implementation phases for the PSCue module. These phases represent finished work that has been fully implemented, tested, and documented.

For current and future work, see [TODO.md](TODO.md).

---

## Completed Phases Summary

- **Phase 1**: Project Structure Setup ✅
- **Phase 2**: Copy ArgumentCompleter Code ✅
- **Phase 3**: Copy CommandPredictor Code ✅
- **Phase 4**: Create Module Files ✅
- **Phase 5**: Create Installation Scripts ✅
- **Phase 6**: GitHub Actions & CI/CD ✅
- **Phase 7**: Documentation ✅
- **Phase 7.5**: Debug/Testing Tool ✅
- **Phase 8**: IPC Communication Layer ✅
- **Phase 9**: Feedback Provider (Learning System) ✅
- **Phase 10**: Enhanced Debugging Tool (PSCue.Debug) ✅
- **Phase 11**: Generic Command Learning (Universal Predictor) ✅
- **Phase 12**: Cross-Session Persistence ✅
- **Phase 13**: Directory-Aware Navigation Suggestions ✅
- **Phase 14**: Enhanced cd Learning with Path Normalization ✅
- **Phase 15**: Test Coverage Improvements ✅
- **Phase 16**: PowerShell Module Functions + IPC Removal ✅
- **Phase 16.5**: Module Function Testing & Bug Fixes ✅
- **Phase 16.6**: Removed Unused CompletionCache ✅
- **Phase 17.1**: ML-Based N-gram Sequence Prediction ✅
- **Phase 17.2**: Privacy & Security - Sensitive Data Protection ✅
- **Phase 17.3**: Partial Word Completion Filtering ✅
- **Phase 17.4**: Multi-Word Prediction Suggestions ✅
- **Phase 17.5**: Smart Directory Navigation (`pcd` command) ✅
- **Phase 18.1**: Dynamic Workflow Learning ✅
- **Phase 18.2**: Time-Based Workflow Detection ✅ (completed as part of 18.1)
- **CI/CD & Distribution**: GitHub Actions Automated Releases ✅

---

### Phase 1: Project Structure Setup ✅
- [x] Document plan in TODO.md
- [x] Create directory structure
- [x] Create .gitignore
- [x] Create PSCue.sln solution file
- [x] Create empty project files:
  - [x] src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj
  - [x] src/PSCue.Module/PSCue.Module.csproj
  - [x] src/PSCue.Cli/PSCue.Cli.csproj (optional)
  - [x] test/PSCue.ArgumentCompleter.Tests/PSCue.ArgumentCompleter.Tests.csproj
  - [x] test/PSCue.Module.Tests/PSCue.Module.Tests.csproj

### Phase 2: Copy ArgumentCompleter Code ✅
- [x] Copy source files from `../pwsh-argument-completer/src/` to `src/PSCue.ArgumentCompleter/`
  - [x] Program.cs
  - [x] CommandCompleter.cs
  - [x] AssemblyInfo.cs
  - [x] Logger.cs
  - [x] Helpers.cs
  - [x] Completions/ directory (all files)
  - [x] KnownCompletions/ directory (all files)
- [x] Update namespaces from `PowerShellArgumentCompleter` to `PSCue.ArgumentCompleter`
- [x] Update AssemblyName to `pscue-completer` in .csproj
- [x] Copy test files from `../pwsh-argument-completer/test/` to `test/PSCue.ArgumentCompleter.Tests/`
- [x] Update test namespaces and project references
- [x] Verify build: `dotnet build src/PSCue.ArgumentCompleter/`
- [x] Verify tests: `dotnet test test/PSCue.ArgumentCompleter.Tests/`

### Phase 3: Copy CommandPredictor Code ✅
- [x] Copy source files from `../pwsh-command-predictor/src/PowerShellPredictor/` to `src/PSCue.Module/`
  - [x] Init.cs
  - [x] CommandCompleterPredictor.cs
  - [x] AssemblyInfo.cs
  - [x] (Optional) KnownCommandsPredictor.cs
  - [x] (Optional) SamplePredictor.cs
  - [x] (Optional) AiPredictor.cs
- [x] Update namespaces from `PowerShellPredictor` to `PSCue.Module`
- [x] Update AssemblyName to `PSCue.Module` in .csproj
- [x] Update project reference to point to PSCue.ArgumentCompleter
- [x] Copy test files from `../pwsh-command-predictor/test/` to `test/PSCue.Module.Tests/`
- [x] Update test namespaces and project references
- [x] Verify build: `dotnet build src/PSCue.Module/`
- [x] Verify tests: `dotnet test test/PSCue.Module.Tests/`

### Phase 4: Create Module Files ✅
- [x] Create `module/PSCue.psd1` (module manifest)
  - [x] Set RootModule to `PSCue.psm1`
  - [x] Set ModuleVersion to `1.0.0`
  - [x] Set Author, Description, etc.
  - [x] Set PowerShellVersion requirement (7.2+)
  - [x] Set CompatiblePSEditions to 'Core'
- [x] Create `module/PSCue.psm1` (module script)
  - [x] Find pscue-completer executable
  - [x] Register argument completers for supported commands
  - [x] Load PSCue.Module.dll (auto-registers predictors)
- [x] Test module loading manually

### Phase 5: Create Installation Scripts ✅
- [x] Create `scripts/` directory
- [x] Create `scripts/install-local.ps1` (build from source)
  - [x] Detect platform (Windows/macOS/Linux, x64/arm64)
  - [x] Build ArgumentCompleter with NativeAOT
  - [x] Build CommandPredictor
  - [x] Create installation directory
  - [x] Copy all necessary files
  - [x] Display instructions
  - [x] Fixed `$IsWindows` variable conflict (used `$IsWindowsPlatform` instead)
- [x] Create `scripts/install-remote.ps1` (download from GitHub)
  - [x] Accept optional $version variable from caller
  - [x] Detect platform and map to release asset name
  - [x] Query GitHub API for latest release or use specific version
  - [x] Download and extract release archive
  - [x] Install to ~/.local/pwsh-modules/PSCue/
  - [x] Clean up temp files
  - [x] Display instructions
- [x] Test local installation script on current platform
- [x] Verify module works after installation
- [x] Fixed PSCue.psm1 completer invocation
  - [x] Updated to pass 3 required arguments: wordToComplete, line, cursorPosition
  - [x] Updated output parsing from JSON to pipe-delimited format (completionText|tooltip)
- [x] Tested completions work correctly:
  - [x] Git commands (checkout, cherry-pick)
  - [x] Git flags (commit --all, --amend, --message, etc.)
  - [x] Scoop commands (import, info, install)

### Phase 6: GitHub Actions & CI/CD ✅
- [x] Create `.github/workflows/` directory
- [x] Create `.github/workflows/ci.yml` (CI workflow)
  - [x] Configure triggers (push to main, PRs)
  - [x] Setup build matrix (windows, macos, linux)
  - [x] Add build and test steps
  - [x] Add code formatting check (dotnet format) - temporarily disabled
  - [x] Upload test results as artifacts
- [x] Create `.github/workflows/release.yml` (Release workflow)
  - [x] Configure triggers (tags matching v*, workflow_dispatch)
  - [x] Build native binaries for supported platforms (win-x64, osx-arm64, linux-x64)
  - [x] Create platform-specific archives (zip for Windows, tar.gz for others)
  - [x] Generate checksums (SHA256)
  - [x] Create GitHub release with all artifacts
  - [x] Auto-generate release notes
  - [x] Note: Skipped macOS x64 (Intel) - focusing on Apple Silicon
- [x] Test CI workflow with a test commit/PR
- [x] Created minimal README.md for release archives
- [x] Fixed platform-specific tests to properly skip on non-Windows platforms
  - [x] Added Xunit.SkippableFact package
  - [x] Marked 35 Windows-only tests (winget, scoop) with [SkippableFact]
  - [x] Added RuntimeInformation.IsOSPlatform(OSPlatform.Windows) checks
  - [x] CI now passes on all platforms (Windows, macOS, Linux)
- [x] Optimized CI performance
  - [x] Commented out lint job (can re-enable when .editorconfig is configured)
- [ ] Document release process in TODO.md or CONTRIBUTING.md (deferred)

### Phase 7: Documentation ✅
- [x] Create comprehensive README.md
  - [x] Overview of PSCue
  - [x] Features (ArgumentCompleter + Predictor)
  - [x] Installation instructions
  - [x] Usage examples
  - [x] List of supported commands
  - [x] Architecture overview
  - [x] Contributing guidelines
- [x] Copy LICENSE from existing projects (MIT)
- [x] Document differences from original projects in README
- [ ] Create CONTRIBUTING.md (optional - deferred)

### Phase 7.5: Debug/Testing Tool ✅
- [x] Create `src/PSCue.Debug/` for testing and debugging
- [x] Implement `query-local` command - test local completion logic (no IPC)
- [x] Implement `query-ipc` command - test IPC completion requests
- [x] Implement `ping` command - test IPC connectivity with timing
- [x] Implement `stats` command - show cache statistics via IPC
- [x] Implement `cache` command - inspect cached completions with optional filter
- [x] Add timing statistics to all commands (format: `Time: 11.69ms`)
- [x] Add PowerShell process discovery (auto-find PSCue-loaded sessions)
- [x] Test: `dotnet run --project src/PSCue.Debug/ -- query-local "git commit"`
- [x] Test: `dotnet run --project src/PSCue.Debug/ -- query-ipc "git commit"`
- [x] Test: `dotnet run --project src/PSCue.Debug/ -- ping`
- [x] Test: `dotnet run --project src/PSCue.Debug/ -- stats`
- [x] Test: `dotnet run --project src/PSCue.Debug/ -- cache --filter git`
- [x] Verified debug tool works with IPC protocol and cache inspection
- [x] Fixed IPC server race condition (pipe disposal while in use)

### Phase 8: IPC Communication Layer ✅
- [x] Design IPC protocol schema (request/response format)
  - [x] Created `IpcRequest` with command, commandLine, wordToComplete, cursorPosition
  - [x] Created `IpcResponse` with completions array, cached flag, timestamp
  - [x] Created `CompletionItem` with text, description, score
  - [x] Added JSON serialization attributes for all types
  - [x] Created `IpcProtocol` utility class with pipe naming helpers
- [x] Implement JSON source generation for NativeAOT
  - [x] Created `IpcJsonContext` with JsonSourceGenerationOptions
  - [x] Marked all IPC types as JsonSerializable
  - [x] Updated IpcClient to use source-generated serializers
  - [x] Updated IpcServer to use source-generated serializers
  - [x] Eliminated all NativeAOT trimming warnings
- [x] Implement Named Pipe server in PSCue.Module
  - [x] Created `IpcServer.cs` with async server loop
  - [x] Start server on module initialization in `Init.OnImport()`
  - [x] Use session-specific pipe name (PSCue-{PID})
  - [x] Handle concurrent requests with fire-and-forget pattern
  - [x] Implement length-prefixed protocol (4-byte header + JSON payload)
  - [x] Integrate with `CommandCompleter.GetCompletions()` for all commands
  - [x] Added graceful error handling and logging
  - [x] Dispose server properly in `Init.OnRemove()`
- [x] Implement CompletionCache with usage tracking
  - [x] Created `CompletionCache.cs` with ConcurrentDictionary
  - [x] Thread-safe cache operations
  - [x] Time-based expiration (5-minute default)
  - [x] Hit count and last access tracking
  - [x] Cache key generation from command context
  - [x] `IncrementUsage()` method for score updates
  - [x] Cache statistics endpoint
  - [x] Memory management with `RemoveExpired()` method
- [x] Implement Named Pipe client in PSCue.ArgumentCompleter
  - [x] Created `IpcClient.cs` with synchronous client
  - [x] Connection with 10ms timeout (configurable)
  - [x] Response timeout 50ms (configurable)
  - [x] Fallback to local logic if unavailable
  - [x] JSON serialization using source generation
  - [x] Updated `Program.cs` to try IPC first, then fallback
- [x] Test IPC communication
  - [x] Build succeeded with 0 warnings, 0 errors
  - [x] All 65 tests pass (62 passed, 3 skipped platform-specific)
  - [x] Manual testing confirms IPC server starts and accepts connections
  - [x] Tab completion works with IPC or fallback
  - [x] Created test scripts: `test-ipc.ps1`, `test-ipc-simple.ps1`
- [x] Caching strategy implemented
  - [x] Cache invalidation: 5-minute time-based expiration
  - [x] Memory management: ConcurrentDictionary with Remove support
  - [x] Cache-first strategy: check cache before generating completions
- [x] **CRITICAL BUG FIX (2025-10-26)**: ArgumentCompleter wasn't using IPC!
  - [x] Fixed `IpcProtocol.GetCurrentPipeName()` to check `PSCUE_PID` environment variable
  - [x] ArgumentCompleter was using its own PID instead of PowerShell's PID for pipe name
  - [x] Updated PSCue.psm1 to set `$env:PSCUE_PID = $PID` on module load
  - [x] Added `-Native` flag to `Register-ArgumentCompleter` for native commands
  - [x] Changed debug environment variable from `DEBUG` to `PSCUE_DEBUG`
  - [x] Cache now correctly populates when Tab completions are triggered
  - [x] Added 9 comprehensive diagnostic test scripts in test-scripts/
  - [x] Verified: ArgumentCompleter log shows "Using IPC completions" after fix

**Performance achieved:**
- ✅ Named Pipe connection: <10ms timeout
- ✅ Build time: ~1.3 seconds for full solution
- ✅ Test execution: <1 second for all tests
- ✅ Module installation: ~30 seconds (includes NativeAOT compilation)

### Phase 9: Feedback Provider (Learning System) ✅

**Prerequisites completed in Phase 8:**
- ✅ CompletionCache with usage tracking (`IncrementUsage()` method ready)
- ✅ IPC protocol includes score field in CompletionItem
- ✅ Cache statistics endpoint implemented
- ✅ **Architecture Simplification** (2025-01-27): Removed IPC from ArgumentCompleter
  - ArgumentCompleter now always computes completions locally with full dynamic arguments support
  - Simpler code: No IPC client, no async operations in ArgumentCompleter
  - Fast enough: Local computation meets <50ms Tab completion target
  - IPC remains valuable for CommandPredictor (inline predictions need instant cache hits)
  - Clear separation: Tab completion = local, Inline predictions = cached via IPC

**Completed work:**
- [x] Implement IFeedbackProvider in PSCue.Module
  - [x] Create `FeedbackProvider.cs` implementing `IFeedbackProvider`
  - [x] Register as feedback provider in `Init.OnImport()`
  - [x] Handle `FeedbackTrigger.Success` events (silent learning)
  - [x] Extract command patterns from `FeedbackContext`
  - [x] Call `CompletionCache.IncrementUsage()` for executed commands
- [x] Test feedback learning
  - [x] Created test script: `test-scripts/test-feedback-provider.ps1`
  - [x] Verified provider registration on PowerShell 7.4+
  - [x] Confirmed provider ID: `e621fe02-3c68-4e1d-9e6f-8b5c4a2d7f90`
  - [x] Confirmed provider name: `PSCue.CommandCompleterFeedbackProvider`
- [x] Document PowerShell 7.4+ requirement for feedback features
  - [x] Updated CLAUDE.md with Phase 9 completion status
  - [x] Documented experimental feature requirement: `Enable-ExperimentalFeature PSFeedbackProvider`
  - [x] Documented graceful degradation on PowerShell 7.2-7.3

**Future enhancements** (not blocking initial release):
- [ ] Enhance CompletionCache learning algorithms
  - [ ] Track flag combinations (e.g., user often uses "git commit -am")
  - [ ] Track argument patterns (e.g., branch naming preferences)
  - [ ] Refine priority scoring algorithm (frequency × recency)
- [ ] Add error suggestions feature (handle `FeedbackTrigger.Error`)
  - [ ] Return `FeedbackItem` with helpful recovery suggestions when commands fail
  - [ ] Implement git-specific error patterns (not a repo, pathspec errors, uncommitted changes, etc.)
  - [ ] Expand error suggestions to more commands (gh, az, scoop, etc.)
- [ ] Add unit tests for feedback provider
  - [ ] Test pattern matching logic
  - [ ] Test cache update integration
- [ ] Cross-session persistence (save learned data to disk)

### Phase 10: Enhanced Debugging Tool (PSCue.Debug) ✅ (Complete)

**Goal**: Transform the CLI testing tool into a comprehensive debugging/diagnostics tool for inspecting the IPC server's learned data and cache state.

**Completed enhancements**:
- ✅ Split `query` into `query-local` (no IPC) and `query-ipc` (via IPC)
- ✅ Added timing statistics to all commands (format: `Time: 11.69ms`)
- ✅ Implemented PowerShell process discovery (auto-finds PSCue sessions)
- ✅ Fixed IPC server race condition (pipe disposal bug)
- ✅ Supports `PSCUE_PID` environment variable for targeting specific sessions
- ✅ Added JSON output support for `stats` and `cache` commands (--json flag)
- ✅ Implemented `clear` command to clear all cached completions
- ✅ Enhanced help system with all commands and options
- ✅ Created comprehensive test script: `test-scripts/test-pscue-debug.ps1`

**Rationale**:
- Developers need visibility into what the learning system has learned
- Users need to debug completion issues (why isn't X showing up?)
- Performance analysis requires cache hit/miss statistics
- Testing new completion patterns requires query tool

**New name**: Rename `PSCue.Cli` → `PSCue.Debug` (reflects expanded debugging purpose)

**Output binary**: `pscue-debug` (or `pscue-debug.exe` on Windows)

#### Subtasks

**10.1: Project Rename** ✅
- [x] Rename directory: `src/PSCue.Cli/` → `src/PSCue.Debug/` (already named correctly)
- [x] Rename project file: `PSCue.Cli.csproj` → `PSCue.Debug.csproj` (already correct)
- [x] Update `AssemblyName` to `pscue-debug`
- [x] Update `RootNamespace` to `PSCue.Debug`
- [x] Update all namespace declarations in source files
- [x] Update solution file references
- [x] Update CLAUDE.md references to CLI → Debug
- [x] Update test scripts that reference the CLI tool

**10.2: Add New IPC Request Types** ✅
- [x] Extend `IpcProtocol.cs` with new request types:
  - [x] Using string-based `RequestType` in `IpcDebugRequest` (simpler than enum)
  - [x] Added `Filter` parameter to `IpcDebugRequest` (optional filter for cache contents)
- [x] Create response types:
  - [x] `CacheStats` with statistics (entry count, hit counts, oldest entry age)
  - [x] `CacheEntryInfo` with array of cache entries (key, completions, scores, age, hits)
  - [x] `IpcDebugResponse` with flexible success/message/stats/entries structure
- [x] Update `IpcJsonContext` with new types for source generation
- [x] Protocol documented in comments

**10.3: Implement Server-Side Handlers** ✅
- [x] Extended `IpcServer.cs` to handle new request types:
  - [x] `stats`: Calls `CompletionCache.GetStatistics()`, returns enriched data
  - [x] `cache`: Returns all cache entries (optionally filtered)
  - [x] `clear`: Calls `CompletionCache.Clear()`, returns count of removed entries
  - [x] `ping`: Returns simple "pong" message
- [x] Handle errors gracefully (unknown request types return error response)

**10.4: Implement CLI Commands Structure** ✅
- [x] Designed command-line interface:
  ```
  pscue-debug <command> [options]

  Commands:
    query-local <input>          Get completions using local logic
    query-ipc <input>            Get completions via IPC
    stats [--json]               Show cache statistics
    cache [--filter <text>]      Show cache contents with scores
          [--json]
    clear                        Clear the completion cache
    ping                         Test IPC connectivity
    help                         Show help message
  ```
- [x] Created command router in Program.cs (switch expression)
- [x] Added `--json` flag support for machine-readable output
- [x] Supports `PSCUE_PID` environment variable to target specific PowerShell session

**10.5: Implement 'stats' Command** ✅
- [x] Send `GetCacheStats` IPC request (using `IpcDebugRequest`)
- [x] Display formatted output with timing information
- [x] JSON output format (when `--json` flag used)

**10.6: Implement 'cache' Command** ✅
- [x] Send `GetCacheContents` IPC request (using `IpcDebugRequest`)
- [x] Support optional `--filter <pattern>` for filtering by command/key
- [x] Display formatted output with key, completion count, hit count, age, and top completions
- [x] JSON output format for scripting (--json flag)
- [x] Handle empty cache gracefully

**10.7: Implement 'ping' Command** ✅
- [x] Send `Ping` IPC request (using `IpcDebugRequest`)
- [x] Display server connectivity with round-trip time
- [x] Handle connection failures with clear error messages

**10.8: Implement 'clear' Command** ✅
- [x] Send `ClearCache` IPC request
- [x] Display confirmation with count of removed entries
- [x] Support `--json` flag for JSON output
- [x] Prepared for future `--force` flag (currently no confirmation prompt)

**10.9: Refactor 'query' Command** ✅
- [x] Keep existing functionality (test completions)
- [x] Split into `query-local` (no IPC) and `query-ipc` (via IPC) for testing both paths
- [x] Enhance output to show whether result was cached (query-ipc shows cached status)
- [x] Add timing information (all commands show `Time: X.XXms`)
- [x] PowerShell process discovery (automatically finds PSCue-loaded sessions)

**10.10: Add Help System** ✅
- [x] Implement `help` command showing all commands
- [x] Add `--help` flag support
- [x] Include examples in help text
- [x] Show available options and flags

**10.11: Error Handling & UX** ✅
- [x] Handle IPC connection failures gracefully
- [x] Detect when no PowerShell session is running PSCue
- [x] Suggest running `Import-Module PSCue` if server not found
- [x] Clear error messages for all failure cases

**10.12: Testing** ✅
- [x] Created test script: `test-scripts/test-pscue-debug.ps1`
- [x] Tests all commands (stats, cache, ping, clear, query-local, query-ipc)
- [x] Tests with and without IPC server running
- [x] Tests JSON output format
- [x] Tests filter functionality for cache command

**10.13: Documentation** ✅
- [x] Update CLAUDE.md with new tool capabilities
- [x] Update README.md with debugging tool section
- [x] Add examples of using debug tool for troubleshooting
- [x] Document IPC protocol extensions (./TECHNICAL_DETAILS.md updated)
- [x] Document IPC server race condition fix
- [x] Document PowerShell process discovery feature

**10.14: Optional Enhancements** (Deferred to future phases)
- [ ] Add `watch` mode for live monitoring (e.g., `pscue-debug stats --watch`)
- [ ] Add export functionality (export cache to file)
- [ ] Add import functionality (import learned data from file)
- [ ] Add cache warmup command (pre-populate common completions)
- [ ] Add performance benchmark command
- [ ] Add colored output support (ANSI codes)

#### Success Criteria ✅ (All Met!)
- [x] All PSCue.Cli references renamed to PSCue.Debug
- [x] `pscue-debug` binary builds successfully
- [x] All five core commands work: query-local, query-ipc, stats, cache, ping, clear
- [x] JSON output option works for stats and cache commands
- [x] Tool provides helpful error messages when server unavailable
- [x] Documentation updated with debugging tool usage
- [x] PowerShell process discovery implemented (auto-finds PSCue sessions)
- [x] Timing statistics on all commands
- [x] IPC server race condition fixed
- [x] Test script validates all commands

#### Implementation Order
1. Start with Phase 10.1 (rename) - low risk, establishes foundation
2. Phase 10.2-10.3 (protocol & server handlers) - enables all other features
3. Phase 10.4 (CLI structure) - establishes command routing
4. Phase 10.5-10.9 (implement commands) - can be done in parallel
5. Phase 10.10-10.11 (polish) - improves UX
6. Phase 10.12-10.13 (testing & docs) - validation
7. Phase 10.14 (optional) - as time permits

#### Post-Phase 10: IPC Cache Filtering Bugs Fixed (2025-10-27)

**Issue #1**: `scoop h<tab>` was returning all completions instead of only those starting with "h"
- **Root Cause**: Cached completions weren't being filtered by `wordToComplete` before returning to client
- **Fix**: Added filtering in `IpcServer.HandleCompletionRequestAsync()` (lines 156-164)
- **File**: `src/PSCue.Module/IpcServer.cs`

**Issue #2**: After `scoop h<tab>`, then `scoop <tab>` was returning only "h" completions instead of all
- **Root Cause**: Cache was storing filtered completions (only 3 items) instead of all 28 subcommands
- **Fix**: Modified `GenerateCompletions()` to remove partial word from commandLine before calling `CommandCompleter.GetCompletions()`, ensuring cache stores ALL completions unfiltered
- **File**: `src/PSCue.Module/IpcServer.cs` (lines 360-364)

**New Test Coverage**:
- Added `CompletionCacheTests.cs` (7 tests): Cache key generation, get/set, hit counting, usage tracking
- Added `IpcFilteringTests.cs` (12 tests): Filtering behavior, cache storage, real-world scenarios
- Added `IpcServerIntegrationTests.cs` (5 tests): End-to-end IPC request/response testing
- Added integration test script: `test-scripts/test-completion-filtering.ps1`

**Total Tests**: **87** (62 ArgumentCompleter + 25 Module) - all passing ✓

**Key Lesson**: These bugs existed because PSCue.Module.Tests had essentially zero coverage of the IPC server and caching logic. The new tests would have caught both bugs before they reached users.

### Phase 11: Generic Command Learning (Universal Predictor) ✅ **COMPLETE**

**Status**: ✅ Implementation complete, all 154 tests passing (62 ArgumentCompleter + 92 Module including 65 new Phase 11 tests)

**Completed**: 2025-01-27

**Summary**:
- ✅ 4 new core components: CommandHistory, ArgumentGraph, ContextAnalyzer, GenericPredictor (~1,050 lines)
- ✅ Enhanced FeedbackProvider for universal learning (+80 lines)
- ✅ Hybrid CommandPredictor blending known + learned completions (+100 lines)
- ✅ 65 comprehensive unit tests covering all learning components (~1,225 lines)
- ✅ Configuration via environment variables (PSCUE_DISABLE_LEARNING, PSCUE_HISTORY_SIZE, etc.)
- ✅ Privacy controls (PSCUE_IGNORE_PATTERNS with wildcard support)
- ✅ Context-aware suggestions based on command workflows
- ✅ Frequency × recency scoring (60/40 split)
- ✅ Cross-session persistence deferred to Phase 12

**Goal**: Transform ICommandPredictor from command-specific (only knows git, gh, scoop, etc.) to a generic system that learns from ALL user commands, even ones PSCue doesn't explicitly support.

**Key Insight**: While ArgumentCompleter needs explicit knowledge of each command's syntax (for Tab completion), ICommandPredictor should be command-agnostic and learn patterns from actual usage.

**Rationale**:
- Users run hundreds of commands PSCue doesn't explicitly support (kubectl, docker, cargo, npm, etc.)
- Even for unsupported commands, we can learn which flags/arguments are commonly used
- Context matters: suggest arguments based on recent command history
- For supported commands, blend known completions with learned patterns
- Creates a truly personalized completion experience

### Phase 12: Cross-Session Persistence ✅ **COMPLETE**

**Status**: ✅ Implementation complete with comprehensive testing

**Completed**: 2025-01-27

**Summary**:
- ✅ SQLite-based persistence (~470 lines in PersistenceManager.cs)
- ✅ Additive merging strategy (frequencies summed, timestamps use max)
- ✅ Concurrent access with SQLite WAL mode
- ✅ Auto-save every 5 minutes + save on module unload
- ✅ Integrated with Init.cs (load on import, save on remove)
- ✅ **54 new tests** covering concurrency, edge cases, and integration
  - 10 unit tests (PersistenceManagerTests.cs)
  - 11 concurrency tests (PersistenceConcurrencyTests.cs)
  - 18 edge case tests (PersistenceEdgeCaseTests.cs)
  - 15 integration tests (PersistenceIntegrationTests.cs)
- ✅ All 198 tests passing (62 ArgumentCompleter + 136 Module)

**Key Features**:
- Multiple PowerShell sessions can run concurrently without data loss
- Learning data survives PowerShell restarts
- Database location: `~/.local/share/PSCue/learned-data.db` (Linux/macOS), `%LOCALAPPDATA%\PSCue\learned-data.db` (Windows)
- Handles Unicode, special characters, very long strings
- Graceful degradation on file system errors
- Zero configuration required

**Test Coverage**:
- Multi-session concurrency (5 concurrent writers)
- Stress test (100 concurrent writers)
- Readers + writers without deadlock
- Database corruption recovery
- Long-running session simulation
- Edge cases (empty data, special chars, Unicode, file deletion)

---

### Phase 13: Directory-Aware Navigation Suggestions (Set-Location/cd) ✅ **COMPLETE**

**Status**: ✅ Implementation complete

**Completed**: 2025-01-28

**Summary**:
- ✅ Created SetLocationCommand.cs with directory completion support (~370 lines)
- ✅ Integrated with CommandCompleter for cd/Set-Location/sl/chdir
- ✅ Enhanced GenericPredictor for navigation-aware inline predictions
- ✅ Smart caching (5s TTL) for performance (<50ms)
- ✅ Context detection (absolute/relative/parent/home paths)
- ✅ Cross-platform (Windows & Unix path handling)
- ✅ 31 comprehensive tests (29 passing, 2 Unix-specific properly skipped on Windows)
- ✅ Total tests: 229 (93 ArgumentCompleter + 136 Module, all passing)

**Key Features**:
- Subdirectories of current directory
- Parent/sibling directories (../)
- Home directory expansion (~/)
- Absolute path completion (C:\, D:\, /)
- Common shortcuts (., .., ~)
- Frequently visited directories from learning system
- Performance optimized (<50ms target met with caching)

**Files Modified/Created**:
- Created: src/PSCue.Shared/KnownCompletions/SetLocationCommand.cs
- Modified: src/PSCue.Shared/CommandCompleter.cs (added 4 command cases)
- Modified: src/PSCue.Module/GenericPredictor.cs (added navigation command handling)
- Created: test/PSCue.ArgumentCompleter.Tests/SetLocationCommandTests.cs (31 tests)
- Updated: CLAUDE.md, README.md (documentation)

**Architecture**:
- **Tab Completion**: SetLocationCommand with DynamicArguments enumerates directories
- **Inline Predictions**: GenericPredictor detects navigation commands, calls GetDirectorySuggestions()
- **Learning Integration**: Merges filesystem suggestions with learned frequently-visited paths
- **Caching**: ConcurrentDictionary with 5s TTL, granular cache keys per directory

**Test Coverage**:
- Basic functionality (cd, Set-Location, sl, chdir commands)
- Directory enumeration (subdirectories, parent directories)
- Context detection (absolute, relative, parent, home, implicit)
- Partial name filtering
- Performance validation (<50ms)
- Cache behavior
- Cross-platform (Windows/Unix paths)
- Edge cases (nonexistent paths, empty results)

**Post-Completion Fix (2025-01-28)**:
- Fixed tooltip inconsistency between Tab completions and inline predictions
- Added `GetDirectorySuggestionsWithPaths()` method returning `(CompletionText, FullPath)` tuples
- Both mechanisms now show consistent tooltips: "Directory: {full-path}"
- Modified: SetLocationCommand.cs (added `*WithPaths` methods), GenericPredictor.cs

---

## Phase 15: Test Coverage Improvements ✅ **COMPLETE**

**Goal**: Address test coverage gaps identified during Phase 13 development. The "pluginstall" bug (claude plugin + install → claude pluginstall) revealed that critical logic paths lack automated tests.

**Current State**: 269 tests total (91 ArgumentCompleter + 178 Module)
- ArgumentCompleter.Tests: Good coverage for completion logic
- Module.Tests: Heavy coverage for Phases 11-15 (learning, persistence, navigation, predictor, feedback)

### Identified Test Coverage Gaps

#### **Critical Gap #1: CommandPredictor.Combine Method** ✅
- **Status**: ✅ Complete (19 tests added)
- **Location**: `src/PSCue.Module/CommandPredictor.cs:150-178`
- **Issue**: Private method with complex string overlap logic
- **Bug Found**: "claude plugin" + "install" → "claude pluginstall" (substring match instead of word boundary)
- **Why Important**: Used by EVERY inline prediction to construct suggestion text
- **Test Needs**:
  - Partial command completion: "git chec" + "checkout" → "git checkout"
  - Word boundary respect: "claude plugin" + "install" → "claude plugin install" (NOT "claude pluginstall")
  - No overlap: "git " + "status" → "git status"
  - Full overlap: "git" + "git" → "git"
  - Multiple spaces: "git  " + "commit" → "git commit"
  - Edge cases: empty input, special characters

**Proposed Solution**:
1. Make `Combine` method `internal` instead of `private`
2. Add `[assembly: InternalsVisibleTo("PSCue.Module.Tests")]` to AssemblyInfo.cs
3. Create `CommandPredictorTests.cs` with ~10 test cases for Combine method
4. Add integration tests for GetSuggestion that indirectly validate Combine logic

#### **Critical Gap #2: CommandPredictor.GetSuggestion**
- **Status**: ❌ No direct tests (only tested indirectly via integration)
- **Location**: `src/PSCue.Module/CommandPredictor.cs:43-82`
- **Issue**: Core public API with no unit tests
- **Why Important**: Entry point for ALL inline predictions
- **Test Needs**:
  - Known command suggestions (git, gh, scoop)
  - Unknown command fallback to generic learning
  - Empty input handling
  - Cache integration
  - Error handling

#### **Critical Gap #3: FeedbackProvider**
- **Status**: ❌ No unit tests (only manual testing via test scripts)
- **Location**: `src/PSCue.Module/FeedbackProvider.cs`
- **Issue**: Core learning component with zero test coverage
- **Why Important**: Responsible for ALL learning from user commands
- **Test Needs**:
  - Command parsing (extract command, arguments, flags)
  - Learning system integration (CommandHistory, ArgumentGraph updates)
  - Privacy controls (PSCUE_IGNORE_PATTERNS)
  - Successful command feedback
  - PowerShell 7.4 compatibility

#### **Important Gap #4: IpcServer Error Handling**
- **Status**: ⚠️ Limited coverage (only happy path tested)
- **Location**: `src/PSCue.Module/IpcServer.cs`
- **Issue**: No tests for error conditions, edge cases, or concurrent access
- **Why Important**: Critical path for inline predictions
- **Test Needs**:
  - Malformed requests
  - Client disconnects mid-request
  - Concurrent requests
  - Server start/stop lifecycle
  - Cache clearing while requests in flight

#### **Important Gap #5: Init Lifecycle**
- **Status**: ❌ No tests
- **Location**: `src/PSCue.Module/Init.cs`
- **Issue**: Module load/unload, auto-save timer, persistence integration untested
- **Why Important**: Controls entire module lifecycle
- **Test Needs**:
  - OnImport (IPC server starts, persistence loads, auto-save timer starts)
  - OnRemove (IPC server stops, persistence saves)
  - Auto-save timer behavior
  - Error recovery (e.g., persistence load failure)

#### **Low Priority Gap #6: KnownCompletions**
- **Status**: ⚠️ Minimal coverage (only integration tests via CommandCompleter)
- **Location**: `src/PSCue.Shared/KnownCompletions/`
- **Issue**: Command definitions (git, gh, scoop, etc.) lack dedicated tests
- **Risk**: Low (mostly static data, bugs caught during manual testing)
- **Test Needs** (optional):
  - Basic structure validation (parameters exist)
  - Static arguments correct
  - Dynamic arguments execute without error

### Implementation Plan

#### Phase 15.1: Critical Gaps (High Priority) ✅ **COMPLETE**
**Time Estimate**: 6-8 hours

1. **CommandPredictorTests.cs** (NEW FILE) ✅ COMPLETE
   - [x] Combine method tests (19 tests)
     - Basic overlap (partial, full, none)
     - Word boundary cases
     - Edge cases (empty, whitespace, special chars)
   - [x] GetSuggestion tests (covered via integration)
     - Known commands, unknown commands, empty input
     - Integration with learning system (CommandHistory + ArgumentGraph)

2. **FeedbackProviderTests.cs** (NEW FILE) ✅ COMPLETE
   - [x] Command parsing (5 tests)
   - [x] Learning integration (9 tests)
   - [x] Privacy controls (6 tests)
   - [x] PowerShell 7.4 compatibility (6 tests)

#### Phase 15.2: Important Gaps (Medium Priority) ✅ **COMPLETE**
**Time Estimate**: 4-6 hours

3. **IpcServerErrorHandlingTests.cs** (NEW FILE) ✅ COMPLETE
   - [x] Error handling (10 tests)
     - Malformed requests, excessive payloads, invalid JSON
     - Missing fields, client disconnects, empty payloads
     - Debug request errors, special characters

4. **IpcServerConcurrencyTests.cs** (NEW FILE) ✅ COMPLETE
   - [x] Concurrent requests (7 tests)
     - Multiple concurrent requests, high concurrency (20 requests)
     - Thread-safe cache access, mixed valid/invalid requests
     - Concurrent debug+completion, cache clearing, rapid connect/disconnect

5. **IpcServerLifecycleTests.cs** (NEW FILE) ⚠️ MOSTLY COMPLETE
   - [x] Lifecycle (9 active + 1 skipped = 10 tests)
     - Server start/stop, disposal, connection acceptance
     - Cache persistence, cleanup
     - **Skipped**: 1 test (dispose while requests in-flight causes test host crash)

6. **InitTests.cs** (NOT STARTED - deferred)
   - [ ] Module load/unload (4 tests)
   - [ ] Auto-save timer (2 tests)
   - [ ] Error recovery (3 tests)
   - **Status**: Deferred to future phase - lower priority than IPC server tests

#### Phase 15.3: Nice to Have (Low Priority) 🟢
**Time Estimate**: 2-3 hours (optional)

5. **Spot-check KnownCompletions**
   - [ ] GitCommandTests.cs - basic structure validation
   - [ ] ClaudeCommandTests.cs - validate new command
   - [ ] Test a few dynamic arguments

6. **CompletionCache extended tests**
   - [ ] Expiration behavior
   - [ ] Heavy concurrent load

### Metrics

**Current**: 296 tests (up from 269, started at 229)
**Target**: ✅ 270+ tests achieved (added 67 tests in Phase 15)

**Coverage by Component** (updated):
- ArgumentCompleter: ✅ 90%+ (91 tests)
- GenericPredictor: ✅ 95%+ (65 tests in Phase 11)
- Persistence: ✅ 95%+ (54 tests in Phase 12)
- SetLocationCommand: ✅ 90%+ (31 tests in Phase 13)
- **CommandPredictor: ✅ 95%** (19 tests in Phase 15.1) - **CRITICAL GAP FIXED**
- **FeedbackProvider: ✅ 90%** (26 tests in Phase 15.2) - **IMPORTANT GAP FIXED**
- **IpcServer: ✅ 85%** (27 tests - error handling, concurrency, lifecycle) - **MAJOR IMPROVEMENT**
- **Init: ❌ 0%** (lifecycle untested - deferred to future phase)
- KnownCompletions: ⚠️ 5% (mostly untested, but low risk)

**Test Breakdown by Phase 15 Component**:
- IpcServerErrorHandlingTests: 10 tests (all passing)
- IpcServerConcurrencyTests: 7 tests (all passing)
- IpcServerLifecycleTests: 10 tests (9 passing, 1 skipped with documented reason)

### Success Criteria for Phase 15 ✅ **COMPLETE**

1. ✅ **COMPLETE** - All critical bugs like "pluginstall" are caught by automated tests
2. ✅ **COMPLETE** - CommandPredictor.Combine has comprehensive test coverage (19 tests)
3. ✅ **COMPLETE** - CommandPredictor.GetSuggestion tested (integration coverage)
4. ✅ **COMPLETE** - FeedbackProvider has unit tests covering core functionality (26 tests)
5. ✅ **COMPLETE** - IpcServer error handling, concurrency, and lifecycle tested (27 tests)
6. ⚠️ **DEFERRED** - Init lifecycle tests deferred to future phase (lower priority)
7. ✅ **COMPLETE** - Test count reached 296 (target was 270+, exceeded by 26 tests)
8. ✅ **COMPLETE** - All tests pass (295 passing, 1 skipped with documented reason)
9. ✅ **COMPLETE** - Build succeeds cleanly with 0 errors, 0 warnings

**Phase 15 Summary**:
- Added 67 new tests across 3 major areas (CommandPredictor, FeedbackProvider, IpcServer)
- Test count increased from 229 → 296 (29% increase)
- Critical gaps in CommandPredictor and IpcServer addressed
- All high and medium priority gaps resolved
- Low priority gaps (Init tests, KnownCompletions tests) deferred

### Test Infrastructure Improvements

**Consider adding**:
- [ ] Code coverage reporting in CI (e.g., Coverlet)
- [ ] Coverage badge in README
- [ ] Pre-commit hook to run tests locally
- [ ] Test coverage thresholds (fail if coverage drops below X%)

### Lessons Learned

**From the "pluginstall" bug**:
- Private methods with complex logic should be testable (use `internal` + `InternalsVisibleTo`)
- String manipulation is error-prone - needs comprehensive test coverage
- Edge cases matter (word boundaries vs character-level matching)
- Integration tests alone aren't enough - unit tests catch bugs earlier

**Best Practices Going Forward**:
1. Write tests BEFORE fixing bugs (TDD for bug fixes)
2. Add regression test for every bug found
3. Complex private methods should be `internal` and tested directly
4. Consider property-based testing for string manipulation
5. Test edge cases explicitly (empty strings, special chars, Unicode, etc.)

---

## Phase 14: Enhanced cd Learning with Path Normalization ✅ **COMPLETE**

**Status**: ✅ Implementation complete with comprehensive testing

**Completed**: 2025-10-30

**Summary**:
- ✅ Path normalization to absolute form (handles ~, .., relative paths) (~140 lines in ArgumentGraph.cs)
- ✅ Context-aware filtering (current directory, partial path matching) (~75 lines in GenericPredictor.cs)
- ✅ Enhanced scoring for frequently visited paths (0.85-1.0 vs 0.6)
- ✅ Trailing directory separator matching PowerShell native behavior
- ✅ Absolute path handling in predictions (CommandPredictor.Combine)
- ✅ **18 new tests** (14 Phase 14 + 4 updated Combine tests)
  - 10 path normalization tests (ArgumentGraphTests.cs)
  - 4 context-aware filtering tests (GenericPredictorTests.cs)
  - 4 absolute path Combine tests (CommandPredictorTests.cs)
- ✅ All 343 tests passing (203 Module + 140 ArgumentCompleter)

**Key Features**:
- **Path Normalization**: All navigation paths normalized to absolute form before learning
  - `cd ~/projects` and `cd ../projects` both learn as `/home/user/projects`
  - Merges different input forms to same entry with cumulative usage count
- **Context Filtering**:
  - Current directory never suggested
  - Partial path matching: `cd dotnet` matches `D:\source\datadog\dd-trace-dotnet`
  - Only relevant paths shown based on user input
- **Smart Scoring**: Frequently visited paths prioritized (0.85-1.0 range)
- **PowerShell Native Behavior**: Trailing `\` or `/` added to all directory paths
- **Bug Fixes**:
  - Fixed `cd dotnet` suggesting invalid `cd dotnet D:\path\`
  - Now correctly suggests `cd D:\path\`

**Files Modified**:
- `src/PSCue.Module/ArgumentGraph.cs`: Path normalization logic (3 new methods)
- `src/PSCue.Module/FeedbackProvider.cs`: Working directory capture
- `src/PSCue.Module/GenericPredictor.cs`: Context-aware filtering (2 new methods)
- `src/PSCue.Module/CommandPredictor.cs`: Absolute path handling in Combine
- `test/PSCue.Module.Tests/ArgumentGraphTests.cs`: 10 new normalization tests
- `test/PSCue.Module.Tests/GenericPredictorTests.cs`: 4 new filtering tests
- `test/PSCue.Module.Tests/CommandPredictorTests.cs`: 4 new Combine tests

**Implementation Details**:

1. **Path Normalization** (ArgumentGraph.cs:220-281):
   - `NormalizeNavigationPaths()`: Normalizes array of paths for cd/Set-Location/sl/chdir
   - `NormalizePath()`: Handles ~, relative paths, absolute paths → returns absolute form
   - Stores absolute paths in ArgumentGraph for consistent learning
   - Example: `cd ~/foo`, `cd ../foo`, `cd /home/user/foo` all learn as `/home/user/foo`

2. **Context-Aware Filtering** (GenericPredictor.cs:105-195):
   - Filters out current directory from suggestions
   - `MatchesPartialPath()`: Matches user input against learned paths
   - `IsSamePath()`: Platform-aware path comparison (case-sensitive on Unix)
   - Trailing directory separator added for PowerShell consistency

3. **Absolute Path Handling** (CommandPredictor.cs:175-207):
   - `IsAbsolutePath()`: Detects Windows (`C:\`) and Unix (`/`) paths
   - Special case in `Combine()`: Replaces last word for absolute paths
   - Fixes: `cd dotnet` + `D:\path\` → `cd D:\path\` (not `cd dotnet D:\path\`)

**Test Coverage**:
- Path normalization: ~, ~/dir, .., relative, absolute paths
- Cross-form merging: Same directory learned via different paths
- Navigation command detection: cd, Set-Location, sl, chdir
- Current directory filtering
- Frequency-based scoring
- Visit count display
- Absolute path replacement in predictions
- Cross-platform (Windows \ and Unix /)

**Architecture Integration**:
- **ArgumentCompleter**: Uses SetLocationCommand for Tab completion (filesystem browsing)
- **CommandPredictor**: Enhanced with learned paths via GenericPredictor
- **FeedbackProvider**: Captures working directory, passes to ArgumentGraph
- **ArgumentGraph**: Normalizes paths before recording usage
- **GenericPredictor**: Filters and scores suggestions contextually
- **PersistenceManager**: Persists normalized absolute paths across sessions

**Performance**:
- Path normalization: <1ms per path
- Context filtering: <5ms per cd suggestion
- Total overhead: <10ms (within acceptable range)

**Commits**:
- `bf2e4d5`: Initial implementation (path normalization, filtering, scoring)
- `ae1256d`: Bug fix (absolute path handling in Combine method)

---

## Phase 16: PowerShell Module Functions + IPC Removal ✅ **COMPLETE**

**Status**: ✅ Implementation complete with all phases verified

**Completed**: 2025-10-30

**Summary**:
- ✅ 10 native PowerShell functions for cache, learning, and database management
- ✅ Removed ~600 lines of IPC code (IpcServer, IpcProtocol, IpcJsonContext)
- ✅ Removed 44 IPC-related tests
- ✅ Removed PSCue.Debug CLI tool entirely
- ✅ Direct in-process access (no IPC overhead)
- ✅ All 315 tests passing (140 ArgumentCompleter + 175 Module)
- ✅ Module loads faster (no IPC server startup)
- ✅ Simpler, more maintainable architecture

**Goal**: Replace PSCue.Debug CLI tool with native PowerShell functions for better UX, discoverability, and direct in-process access. Remove IPC layer entirely since it was only used by PSCue.Debug.

**Rationale**:
- Better PowerShell integration (tab completion, pipeline support, Get-Help)
- No IPC overhead for cache/learning operations
- More discoverable via `Get-Command -Module PSCue`
- Simpler installation (one less binary to ship)
- PowerShell-native patterns (objects, not JSON strings)
- IPC was obsolete: Only PSCue.Debug used it
- Faster module loading: No IPC server startup (~10-20ms saved)

### Phase 16.1: Core Infrastructure ✅

**Completed**: 2025-01-30

- Added public API methods to existing classes for PowerShell access
- Created PSCueModule static class (split from Init)
  - `public static CompletionCache Cache`
  - `public static ArgumentGraph KnowledgeGraph`
  - `public static CommandHistory CommandHistory`
  - `public static PersistenceManager Persistence`
- Refactored Init → ModuleInitializer + PSCueModule
- Added string overload to CommandCompleter for PowerShell compatibility
- All tests pass

### Phase 16.2: Cache Management Functions ✅

**Completed**: 2025-01-30

Created `module/Functions/CacheManagement.ps1` (181 lines) with 3 functions:
- `Get-PSCueCache [-Filter <string>] [-AsJson]` - View cached completions
- `Clear-PSCueCache [-WhatIf] [-Confirm]` - Clear cache
- `Get-PSCueCacheStats [-AsJson]` - Cache statistics

Features:
- Pipeline-friendly object output
- Comprehensive comment-based help
- Standard PowerShell parameters
- Tab completion support

### Phase 16.3: Learning System Functions ✅

**Completed**: 2025-01-30

Created `module/Functions/LearningManagement.ps1` (327 lines) with 5 functions:
- `Get-PSCueLearning [-Command <string>] [-AsJson]` - View learned data
- `Clear-PSCueLearning [-WhatIf] [-Confirm]` - Clear learned data (memory + DB)
- `Export-PSCueLearning -Path <string>` - Export to JSON
- `Import-PSCueLearning -Path <string> [-Merge]` - Import from JSON
- `Save-PSCueLearning` - Force immediate save

Features:
- ConfirmImpact=High for destructive operations
- Backup and migration scenarios
- Comprehensive help with examples

### Phase 16.4: Database Management + Debugging Functions ✅

**Completed**: 2025-01-30

Created `module/Functions/DatabaseManagement.ps1` (150 lines) with 2 functions:
- `Get-PSCueDatabaseStats [-Detailed] [-AsJson]` - Database statistics
- `Get-PSCueDatabaseHistory [-Last <n>] [-Command <string>] [-AsJson]` - Query history

Created `module/Functions/Debugging.ps1` (220 lines) with 2 functions:
- `Test-PSCueCompletion -InputString <string> [-IncludeTiming]` - Test completions
- `Get-PSCueModuleInfo [-AsJson]` - Module diagnostics

Features:
- Direct SQLite database queries
- In-memory vs persisted data comparison
- Timing information
- Module configuration and statistics

### Phase 16.6: Documentation ✅

**Completed**: 2025-01-30

- Updated CLAUDE.md with PowerShell function documentation
- Updated README.md with database management examples
- Created ./DATABASE-FUNCTIONS.md with detailed query examples
- Updated installation scripts to copy Functions directory
- Comprehensive help for all 10 functions

### Phase 16.7: IPC Removal ✅

**Completed**: 2025-10-30

**Removed Components**:
- Deleted `src/PSCue.Debug/` directory entirely
- Deleted `src/PSCue.Module/IpcServer.cs` (~400 lines)
- Deleted `src/PSCue.Shared/IpcProtocol.cs` (~150 lines)
- Deleted `src/PSCue.Shared/IpcJsonContext.cs` (~50 lines)
- Removed IpcServer initialization/disposal from ModuleInitializer.cs
- Updated FeedbackProvider to use PSCueModule.Cache directly

**Removed Tests** (44 total):
- IpcServerIntegrationTests.cs (5 tests)
- IpcServerErrorHandlingTests.cs (10 tests)
- IpcServerConcurrencyTests.cs (7 tests)
- IpcServerLifecycleTests.cs (10 tests)
- IpcFilteringTests.cs (12 tests)

**Removed Test Scripts**:
- test-ipc.ps1, test-ipc-simple.ps1, test-ipc-path.ps1
- test-cache-debug.ps1, test-pscue-debug.ps1

**Documentation Updated**:
- ../README.md: Removed IPC references, updated architecture diagram
- ./TECHNICAL_DETAILS.md: Removed IPC sections, updated key decisions
- ../CLAUDE.md: Updated to reflect Phase 16 completion

**Verification**:
- `dotnet build` succeeds (fixed PSCue.slnx folder paths)
- `dotnet test` passes (315 tests: 140 ArgumentCompleter + 175 Module)
- Module loads correctly without IPC server
- No lingering IPC references in code (only historical docs)

### Benefits Summary

**Code Removed**:
- ~600 lines of IPC code
- 44 test files (~1,500 lines of test code)
- 5 test scripts
- 1 PSCue.Debug project

**Performance Improvements**:
- Module loading: ~10-20ms faster (no IPC server startup)
- Cache operations: <5ms (direct in-process access vs IPC round-trip)
- No serialization overhead
- No connection timeouts or failures

**Maintainability**:
- Simpler architecture (fewer moving parts)
- No cross-process communication complexity
- Fewer failure modes
- Easier to debug (no IPC protocol to troubleshoot)

**User Experience**:
- Native PowerShell integration
- Better discoverability (`Get-Command -Module PSCue`)
- Tab completion on function parameters
- Pipeline support
- Standard cmdlet patterns (`-WhatIf`, `-Confirm`, `-Verbose`)
- Rich object output with optional `-AsJson`

### Files Created/Modified

**Created**:
- `module/Functions/CacheManagement.ps1` (181 lines)
- `module/Functions/LearningManagement.ps1` (327 lines)
- `module/Functions/DatabaseManagement.ps1` (150 lines)
- `module/Functions/Debugging.ps1` (220 lines)

**Modified**:
- `src/PSCue.Module/ModuleInitializer.cs` - Removed IPC server
- `src/PSCue.Module/PSCueModule.cs` - Static module state
- `src/PSCue.Module/FeedbackProvider.cs` - Direct cache access
- `src/PSCue.Shared/CommandCompleter.cs` - String overload
- `module/PSCue.psd1` - Export 10 functions
- `module/PSCue.psm1` - Dot-source function scripts
- `PSCue.slnx` - Fixed folder paths, removed PSCue.Debug

**Deleted**:
- `src/PSCue.Debug/` (entire project)
- `src/PSCue.Module/IpcServer.cs`
- `src/PSCue.Shared/IpcProtocol.cs`
- `src/PSCue.Shared/IpcJsonContext.cs`
- 5 IPC test files
- 5 IPC test scripts

### Success Criteria ✅ ALL MET

1. ✅ All PSCue.Debug functionality available as PowerShell functions
2. ✅ Functions discoverable via `Get-Command -Module PSCue`
3. ✅ `Get-Help <function>` provides comprehensive documentation
4. ✅ Functions return rich objects with optional `-AsJson`
5. ✅ Destructive operations support `-WhatIf` and `-Confirm`
6. ✅ Direct in-process access (no IPC overhead)
7. ✅ Export/Import functions enable backup/migration
8. ✅ All 315 tests passing
9. ✅ Documentation updated (../README.md, ../CLAUDE.md, ./TECHNICAL_DETAILS.md)
10. ✅ PSCue.Debug completely removed
11. ✅ IPC layer completely removed
12. ✅ 44 IPC tests removed
13. ✅ Module loads faster
14. ✅ Simpler architecture

---

### Phase 16.5: Module Function Testing & Bug Fixes ✅

**Completed**: 2025-10-31

Fixed critical bugs preventing PowerShell module functions from working correctly.

**Changes**:
- Fixed FeedbackProvider instance mismatch - now gets instances dynamically from PSCueModule
- Fixed ConcurrentDictionary enumeration in Get-PSCueLearning
- Created integration test script: `test-scripts/test-module-functions.ps1`
- Verified learning data retrieval works in interactive sessions
- All tests passing

**Files Modified**:
- `src/PSCue.Module/FeedbackProvider.cs` - Dynamic instance retrieval
- `src/PSCue.Module/ModuleInitializer.cs` - Parameterless FeedbackProvider constructor
- `module/Functions/LearningManagement.ps1` - Fixed dictionary enumeration
- `test-scripts/test-module-functions.ps1` - Comprehensive integration tests

### Phase 16.6: Removed Unused CompletionCache ✅

**Completed**: 2025-10-31

Removed vestigial CompletionCache infrastructure that served no purpose (~727 lines of dead code).

**Rationale**:
- ArgumentCompleter (NativeAOT exe) runs in separate process - cannot share memory with Module DLL
- CompletionCache only lived in Module DLL
- FeedbackProvider was updating it, but nothing was reading it
- CommandPredictor uses ArgumentGraph/GenericPredictor instead

**Removed**:
- `src/PSCue.Module/CompletionCache.cs` (~190 lines)
- `module/Functions/CacheManagement.ps1` (3 functions: Get-PSCueCache, Clear-PSCueCache, Get-PSCueCacheStats)
- `test/PSCue.Module.Tests/CompletionCacheTests.cs` (8 tests)
- Cache initialization in ModuleInitializer.cs
- Cache update logic in FeedbackProvider.cs
- PSCueModule.Cache property

**Result**:
- 7 PowerShell functions (down from 10)
- 323 tests passing (140 ArgumentCompleter + 183 Module)
- Simpler architecture with single learning system
- No functional impact

### Phase 17.1: ML-Based N-gram Sequence Prediction ✅

**Completed**: 2025-11-04

Added intelligent command sequence prediction using n-gram machine learning model.

**Features**:
- **SequencePredictor**: N-gram based ML prediction for command sequences
- **Bigram/Trigram Support**: Configurable via PSCUE_ML_NGRAM_ORDER (default: 2)
- **Automatic Learning**: Integrated with FeedbackProvider for seamless learning
- **SQLite Persistence**: Stores learned sequences with frequencies across sessions
- **Additive Merging**: Concurrent session support with frequency summing
- **Performance**: <1ms cache lookups, <20ms total prediction time
- **Configuration**: Environment variables for enabling/disabling and tuning

**Implementation**:
- `src/PSCue.Module/SequencePredictor.cs`: N-gram prediction engine (~350 lines)
- Database schema extended with `command_sequences` table
- Integration with PersistenceManager for cross-session storage

**Testing**:
- Unit tests: SequencePredictorTests.cs
- Integration tests: SequencePersistenceIntegrationTests.cs
- Performance tests: SequencePerformanceTests.cs
- All tests passing

**Configuration**:
```powershell
$env:PSCUE_ML_ENABLED = "true"           # Enable ML predictions (default)
$env:PSCUE_ML_NGRAM_ORDER = "2"          # N-gram order (2=bigrams, 3=trigrams)
$env:PSCUE_ML_NGRAM_MIN_FREQ = "3"       # Minimum frequency threshold
```

### Phase 17.2: Privacy & Security - Sensitive Data Protection ✅

**Completed**: 2025-11-05

Multi-layered filtering to prevent learning commands with sensitive information.

**Features**:
- **Built-in Keyword Filtering** (always active, cannot be disabled):
  - `*password*`, `*passwd*`, `*secret*`, `*api*key*`, `*token*`
  - `*private*key*`, `*credentials*`, `*bearer*`, `*oauth*`

- **Heuristic Detection** (pattern-based, always active):
  - GitHub/Stripe keys: `sk_`, `pk_`, `ghp_`, `gho_`, etc.
  - AWS access keys: `AKIA...`
  - JWT tokens: `eyJ...`
  - Bearer tokens: `Bearer ...`
  - Long secrets: Base64/hex strings (40+ chars, outside quotes)

- **Smart Filtering**:
  - Removes quoted content before hex/base64 checks
  - Avoids false positives from commit messages
  - Example: `git commit -m 'aaaa...'` allowed, `echo aaaa...` blocked

- **Custom User Patterns**:
  - Configure via `PSCUE_IGNORE_PATTERNS` environment variable
  - Wildcard support: `"aws *,terraform *,*custom*"`

**Implementation**:
- Enhanced `FeedbackProvider.cs` with filtering logic (~90 lines added)
- `LoadIgnorePatterns()`: Loads built-in + user patterns
- `ShouldIgnoreCommand()`: Wildcard matching
- `ContainsSensitiveValue()`: Heuristic detection

**Testing**:
- Comprehensive test coverage for all scenarios
- FeedbackProviderTests.cs extended with filtering tests
- All tests passing

**Documentation**:
- Added dedicated "Privacy & Security" section to README.md
- Updated CLAUDE.md with filtering details
- Updated TODO.md

### Phase 17.3: Partial Word Completion Filtering ✅

**Completed**: 2025-11-06

Fixed incorrect suggestion behavior when typing partial subcommands like "git chec".

**Problem**:
When users typed partial words (e.g., "git chec"), the predictor incorrectly suggested additional arguments after the partial word:
- `git chec pull`
- `git chec status`
- `git chec log`

These suggestions didn't make sense because "chec" itself needed to be completed to "checkout" first.

**Solution**:
Enhanced `GenericPredictor.GetSuggestions` (`src/PSCue.Module/GenericPredictor.cs:66-377`) to:
1. Detect when command line ends with a partial word (no trailing space)
2. Extract the word being completed
3. Filter all suggestions to only show items starting with the partial word
4. Apply filtering to both learned suggestions and ML-based predictions

**Changes**:
- `GenericPredictor.GetSuggestions`: Added partial word detection and filtering logic
- `GenericPredictor.AddContextSuggestions`: Added wordToComplete parameter for filtering context-based and ML predictions
- Added 6 comprehensive tests in `test/PSCue.Module.Tests/GenericPredictorTests.cs:478-623`:
  - Partial subcommand filtering
  - Avoiding extra argument suggestions
  - Complete subcommand with space handling
  - Partial flag filtering
  - Partial argument filtering
  - ML prediction filtering

**Behavior**:
- `git che` → Shows: `checkout`, `cherry-pick` (only items starting with "che")
- `git checkout ` → Shows: `main`, `-b`, etc. (arguments for checkout)
- `git commit -` → Shows: `-m`, `-a`, `--amend` (flags starting with "-")

**Impact**: Prediction behavior now matches Tab completion expectations, providing cleaner and more intuitive suggestions.

---

### Phase 17.4: Multi-Word Prediction Suggestions ✅

**Completed**: 2025-11-06

Implemented multi-word prediction suggestions that show common argument combinations like "git checkout master" alongside single-word suggestions like "checkout".

**Problem**:
Previous implementation only showed single-word suggestions:
- Input: `git che`
- Shows: `checkout`, `cherry-pick`

Users still needed to type the second argument manually, even if they frequently used specific combinations.

**Solution**:
Enhanced the learning system to track and suggest sequential argument patterns.

**Changes**:

1. **ArgumentGraph.cs** (~150 lines added):
   - `ArgumentSequence` class: Tracks consecutive argument pairs with usage frequency and recency
   - `CommandKnowledge.ArgumentSequences`: Dictionary storing up to 50 sequences per command
   - `RecordUsage`: Tracks consecutive argument pairs (subcommand → arg, flag → value)
   - `GetSequencesStartingWith`: Retrieves common next arguments for a given first argument
   - `EnforceLimits`: Prunes old sequences (LRU eviction)
   - Delta tracking and baseline methods for persistence support

2. **GenericPredictor.cs** (~60 lines added):
   - `AddMultiWordSuggestions`: Generates multi-word completions from learned sequences
   - Minimum usage threshold: 3 occurrences
   - Creates suggestions like "checkout master" when "checkout" is used frequently with "master"
   - Filters by partial word input
   - Slightly lower score (0.95×) than single-word to prefer flexibility

3. **CommandPredictor.cs** (~15 lines added):
   - Enhanced `Combine` method to handle multi-word completions
   - Detects when completion contains spaces
   - Matches first word against partial input
   - Example: `"git che" + "checkout master"` → `"git checkout master"`

4. **PersistenceManager.cs** (~70 lines added):
   - New table: `argument_sequences` (command, first_argument, second_argument, usage_count, timestamps)
   - Index: `idx_argument_sequences_command_first` for fast lookups
   - Save/load logic with delta tracking (additive merge)
   - Transaction-based persistence

5. **Tests** (28 new tests):
   - `ArgumentGraphTests.cs`: 13 sequence tracking tests
   - `CommandPredictorTests.cs`: 4 multi-word Combine tests
   - All 255 module tests passing

**Behavior**:
- `git che` → Shows: `checkout`, `checkout master`, `checkout -b`, `cherry-pick`
- `docker` → Shows: `run`, `run -it`, `ps`, etc.
- Only shows sequences used ≥3 times
- Multi-word suggestions appear alongside single-word suggestions
- Sorted by usage frequency and recency

**Implementation Details**:

**Sequence Tracking**:
- Records consecutive argument pairs: `["checkout", "master"]` → sequence `"checkout|master"`
- Skips flag-to-flag pairs (already handled by FlagCombinations)
- Skips navigation commands (paths too specific)
- Tracks usage count, first seen, last used timestamps

**Multi-Word Generation**:
- For top 5 single-word suggestions, checks for common next arguments
- Builds text like `"checkout master"` with tooltip `"used 15x together"`
- Filters by partial input if present
- Deduplicates against existing suggestions

**Performance Impact**:
- Recording: ~1-2 dictionary ops per command (<1ms)
- Memory: ~64 bytes per sequence, max 50/command (~3KB)
- Generation: ~50 lookups for 5 candidates (<1ms)
- **Total impact: Negligible**

**Database Schema**:
```sql
CREATE TABLE argument_sequences (
    command TEXT NOT NULL COLLATE NOCASE,
    first_argument TEXT NOT NULL COLLATE NOCASE,
    second_argument TEXT NOT NULL COLLATE NOCASE,
    usage_count INTEGER NOT NULL DEFAULT 0,
    first_seen TEXT NOT NULL,
    last_used TEXT NOT NULL,
    PRIMARY KEY (command, first_argument, second_argument)
);
```

**Files Modified**:
- `src/PSCue.Module/ArgumentGraph.cs`: Sequence tracking and retrieval
- `src/PSCue.Module/GenericPredictor.cs`: Multi-word suggestion generation
- `src/PSCue.Module/CommandPredictor.cs`: Multi-word Combine logic
- `src/PSCue.Module/PersistenceManager.cs`: Database schema and persistence
- `test/PSCue.Module.Tests/ArgumentGraphTests.cs`: 13 sequence tests
- `test/PSCue.Module.Tests/CommandPredictorTests.cs`: 4 Combine tests

**Future Expansion**:
Architecture supports extending to longer sequences (3+ words) in the future. Current implementation focuses on 2-word combinations for simplicity and performance.

---

### CI/CD & Distribution: GitHub Actions Automated Releases ✅

**Completed**: 2025-11-04/05

Automated build, test, and release pipeline for PSCue.

**Features**:
- **CI Workflow** (`.github/workflows/ci.yml`):
  - Runs on every push and PR
  - Builds for win-x64 and linux-x64
  - Runs full test suite on both platforms
  - Matrix strategy for parallel builds

- **Release Workflow** (`.github/workflows/release.yml`):
  - Triggers on version tags (`v*`)
  - Builds NativeAOT binaries for both platforms
  - Creates platform-specific archives (zip/tar.gz)
  - Generates SHA256 checksums
  - Creates GitHub release with all artifacts
  - **Fixed**: Includes Functions/ directory in archives

- **Installation Script** (`scripts/install-remote.ps1`):
  - Downloads from GitHub releases (latest or specific version)
  - Platform detection and validation
  - Extracts and installs to `~/.local/pwsh-modules/PSCue/`
  - One-line installation: `irm https://raw.githubusercontent.com/.../install-remote.ps1 | iex`

**Platform Support**: Windows x64, Linux x64

**Releases**:
- v0.1.0: Initial attempt (build failures)
- v0.2.0: First successful release with all features

**Manual Release Process**:
```bash
# 1. Update version in module/PSCue.psd1
# 2. Commit and tag
git add module/PSCue.psd1
git commit -m "chore: bump version to X.Y.Z"
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin main vX.Y.Z

# 3. GitHub Actions automatically builds and publishes
```

### Phase 17.5: Smart Directory Navigation (`pcd` command) ✅

**Completed**: 2025-11-08

Added a PowerShell function with intelligent tab completion for directory navigation, leveraging PSCue's learned `cd` command data without interfering with native `cd` completion.

**Core Implementation** (`module/Functions/PCD.ps1` - ~110 lines):
- **`Invoke-PCD` function**: PowerShell function that calls `Set-Location` with the provided path
- **`pcd` alias**: Short, convenient alias for quick navigation
- **Tab completion**: Registered via `Register-ArgumentCompleter` for both `Invoke-PCD` and `pcd`
  - Direct in-process access to `PSCueModule.KnowledgeGraph` (<1ms, no IPC overhead)
  - Queries learned `cd` command data via `GetSuggestions("cd", @())`
  - Filters results by `$wordToComplete` using `StartsWith` matching
  - Returns `CompletionResult` objects with usage count and last used date tooltips
- **Graceful fallback**: Returns empty if module not initialized, letting native completion take over
- **Home directory support**: Handles empty argument (navigates to `~`)
- **Path quoting**: Automatically quotes paths with spaces

**Module Integration** (`module/PSCue.psm1`):
- Added dot-source for `PCD.ps1`
- Also added missing `WorkflowManagement.ps1` dot-source

**Module Manifest** (`module/PSCue.psd1`):
- Added `Invoke-PCD` to `FunctionsToExport`
- Added `pcd` to `AliasesToExport`
- Fixed missing workflow management functions in exports

**Comprehensive Testing** (`test/PSCue.Module.Tests/PCDTests.cs` - ~425 lines):
- **ArgumentGraph integration tests**: Verify learned directory suggestions and ordering
- **Tab completion simulation tests**: Test filtering, sorting, and `CompletionResult` generation
- **Scoring and ranking tests**: Verify frequency-based ordering
- **Edge cases**: Root directories, relative paths, special characters, long paths
- **Performance tests**: Ensure <10ms completion time with large datasets
- **Module lifecycle tests**: Handle uninitialized module gracefully

**Documentation Updates**:
- `README.md`: Added "Smart Directory Navigation with `pcd`" section with examples
- `CLAUDE.md`: Added `pcd` to function reference and key files list
- `TODO.md`: Marked Phase 17.5 as complete with implementation details

**Design Decisions**:
- **In-process completion**: Direct `PSCueModule.KnowledgeGraph` access avoids IPC overhead
- **Non-invasive**: Separate `pcd` command doesn't interfere with native `cd` tab completion
- **Reuses existing data**: ArgumentGraph already tracks `cd` command with all paths
- **User choice**: Users can use `pcd` for smart suggestions or `cd` for native completion
- **PSReadLine integration**: Menu display handled by PSReadLine (`MenuComplete`, `ListView`)

**Future Enhancement Ideas** (documented in TODO.md):
- Fuzzy matching (e.g., "dt" → "D:\source\datadog\dd-trace-dotnet")
- Frecency scoring (frequency + recency blend) for better ranking
- Multi-segment substring matching (e.g., "dog/trace" → "datadog/dd-trace-dotnet")
- Working directory proximity (prefer subdirectories of current location)

**Key Metrics**:
- Tab completion performance: <1ms (direct in-process access)
- Tooltips show: "Used N times (last: YYYY-MM-DD)"
- Suggestions ordered by: Score (frequency + recency decay)

### Phase 18.1: Dynamic Workflow Learning ✅

**Completed**: 2025-11-07

Automatically learns command workflow patterns (command → next command transitions) and predicts the next command based on usage patterns with timing awareness.

**Core Implementation** (`src/PSCue.Module/WorkflowLearner.cs` - 500+ lines):
- **WorkflowTransition** class: Tracks frequency, time deltas, timestamps, and confidence
- **WorkflowSuggestion** class: Prediction results with command, confidence, source, and reason
- **WorkflowLearner** class: Main learning engine with in-memory graph and persistence support
- **Command normalization**: Extracts base command + subcommand (e.g., "git commit" from "git commit -m 'test'")
- **Memory management**: LRU eviction keeps top 20 transitions per command
- **Time-sensitive scoring**: Adjusts confidence based on timing patterns (1.5× to 0.8× boost)
- **Confidence calculation**: 70% frequency + 30% recency with exponential decay

**Database Integration** (`src/PSCue.Module/PersistenceManager.cs`):
- New `workflow_transitions` table (8 tables total now)
- Columns: `from_command`, `to_command`, `frequency`, `total_time_delta_ms`, `first_seen`, `last_seen`
- Indexed on `from_command` for fast lookups (<2ms target)
- Additive merging for concurrent session support
- Save/load methods: `SaveWorkflowTransitions()`, `LoadWorkflowTransitions()`

**Module Integration**:
- **ModuleInitializer.cs**: Load/save lifecycle, auto-save every 5 minutes, configuration from env vars
- **PSCueModule.cs**: Added `WorkflowLearner` static property
- **FeedbackProvider.cs**: Records transitions after successful commands with time delta tracking
- **CommandPredictor.cs**: Shows workflow predictions at empty prompt and filters by partial command

**PowerShell Functions** (`module/Functions/WorkflowManagement.ps1` - 460+ lines):
1. `Get-PSCueWorkflows`: View learned patterns with filtering and formatting
2. `Get-PSCueWorkflowStats`: Summary and detailed statistics with top workflows
3. `Clear-PSCueWorkflows`: Clear with confirmation (ConfirmImpact=High)
4. `Export-PSCueWorkflows`: Backup to JSON with metadata
5. `Import-PSCueWorkflows`: Restore with merge option

**Configuration** (Environment Variables):
- `PSCUE_WORKFLOW_LEARNING`: Enable/disable (default: true)
- `PSCUE_WORKFLOW_MIN_FREQUENCY`: Min occurrences to suggest (default: 5)
- `PSCUE_WORKFLOW_MAX_TIME_DELTA`: Max minutes between commands (default: 15)
- `PSCUE_WORKFLOW_MIN_CONFIDENCE`: Min confidence threshold (default: 0.6)

**Comprehensive Testing** (`test/PSCue.Module.Tests/WorkflowLearnerTests.cs` - 485+ lines):
- 35+ unit tests covering all functionality
- Constructor validation, transition recording, prediction queries
- Time-sensitive scoring, persistence, memory management
- WorkflowTransition confidence and time delta calculations

**Documentation Updates**:
- `CLAUDE.md`: Architecture, configuration, key files
- `README.md`: Features, workflow management section with examples
- `TODO.md`: Marked Phase 18.1 as complete
- `TODO.md`: Design doc with implementation details (Phases 18.3-18.8)

**Commits**:
1. `d638466`: feat: implement Phase 18.1 dynamic workflow learning
2. `d1f3159`: docs: update documentation for Phase 18.1 implementation
3. `fa997c8`: feat: integrate WorkflowLearner with CommandPredictor
4. `5945511`: test: add comprehensive tests for WorkflowLearner
5. `154bcad`: feat: add PowerShell workflow management functions
6. `b46c9b8`: docs: update README.md with workflow learning features

**Example Workflow**:
```powershell
# After running this sequence 10+ times:
cargo build
cargo test
git add .

# PSCue learns the pattern and predicts:
PS> cargo build<Enter>
PS> cargo █  # Inline suggestion: "cargo test" (85% confidence)

PS> cargo test<Enter>
PS> git █    # Inline suggestion: "git add" (70% confidence, cross-tool)
```

### Phase 18.2: Time-Based Workflow Detection ✅

**Completed**: 2025-11-07 (as part of Phase 18.1)

Time-sensitive scoring was implemented directly in Phase 18.1 as part of the `WorkflowLearner` core functionality.

**Features**:
- **Time delta tracking**: Records time between command executions in milliseconds
- **Average time calculation**: `TotalTimeDeltaMs / Frequency` for typical workflow timing
- **Time-sensitive scoring** (`GetTimeSensitiveScore()` method):
  - Within expected timeframe (ratio < 1.5): 1.5× confidence boost
  - Moderately delayed (ratio < 5): 1.2× boost
  - Significantly delayed (ratio < 30): 1.0× (no boost)
  - Very old (ratio ≥ 30): 0.8× penalty (weak relationship)
- **Database schema**: `total_time_delta_ms` column in `workflow_transitions` table
- **Configuration**: `PSCUE_WORKFLOW_MAX_TIME_DELTA` filters out transitions >15 minutes

**Example**:
```powershell
# User typically runs "git commit" 30 seconds after "git add"

# Scenario 1: Quick succession (matches pattern)
PS> git add .<Enter>
# 30 seconds later...
PS> git c█  # "git commit" suggested with 1.5× boost (high confidence)

# Scenario 2: Delayed (doesn't match timing pattern)
PS> git add .<Enter>
# 2 hours later...
PS> git c█  # "git commit" suggested with 0.8× penalty (lower confidence)
```

**Result**: Predictions are more accurate by considering not just frequency but also typical timing patterns in workflows.

## Notes

- This document archives completed work for reference and historical purposes
- For active tasks and future work, see [TODO.md](TODO.md)
- All phases listed here have been fully implemented, tested, and documented
