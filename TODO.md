# PSCue Module - Implementation Plan

## Overview

PSCue is a unified PowerShell module that combines:
1. **Argument Completer** (from pwsh-argument-completer) - Native argument completion using `Register-ArgumentCompleter`
2. **Command Predictor** (from pwsh-command-predictor) - ICommandPredictor for inline suggestions
3. **Feedback Provider** - IFeedbackProvider for learning from successful command execution (PowerShell 7.4+)
4. **Future Features** - ML-based completions, etc.

---

## Directory Structure

```
PSCue/
├── src/
│   ├── PSCue.ArgumentCompleter/         # Native executable (C#, NativeAOT)
│   │   ├── PSCue.ArgumentCompleter.csproj
│   │   ├── Program.cs                   # Entry point for Tab completion
│   │   ├── AssemblyInfo.cs              # NativeAOT trim settings
│   │   └── IpcClient.cs                 # Named Pipe client for communicating with Predictor ✅
│   │
│   ├── PSCue.Module/          # DLL for ICommandPredictor + IFeedbackProvider (C#)
│   │   ├── PSCue.Module.csproj
│   │   ├── Init.cs                      # IModuleAssemblyInitializer - auto-registers predictor & feedback provider ✅
│   │   ├── CommandCompleterPredictor.cs # ICommandPredictor implementation ✅
│   │   ├── FeedbackProvider.cs          # IFeedbackProvider - learns from successful command execution ✅
│   │   ├── IpcServer.cs                 # Named Pipe server for serving completions ✅
│   │   └── CompletionCache.cs           # Cache with usage tracking and learning ✅
│   │
│   ├── PSCue.Shared/                    # Shared completion logic ✅
│   │   ├── PSCue.Shared.csproj
│   │   ├── CommandCompleter.cs          # Main completion orchestrator
│   │   ├── Logger.cs                    # Debug logging (shared, concurrent write support) ✅
│   │   ├── Helpers.cs                   # Utility functions
│   │   ├── Completions/                 # Completion framework
│   │   │   ├── ICompletion.cs
│   │   │   ├── Command.cs
│   │   │   ├── CommandParameter.cs
│   │   │   ├── StaticArgument.cs
│   │   │   └── DynamicArgument.cs
│   │   ├── KnownCompletions/            # Command-specific completions
│   │   │   ├── GitCommand.cs
│   │   │   ├── GhCommand.cs
│   │   │   ├── ScoopCommand.cs
│   │   │   ├── WingetCommand.cs
│   │   │   └── Azure/
│   │   │       ├── AzCommand.cs
│   │   │       ├── AzdCommand.cs
│   │   │       └── FuncCommand.cs
│   │   ├── IpcProtocol.cs               # IPC protocol definitions ✅
│   │   └── IpcJsonContext.cs            # JSON source generation for NativeAOT ✅
│   │
│   └── PSCue.Debug/                     # Debug/testing tool ✅
│       ├── PSCue.Debug.csproj
│       └── Program.cs
│
├── module/
│   ├── PSCue.psd1                       # Module manifest
│   └── PSCue.psm1                       # Module script
│
├── test/
│   ├── PSCue.ArgumentCompleter.Tests/
│   │   └── PSCue.ArgumentCompleter.Tests.csproj
│   └── PSCue.Module.Tests/
│       └── PSCue.Module.Tests.csproj
│
├── ai/                                   # AI model setup scripts (future)
│   ├── setup-ai-model.ps1
│   └── ...
│
├── .github/
│   └── workflows/
│       ├── ci.yml                       # CI workflow (build, test, lint)
│       └── release.yml                  # Release workflow (build binaries, create release)
│
├── scripts/
│   ├── install-local.ps1                # Build from source and install (local dev)
│   └── install-remote.ps1               # Download and install from GitHub release
│
├── PSCue.sln                            # Solution file
├── README.md
├── LICENSE
└── .gitignore
```

---

## Module Architecture

### PSCue.psd1 (Module Manifest)
- RootModule: `PSCue.psm1`
- ModuleVersion: `1.0.0`
- RequiredAssemblies: `PSCue.Module.dll`
- FunctionsToExport: `@()` (initially)
- Author: Lucas Pimentel
- Description: Unified PowerShell completion and prediction module

### PSCue.psm1 (Module Script)
- Load and register argument completers (calls the native executable)
- The Predictor DLL auto-registers via `IModuleAssemblyInitializer`
- Register completers for: git, code, az, azd, func, chezmoi, gh, tre, lsd, dust, scoop (Windows), winget (Windows)

---

## Build Process

### Multi-stage build approach:

1. **Build PSCue.ArgumentCompleter** (NativeAOT):
   - Publish as native executable per platform:
     - Windows: win-x64
     - macOS Intel: osx-x64
     - macOS Apple Silicon: osx-arm64
     - Linux: linux-x64
   - Output: `pscue-completer.exe` / `pscue-completer`
   - NativeAOT settings:
     - PublishAot: true
     - OptimizationPreference: Speed
     - InvariantGlobalization: true

2. **Build PSCue.Module** (Managed DLL):
   - Build as Release for net9.0
   - Output: `PSCue.Module.dll`
   - References PSCue.ArgumentCompleter as a project reference (for shared code)
   - Includes PowerShell SDK dependency

3. **Test Projects**:
   - Build and run tests for both ArgumentCompleter and Predictor

---

## Installation Strategies

PSCue supports two installation methods: local (build from source) and remote (download pre-built binaries).

### 1. Local Installation (Development/Source)

**Script**: `scripts/install-local.ps1`

**Purpose**: For developers or users who want to build from source

**Usage**:
```powershell
# Clone the repository
git clone https://github.com/lucaspimentel/PSCue.git
cd PSCue

# Run the local installation script
./scripts/install-local.ps1
```

**Workflow**:
1. Detect platform (Windows/macOS/Linux, x64/arm64)
2. Build ArgumentCompleter with NativeAOT for the detected platform
   - `dotnet publish src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj -c Release -r <rid> -o src/PSCue.ArgumentCompleter/publish`
3. Build CommandPredictor as managed DLL
   - `dotnet build src/PSCue.Module/PSCue.Module.csproj -c Release`
4. Create installation directory: `~/.local/pwsh-modules/PSCue/`
5. Copy files to installation directory:
   - Native executable: `pscue-completer[.exe]`
   - CommandPredictor DLL: `PSCue.Module.dll`
   - Module files: `PSCue.psd1`, `PSCue.psm1`
6. Display instructions for adding to `$PROFILE`

### 2. Remote Installation (End Users)

**Script**: `scripts/install-remote.ps1`

**Purpose**: One-liner installation for end users from GitHub releases

**Usage**:
```powershell
# One-line remote installation (latest version)
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex

# Install specific version
$version = "1.0.0"; irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

**Workflow**:
1. Accept optional `$version` variable from caller (defaults to "latest")
2. Detect platform (Windows/macOS/Linux, x64/arm64)
3. Map platform to release asset name:
   - Windows x64: `PSCue-win-x64.zip`
   - macOS x64: `PSCue-osx-x64.tar.gz`
   - macOS arm64: `PSCue-osx-arm64.tar.gz`
   - Linux x64: `PSCue-linux-x64.tar.gz`
4. Determine download URL:
   - If version is "latest": Query GitHub API `https://api.github.com/repos/lucaspimentel/PSCue/releases/latest`
   - Otherwise: Use `https://github.com/lucaspimentel/PSCue/releases/download/v{version}/{asset}`
5. Download release asset to temp directory
6. Extract archive to temp location
7. Create installation directory: `~/.local/pwsh-modules/PSCue/`
8. Copy files from extracted archive:
   - Native executable: `pscue-completer[.exe]`
   - CommandPredictor DLL: `PSCue.Module.dll`
   - Module files: `PSCue.psd1`, `PSCue.psm1`
9. Clean up temp files
10. Display instructions for adding to `$PROFILE`

**Key Features**:
- No build tools required (no .NET SDK needed)
- Downloads pre-built binaries from GitHub releases
- Fast installation
- Supports version pinning
- Platform auto-detection
- Checksum verification (future enhancement)

### User's Profile Setup

After either installation method, users add these lines to their PowerShell profile:

```powershell
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1
Set-PSReadLineOption -PredictionSource HistoryAndPlugin
```

---

## GitHub Actions & CI/CD

### CI Workflow (.github/workflows/ci.yml)

**Triggers**:
- Push to main branch
- Pull requests to main branch

**Jobs**:

1. **Build & Test (Matrix)**:
   - **Platforms**: ubuntu-latest, windows-latest, macos-latest
   - **Steps**:
     - Checkout code
     - Setup .NET 9.0 SDK
     - Restore dependencies: `dotnet restore`
     - Build solution: `dotnet build --configuration Release`
     - Run tests: `dotnet test --configuration Release --no-build --verbosity normal`
     - Upload test results as artifacts

2. **Lint & Format**:
   - Check code formatting: `dotnet format --verify-no-changes`
   - Run static analysis (optional): `dotnet analyze`

**Status Badge**: Add to README.md
```markdown
[![CI](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml/badge.svg)](https://github.com/lucaspimentel/PSCue/actions/workflows/ci.yml)
```

### Release Workflow (.github/workflows/release.yml)

**Triggers**:
- Manual workflow dispatch (workflow_dispatch)
- Git tags matching `v*` (e.g., `v1.0.0`)

**Jobs**:

1. **Build Native Binaries (Matrix)**:
   - **Matrix dimensions**:
     - Platform: windows, macos, linux
     - Architecture: x64, arm64 (macOS only)
   - **Steps**:
     - Checkout code
     - Setup .NET 9.0 SDK
     - Publish ArgumentCompleter for each RID:
       - `dotnet publish src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj -c Release -r win-x64 -o dist/win-x64`
       - `dotnet publish src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj -c Release -r osx-x64 -o dist/osx-x64`
       - `dotnet publish src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj -c Release -r osx-arm64 -o dist/osx-arm64`
       - `dotnet publish src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj -c Release -r linux-x64 -o dist/linux-x64`
     - Build CommandPredictor DLL:
       - `dotnet build src/PSCue.Module/PSCue.Module.csproj -c Release -o dist/common`
     - Copy module files (PSCue.psd1, PSCue.psm1) to each platform dist folder
     - Create platform-specific archives:
       - Windows: Zip file `PSCue-win-x64.zip`
       - macOS/Linux: Tar.gz files `PSCue-osx-x64.tar.gz`, `PSCue-osx-arm64.tar.gz`, `PSCue-linux-x64.tar.gz`
     - Generate checksums (SHA256) for each archive
     - Upload archives as artifacts

2. **Create GitHub Release**:
   - **Depends on**: Build Native Binaries job
   - **Steps**:
     - Download all build artifacts
     - Extract version from tag (e.g., `v1.0.0` → `1.0.0`)
     - Create GitHub release using `softprops/action-gh-release@v1`:
       - Tag: Current tag
       - Name: `PSCue v{version}`
       - Body: Auto-generated release notes from commits
       - Files: All platform archives + checksums
       - Draft: false
       - Prerelease: Detect from version (e.g., `v1.0.0-beta`)
     - Update `latest` tag (for remote installer)

**Release Assets Structure**:
```
PSCue-win-x64.zip
PSCue-osx-x64.tar.gz
PSCue-osx-arm64.tar.gz
PSCue-linux-x64.tar.gz
checksums.txt
```

**Each archive contains**:
```
pscue-completer[.exe]      # Native executable
PSCue.Module.dll # CommandPredictor assembly
PSCue.psd1                 # Module manifest
PSCue.psm1                 # Module script
LICENSE                    # License file
README.md                  # Installation instructions
```

### Creating a Release

**Manual release process**:
```bash
# 1. Update version in module manifest (module/PSCue.psd1)
# 2. Commit version bump
git add module/PSCue.psd1
git commit -m "Bump version to 1.0.0"

# 3. Create and push tag
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# 4. GitHub Actions automatically builds and creates release
```

**Automated version bumping** (future enhancement):
- Use semantic release or similar tool
- Auto-generate changelog from conventional commits
- Auto-bump version based on commit messages

---

## Implementation Phases

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
- [x] Document IPC protocol extensions (TECHNICAL_DETAILS.md updated)
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

#### Architecture Changes

**Current State** (Phase 9):
- ICommandPredictor calls into CommandCompleter (command-specific logic)
- Only provides suggestions for explicitly supported commands
- Learning is limited to scoring known completions

**Target State** (Phase 11):
- ICommandPredictor has generic learning engine + command-specific augmentation
- Learns from ALL commands via IFeedbackProvider
- Builds knowledge graph of command → arguments from actual usage
- Uses context (recent commands) to provide better suggestions
- Supplements known commands with learned data

#### Data Structures

**11.1: Command History Store** ✅
- [x] Create `CommandHistory.cs` in PSCue.Module
- [x] Track recent commands (last N commands, configurable, default 100)
- [x] Store: command name, arguments, flags, timestamp, success/failure
- [x] Ring buffer implementation for memory efficiency
- [x] Thread-safe (concurrent access from predictor & feedback provider)
- [ ] Example:
  ```csharp
  public class CommandHistoryEntry
  {
      public string Command { get; set; }          // "git"
      public string[] Arguments { get; set; }      // ["commit", "-m", "message"]
      public DateTime Timestamp { get; set; }
      public bool Success { get; set; }
  }
  ```

**11.2: Argument Knowledge Graph** ✅
- [x] Create `ArgumentGraph.cs` in PSCue.Module
- [x] Track per-command argument patterns:
  - [x] Which arguments/flags are used with each command
  - [x] Frequency of each argument
  - [x] Co-occurrence patterns (which flags appear together)
  - [x] Position information (flags vs positional args)
- [ ] Data structure:
  ```csharp
  public class CommandKnowledge
  {
      public string Command { get; set; }
      public Dictionary<string, ArgumentStats> Arguments { get; set; }
      public Dictionary<string, int> FlagCombinations { get; set; } // e.g., "-la" -> count
  }

  public class ArgumentStats
  {
      public string Argument { get; set; }
      public int UsageCount { get; set; }
      public DateTime FirstSeen { get; set; }
      public DateTime LastUsed { get; set; }
      public bool IsFlag { get; set; }           // starts with - or --
      public List<string> CoOccursWith { get; set; } // other args seen together
  }
  ```
- [ ] Efficient lookup by command name
- [ ] Aging mechanism (decay old patterns)
- [ ] Maximum size limits (prevent unbounded growth)

**11.3: Context Analyzer** ✅
- [ ] Create `ContextAnalyzer.cs` in PSCue.Module
- [ ] Analyze recent command history for patterns:
  - [ ] Current directory changes (cd commands)
  - [ ] File operations (ls, cat, vim) → suggest related files
  - [ ] Git workflows (add → commit → push sequences)
  - [ ] Docker workflows (build → run sequences)
- [ ] Detect command sequences and suggest next likely command
- [ ] Extract relevant context for current prediction

#### Learning System Updates

**11.4: Enhance FeedbackProvider** ✅
- [ ] Extend `FeedbackProvider.cs` to extract more from `FeedbackContext`:
  - [ ] Parse command line into: command + arguments + flags
  - [ ] Identify flag patterns (e.g., `-am` = `-a` + `-m`)
  - [ ] Detect positional arguments vs named arguments
  - [ ] Extract command context (working directory, etc.)
- [ ] Update both CommandHistory and ArgumentGraph:
  - [ ] Add entry to CommandHistory
  - [ ] Update ArgumentGraph with new argument patterns
  - [ ] Increment usage counts
  - [ ] Update co-occurrence data
- [ ] Handle both success and error events:
  - [ ] Success: reinforce pattern (increase weight)
  - [ ] Error: potentially flag invalid combinations (future)
- [ ] Performance: must be fast (<5ms), runs after every command

**11.5: Generic Prediction Engine** ✅
- [ ] Create `GenericPredictor.cs` in PSCue.Module
- [ ] Implement generic suggestion logic:
  ```csharp
  public class GenericPredictor
  {
      public List<Suggestion> GetSuggestions(
          string commandLine,
          CommandHistory history,
          ArgumentGraph knowledge,
          ContextAnalyzer context)
      {
          // 1. Parse current command line
          // 2. Get learned arguments for this command
          // 3. Score by frequency + recency
          // 4. Apply context boost (recent patterns)
          // 5. Filter already-typed arguments
          // 6. Return top N suggestions
      }
  }
  ```
- [ ] Scoring algorithm:
  - [ ] Base score = usage frequency
  - [ ] Recency boost = used recently → higher score
  - [ ] Context boost = fits current pattern → higher score
  - [ ] Co-occurrence boost = commonly used with already-typed flags
- [ ] Normalization: convert scores to 0.0-1.0 range

**11.6: Hybrid Predictor (Generic + Known Commands)**
- [ ] Update `CommandCompleterPredictor.cs` to use hybrid approach:
  ```
  GetSuggestion(commandLine):
      1. Check if command is explicitly supported (git, gh, etc.)
         → If yes, get known completions from CommandCompleter
      2. Get generic suggestions from GenericPredictor
      3. Merge both sources:
         - For known commands: blend known + learned
         - For unknown commands: use only learned
      4. Re-score combined suggestions
      5. Return top suggestions
  ```
- [ ] Merging strategy:
  - [ ] Known completions get base score from CompletionCache
  - [ ] Learned data can boost scores further
  - [ ] New arguments from learning appear alongside known ones
  - [ ] Deduplicate (same argument from both sources)
- [ ] Configuration option to disable generic learning (default: enabled)

#### Configuration & Persistence

**11.7: Configuration** ✅
- [ ] Add settings to module configuration:
  - [ ] `EnableGenericLearning` (bool, default: true)
  - [ ] `HistorySize` (int, default: 100)
  - [ ] `MaxKnownCommands` (int, default: 500)
  - [ ] `MaxArgumentsPerCommand` (int, default: 100)
  - [ ] `ScoreDecayDays` (int, default: 30) - how fast old patterns fade
- [ ] Load configuration from profile or environment variable
- [ ] Apply limits to prevent unbounded memory growth

**11.8: Cross-Session Persistence (Optional)**
- [ ] Save learned data to disk on module unload
- [ ] Load learned data on module initialization
- [ ] File format: JSON or SQLite
- [ ] Location: `~/.local/share/PSCue/learned-data.json`
- [ ] Merge strategy: combine disk data + in-memory learning
- [ ] Periodic saves (every N commands or N minutes)
- [ ] Handle corruption gracefully (invalid file → start fresh)
- **Note**: This is optional for Phase 11, can be deferred to Phase 12

#### Privacy & Control

**11.9: Privacy Considerations** ✅
- [ ] Add opt-out mechanism for sensitive commands
  - [ ] Check environment variable: `PSCUE_IGNORE_PATTERNS`
  - [ ] Support glob patterns: `aws *, *secret*, *password*`
  - [ ] Don't log matching commands to history
- [ ] Add command to clear learned data: `Clear-PSCueLearning`
  - [ ] Clears CommandHistory
  - [ ] Clears ArgumentGraph
  - [ ] Optionally deletes persisted file
- [ ] Add command to export learned data: `Export-PSCueLearning -Path file.json`
  - [ ] For backup or migration
- [ ] Add command to import learned data: `Import-PSCueLearning -Path file.json`
- [ ] Documentation on what data is collected and stored

#### Testing & Validation

**11.10: Testing** ✅
- [ ] Unit tests for ArgumentGraph:
  - [ ] Test add/update argument patterns
  - [ ] Test frequency tracking
  - [ ] Test co-occurrence detection
  - [ ] Test scoring algorithm
- [ ] Unit tests for GenericPredictor:
  - [ ] Test suggestion generation
  - [ ] Test scoring (frequency + recency + context)
  - [ ] Test filtering (don't suggest already-typed args)
- [ ] Integration tests for hybrid predictor:
  - [ ] Test known command + generic learning blend
  - [ ] Test unknown command (generic only)
  - [ ] Test suggestion quality improves over time
- [ ] Create test script: `test-scripts/test-generic-learning.ps1`
  - [ ] Simulate command execution sequence
  - [ ] Verify learning occurs
  - [ ] Verify suggestions improve
  - [ ] Test privacy controls (ignore patterns)

**11.11: Real-World Validation**
- [ ] Add telemetry/metrics (optional, opt-in):
  - [ ] Track suggestion acceptance rate
  - [ ] Track which commands benefit most from generic learning
  - [ ] Track performance impact (memory, CPU)
- [ ] Create feedback mechanism for users
- [ ] Monitor for edge cases and bugs

#### Performance Optimization

**11.12: Performance Targets**
- [ ] Learning overhead (FeedbackProvider): <5ms per command
- [ ] Prediction overhead (GenericPredictor): <10ms per request
- [ ] Memory footprint: <50MB for typical usage (100 commands, 500 arguments each)
- [ ] Startup overhead: <50ms to load persisted data (if implemented)

**11.13: Optimization Strategies**
- [ ] Use efficient data structures:
  - [ ] Dictionary for O(1) command lookup
  - [ ] Trie for prefix matching (if needed)
  - [ ] Ring buffer for fixed-size history
- [ ] Lazy loading: don't load all data upfront
- [ ] Background processing: heavy work off critical path
- [ ] Caching: memoize expensive computations
- [ ] Limits: cap sizes to prevent unbounded growth

#### Documentation

**11.14: Documentation Updates**
- [ ] Update CLAUDE.md:
  - [ ] Add Phase 11 to implementation plan
  - [ ] Document generic learning architecture
  - [ ] Update data flow diagrams
- [ ] Update README.md:
  - [ ] Explain generic learning feature
  - [ ] Show examples (learning from unknown commands)
  - [ ] Document privacy controls
  - [ ] Show before/after (with vs without learning)
- [ ] Add new document: `LEARNING.md`:
  - [ ] Explain how generic learning works
  - [ ] Data collected and stored
  - [ ] Privacy and control
  - [ ] Performance characteristics
  - [ ] Troubleshooting
- [ ] Add comments to new classes explaining algorithms

#### Success Criteria
- [ ] GenericPredictor provides suggestions for ANY command (even unsupported ones)
- [ ] Suggestions improve over time as user runs commands
- [ ] Context-aware suggestions (based on recent history)
- [ ] Known commands get blend of explicit + learned completions
- [ ] Privacy controls work (ignore patterns, clear learning)
- [ ] Performance targets met (<5ms learning, <10ms prediction)
- [ ] Memory usage stays bounded (<50MB typical)
- [ ] Documentation complete and clear
- [ ] Tests validate core functionality

#### Implementation Order
1. **11.1-11.2**: Data structures (CommandHistory, ArgumentGraph) - foundation
2. **11.3**: Context analyzer - enables context-aware suggestions
3. **11.4**: Enhance FeedbackProvider - start learning from all commands
4. **11.5**: Generic prediction engine - generate suggestions from learned data
5. **11.6**: Hybrid predictor - blend known + learned
6. **11.7**: Configuration - allow customization
7. **11.9**: Privacy controls - respect user privacy
8. **11.10-11.11**: Testing & validation - ensure quality
9. **11.12-11.13**: Performance optimization - meet targets
10. **11.14**: Documentation - explain feature to users
11. **11.8**: Cross-session persistence (optional, can defer) - save/load learned data

#### Future Enhancements (Phase 12+)
- [ ] ML-based prediction (beyond simple frequency/recency)
- [ ] Detect command errors and suggest fixes
- [ ] Learn command sequences (workflows)
- [ ] Semantic understanding of arguments (file paths, URLs, etc.)
- [ ] Multi-user learning (aggregate patterns across users, opt-in)
- [ ] Cloud sync (sync learned data across machines, opt-in)

---

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

### Phase 13: Future Enhancements
- [ ] Add ML-based prediction support
- [ ] Copy AI model scripts to `ai/` directory
- [ ] Create Scoop manifest
- [ ] Publish to PowerShell Gallery
- [ ] Add Homebrew formula (macOS/Linux)
- [ ] Export/import learned data commands (Export-PSCueLearning, Import-PSCueLearning)
- [ ] Cloud sync (sync learned data across machines, opt-in)
- [ ] Advanced learning: command sequences, workflow detection
- [ ] Semantic argument understanding (detect file paths, URLs, etc.)

---

## Naming Conventions

### Executables
- `pscue-completer` / `pscue-completer.exe` (ArgumentCompleter native executable)

### Assemblies
- `PSCue.Module.dll` (CommandPredictor module)
- `PSCue.Shared.dll` (optional shared library, future)

### Namespaces
- `PSCue.ArgumentCompleter.*`
- `PSCue.Module.*`
- `PSCue.Shared.*` (future)

### Module Name
- `PSCue` (PowerShell module name)

---

## Key Technical Decisions

### Why separate ArgumentCompleter and CommandPredictor?

1. **Different compilation requirements**:
   - ArgumentCompleter: NativeAOT for fast startup (CLI tool)
   - CommandPredictor: Managed DLL for PowerShell SDK integration

2. **Different lifetimes**:
   - ArgumentCompleter: Launched per-completion (short-lived process)
   - CommandPredictor: Loaded once with module (long-lived)

3. **Clear separation of concerns**:
   - ArgumentCompleter: Handles `Register-ArgumentCompleter` (Tab completion)
   - CommandPredictor: Handles `ICommandPredictor` (inline suggestions)

### ArgumentCompleter-Predictor Communication (API Architecture)

Since the Predictor is long-lived and ArgumentCompleter is short-lived, we can leverage inter-process communication to share state and optimize performance.

**Architecture**:
```
┌─────────────────────────────────────────────────────────┐
│  PowerShell Session                                     │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  PSCue.Module.dll (Long-lived)               │
│  ┌───────────────────────────────────────────────────┐ │
│  │ - ICommandPredictor (inline suggestions)          │ │
│  │ - IFeedbackProvider (learns from execution)       │ │
│  │ - CompletionCache (with usage stats)              │ │
│  │ - IPC Server (Named Pipes)                        │◄┼─┐
│  │ - State Manager                                   │ │ │
│  │ - Git/Scoop/etc. parsers                          │ │ │
│  └───────────────────────────────────────────────────┘ │ │
│         ▲                                               │ │
│         │ Feedback Loop (update cache after execution) │ │
│         │                                               │ │
└─────────┼───────────────────────────────────────────────┘ │
          │                                                 │
          │ User executes command                           │
          │                                                 │
          │         ┌───────────────────────────────────────┘
          │         │ IPC Request (get completions)
          │         │
┌─────────┴─────────▼─────────────────┐
│  Tab completion request              │
├──────────────────────────────────────┤
│                                      │
│  pscue-completer.exe (Short-lived)  │
│  ┌──────────────────────────────┐   │
│  │ 1. Try connect to Predictor  │   │
│  │ 2. If available, use IPC API │   │
│  │    (gets learned suggestions)│   │
│  │ 3. Else, run local logic     │   │
│  └──────────────────────────────┘   │
│                                      │
└──────────────────────────────────────┘
```

**Benefits**:
1. **State Persistence**: CommandPredictor maintains caches for git branches, scoop packages, etc.
2. **Performance**: Avoid redundant work across multiple completion requests
3. **Consistency**: Both Tab completion and inline predictions use the same data
4. **Learning Loop**: IFeedbackProvider updates cache based on actual command usage
5. **Resource Efficiency**: Single git/scoop query shared across invocations

**Learning Flow Example**:
```
User types "git checkout"
  → ICommandPredictor suggests branches (from cache)
  → ArgumentCompleter provides Tab completions (via IPC, gets same data)

User executes "git checkout -b feature-x"
  → IFeedbackProvider observes: user created a branch with -b flag
  → Cache updated: Increase priority of "-b" for "git checkout"
  → Track: User prefers "feature-*" naming pattern

Next time user types "git checkout"
  → ICommandPredictor suggests "-b" higher in list
  → ArgumentCompleter shows "-b" earlier in Tab completions
  → Both benefit from learned behavior
```

**Implementation Options**:
1. **Named Pipes** (recommended): Cross-platform, efficient, secure
   - Use session-specific pipe name (e.g., `PSCue-{PID}`)
   - Fast serialization (JSON or MessagePack)
2. **HTTP localhost**: Simple, but higher overhead
3. **Unix Domain Sockets**: More efficient on macOS/Linux

**Fallback Strategy**:
- ArgumentCompleter works standalone if CommandPredictor isn't loaded
- Check IPC availability with timeout (<10ms)
- Graceful degradation to local completion logic

**Protocol Design**:
```json
{
  "command": "git",
  "args": ["checkout"],
  "wordToComplete": "ma",
  "requestType": "branches"
}
```

Response:
```json
{
  "completions": [
    {"text": "main", "description": "Default branch"},
    {"text": "master", "description": "Old default branch"}
  ],
  "cached": true
}
```

**Security**:
- Bind to localhost/named pipe only
- Validate session/process ownership
- No authentication needed (same user, same session)

**Performance Target**:
- IPC round-trip: <5ms
- Total completion time: <50ms (including ArgumentCompleter startup)

### Project References

- Predictor can reference ArgumentCompleter to reuse completion logic
- ArgumentCompleter is self-contained (no dependencies except .NET)

### Code Migration Strategy

- **Copy, don't link**: Independent codebase for PSCue
- Reference original projects in README/documentation
- Clean break allows for PSCue-specific enhancements
- Original projects remain independent

---

## Testing Strategy

### ArgumentCompleter Tests
- Unit tests for completion logic
- Tests for each supported command
- Tests for dynamic completions (git branches, scoop packages, etc.)

### Predictor Tests
- Unit tests for prediction logic
- Integration tests with ArgumentCompleter
- Mock PSReadLine scenarios

### Integration Tests
- Full module loading test
- End-to-end completion scenarios
- Cross-platform compatibility tests

---

## Platform Support

### Tier 1 (Full Support)
- Windows x64
- macOS x64 (Intel)
- macOS arm64 (Apple Silicon)
- Linux x64

### Tier 2 (Possible Future)
- Linux arm64
- Windows arm64

---

## Performance Considerations

### ArgumentCompleter (Native)
- NativeAOT for instant startup (<10ms)
- Zero-copy string operations with `ReadOnlySpan<char>`
- Minimal allocations
- Fast process invocation from PowerShell

### Predictor (Managed)
- Loaded once, stays in memory
- Can use more sophisticated logic (ML models, etc.)
- Async predictions supported

---

## Success Criteria

### Phase 1-7.5 (Completed) ✅
- [x] Plan documented
- [x] Solution builds successfully on all platforms (Windows, macOS, Linux)
- [x] All tests pass on CI
- [x] Module installs correctly (local and remote)
- [x] Tab completion works for all supported commands
- [x] ArgumentCompleter and Predictor code copied and adapted
- [x] GitHub Actions CI/CD pipeline working
- [x] Pre-built binaries in GitHub releases
- [x] README is comprehensive
- [x] Installation instructions are clear
- [x] CLI testing tool implemented
- [ ] Inline predictions work with PSReadLine (needs manual testing)

### Phase 8 (IPC Communication Layer) ✅
- [x] Named Pipe IPC communication layer implemented
- [x] ArgumentCompleter can call Predictor via IPC
- [x] CompletionCache with usage tracking
- [x] JSON source generation for NativeAOT
- [x] Graceful fallback when IPC unavailable
- [x] Performance targets met (<5ms connection timeout, <50ms response)
- [x] All tests pass, build clean with 0 warnings
- [x] IpcClient refactored to use proper async/await with CancellationToken-based timeouts

### Phase 9 (Learning System) ✅
- [x] IFeedbackProvider implementation complete
- [x] Learning system updates cache based on successful command execution
- [x] Cache scores updated based on usage via `IncrementUsage()`
- [x] Test script verifies provider registration
- [x] Graceful degradation on PowerShell 7.2-7.3
- [x] Documentation updated

### Phase 10 (Future Enhancements)
- [ ] Published to PowerShell Gallery
- [ ] Available via Scoop
- [ ] ML-based predictions implemented
- [ ] Cross-session learning (persistent cache)

---

## Questions & Decisions

### Open Questions
1. Should we maintain a PSCue.Shared project from the start, or add it later if needed?
   - **Decision**: Add later only if significant shared code emerges

2. Should the CLI testing tool be included in the initial release?
   - **Decision**: Yes, useful for development and debugging

3. Should we support older PowerShell versions (5.1)?
   - **Decision**: No, require PowerShell 7.2+ (Core only)
   - **Note**: IFeedbackProvider requires 7.4+, but module will work with degraded functionality on 7.2-7.3

4. What about backward compatibility with old module names?
   - **Decision**: No backward compatibility, clean break

5. Should IFeedbackProvider be in the initial release or Phase 9?
   - **Decision**: Phase 9 (after IPC layer is working), but architecture planned from start

### Resolved Decisions
- ✅ Use NativeAOT for ArgumentCompleter (fast startup)
- ✅ Use managed DLL for Predictor (PowerShell SDK integration)
- ✅ Unified module name: PSCue
- ✅ Copy code strategy (not git submodules/subtrees)
- ✅ Installation location: ~/.local/pwsh-modules/PSCue/
- ✅ Executable name: pscue-completer[.exe]
- ✅ IPC Architecture: ArgumentCompleter calls into CommandPredictor via Named Pipes
- ✅ CommandPredictor hosts completion cache and state for performance optimization
- ✅ ArgumentCompleter has fallback to local logic if CommandPredictor unavailable
- ✅ IFeedbackProvider integration: Creates learning loop for smarter completions
- ✅ Cache tracks usage patterns and updates priority scores based on actual command execution

---

## Resources

### Source Projects
- ArgumentCompleter: `D:/source/lucaspimentel/pwsh-argument-completer`
- Predictor: `D:/source/lucaspimentel/pwsh-command-predictor`

### Documentation References
- [PowerShell Predictor API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor)
- [PowerShell Feedback Provider API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider)
- [Register-ArgumentCompleter](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/register-argumentcompleter)
- [NativeAOT Deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [PowerShell Module Manifests](https://learn.microsoft.com/powershell/scripting/developer/module/how-to-write-a-powershell-module-manifest)

---

## Notes

- This plan is a living document and will be updated as implementation progresses
- Check off items as they are completed
- Add new items as they are discovered
- Update decisions as they are made
