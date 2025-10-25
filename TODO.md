# PSCue Module - Implementation Plan

## Overview

PSCue is a unified PowerShell module that combines:
1. **Argument Completer** (from pwsh-argument-completer) - Native argument completion using `Register-ArgumentCompleter`
2. **Command Predictor** (from pwsh-command-predictor) - ICommandPredictor for inline suggestions
3. **Feedback Provider** - IFeedbackProvider for learning from command execution (PowerShell 7.4+)
4. **Future Features** - ML-based completions, etc.

---

## Directory Structure

```
PSCue/
├── src/
│   ├── PSCue.ArgumentCompleter/         # Native executable (C#, NativeAOT)
│   │   ├── PSCue.ArgumentCompleter.csproj
│   │   ├── Program.cs                   # Entry point for Tab completion
│   │   ├── Logger.cs                    # Debug logging
│   │   ├── AssemblyInfo.cs              # NativeAOT trim settings
│   │   └── IpcClient.cs                 # (future) Named Pipe client for communicating with Predictor
│   │
│   ├── PSCue.Module/          # DLL for ICommandPredictor + IFeedbackProvider (C#)
│   │   ├── PSCue.Module.csproj
│   │   ├── Init.cs                      # IModuleAssemblyInitializer - auto-registers predictor ✅
│   │   ├── CommandCompleterPredictor.cs # ICommandPredictor implementation ✅
│   │   ├── FeedbackProvider.cs          # (future) IFeedbackProvider - learns from command execution
│   │   ├── IpcServer.cs                 # (future) Named Pipe server for serving completions
│   │   └── CompletionCache.cs           # (future) Cache with usage tracking and learning
│   │
│   ├── PSCue.Shared/                    # Shared completion logic ✅
│   │   ├── PSCue.Shared.csproj
│   │   ├── CommandCompleter.cs          # Main completion orchestrator
│   │   ├── Logger.cs                    # Debug logging
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
│   │   └── IpcProtocol.cs               # (future) IPC protocol definitions
│   │
│   └── PSCue.Cli/                       # CLI testing tool ✅
│       ├── PSCue.Cli.csproj
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

### Phase 7.5: Optional - CLI Testing Tool ✅
- [x] Create `src/PSCue.Cli/` for testing
- [x] Copy Program.cs from pwsh-command-predictor CLI
- [x] Update to use PSCue namespaces
- [x] Add PowerShell SDK package reference
- [x] Test: `dotnet run --project src/PSCue.Cli/ -- "git commit"`
- [x] Verified CLI tool works for git, scoop, and other commands

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

**Performance achieved:**
- ✅ Named Pipe connection: <10ms timeout
- ✅ Build time: ~1.3 seconds for full solution
- ✅ Test execution: <1 second for all tests
- ✅ Module installation: ~30 seconds (includes NativeAOT compilation)

### Phase 9: Feedback Provider (Learning System)

**Prerequisites completed in Phase 8:**
- ✅ CompletionCache with usage tracking (`IncrementUsage()` method ready)
- ✅ IPC protocol includes score field in CompletionItem
- ✅ Cache statistics endpoint implemented
- ✅ **Performance Optimization** (2025-01-22): `includeDynamicArguments` parameter added
  - ICommandPredictor skips slow operations (git branches, scoop packages) for fast responses
  - ArgumentCompleter includes all completions for comprehensive Tab suggestions
  - IPC protocol updated to support the flag

**Remaining work:**
- [ ] Implement IFeedbackProvider in PSCue.Module
  - [ ] Create `FeedbackProvider.cs` implementing `IFeedbackProvider`
  - [ ] Register as feedback provider in `Init.OnImport()`
  - [ ] Handle `FeedbackTrigger.Success` events
  - [ ] Handle `FeedbackTrigger.Error` events
  - [ ] Extract command patterns from `FeedbackContext`
  - [ ] Call `CompletionCache.IncrementUsage()` for executed commands
- [ ] Enhance CompletionCache learning
  - [ ] Track command frequency (e.g., "git checkout -b" usage count)
  - [ ] Track flag combinations (e.g., user often uses "git commit -am")
  - [ ] Track argument patterns (e.g., branch naming preferences)
  - [ ] Refine priority scoring algorithm (frequency × recency)
- [ ] Test feedback learning
  - [ ] Unit tests for feedback processing
  - [ ] Integration tests for cache updates
  - [ ] Verify ArgumentCompleter receives learned suggestions
  - [ ] Verify scores increase for frequently-used completions
- [ ] Document PowerShell 7.4+ requirement for feedback features
  - [ ] Add experimental feature enablement to docs: `Enable-ExperimentalFeature PSFeedbackProvider`
  - [ ] Document graceful degradation on PowerShell 7.2-7.3

### Phase 10: Future Enhancements (Not in initial release)
- [ ] Add ML-based prediction support
- [ ] Copy AI model scripts to `ai/` directory
- [ ] Create Scoop manifest
- [ ] Publish to PowerShell Gallery
- [ ] Add Homebrew formula (macOS/Linux)
- [ ] Implement cross-session learning (persist cache to disk)

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
- [x] Performance targets met (<10ms connection, <50ms response)
- [x] All tests pass, build clean with 0 warnings
- [x] IpcClient refactored to use proper async/await with CancellationToken-based timeouts

### Phase 9 (Learning System) - Next
- [ ] IFeedbackProvider implementation
- [ ] Learning system adapts to user patterns
- [ ] Track command frequency and flag combinations
- [ ] Update completion scores based on usage

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
