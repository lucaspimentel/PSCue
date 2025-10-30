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
- Register completers for: git, gh, gt, code, az, azd, func, chezmoi, tre, lsd, dust, scoop (Windows), winget (Windows), wt (Windows)

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

## Current Work

### Phase 14: Future Enhancements
- [ ] Add ML-based prediction support
- [ ] Copy AI model scripts to `ai/` directory
- [ ] Create Scoop manifest
- [ ] Publish to PowerShell Gallery
- [ ] Add Homebrew formula (macOS/Linux)
- [ ] Cloud sync (sync learned data across machines, opt-in)
- [ ] Advanced learning: command sequences, workflow detection
- [ ] Semantic argument understanding (detect file paths, URLs, etc.)

---

### Phase 16: PowerShell Module Functions (Cache & Learning Management) + Remove IPC

**Goal**: Replace PSCue.Debug CLI tool with native PowerShell functions for better UX, discoverability, and direct in-process access. **Remove IPC layer entirely** since it's only used by PSCue.Debug.

**Rationale**:
- Better PowerShell integration (tab completion, pipeline support, Get-Help)
- No IPC overhead for cache/learning operations (direct in-process access)
- More discoverable via `Get-Command -Module PSCue`
- Simpler installation (one less binary to ship)
- PowerShell-native patterns (objects, not JSON strings)
- **IPC is obsolete**: ArgumentCompleter doesn't use it (Phase 9), CommandPredictor doesn't use it, only PSCue.Debug uses it
- **Simpler architecture**: Remove ~500 lines of IPC code (server, protocol, tests)
- **Faster module loading**: No IPC server startup overhead

**Current State**:
- PSCue.Debug project provides: stats, cache, clear, ping, query-local, query-ipc
- All operations go through IPC (slower, more complex)
- IpcServer runs in Module, serves requests from PSCue.Debug only
- ArgumentCompleter removed IPC usage in Phase 9 (computes locally now)
- CommandPredictor never used IPC (calls CommandCompleter directly)
- IPC exists ONLY for PSCue.Debug - no other consumers!
- Requires separate binary installation
- Not discoverable via standard PowerShell commands

**Target State**:
- Native PowerShell functions exported by PSCue module
- Direct access to CompletionCache, PersistenceManager, learning system
- Rich object output (with optional -AsJson for scripting)
- Standard PowerShell parameters (-WhatIf, -Confirm, -Verbose)
- Built-in help documentation
- **IPC completely removed** (IpcServer, IpcProtocol, IpcJsonContext, all tests)
- Simpler, faster, less code to maintain

#### Proposed Commands

**Cache Management**:
- `Get-PSCueCache [-Filter <string>] [-AsJson]` - View cached completions
- `Clear-PSCueCache [-WhatIf] [-Confirm]` - Clear completion cache
- `Get-PSCueCacheStats [-AsJson]` - Cache statistics (entries, hits, age)

**Learning System**:
- `Get-PSCueLearning [-Command <string>] [-AsJson]` - View learned data
- `Clear-PSCueLearning [-WhatIf] [-Confirm]` - Clear learned data
- `Export-PSCueLearning -Path <string>` - Export learned data to file
- `Import-PSCueLearning -Path <string> [-Merge]` - Import learned data from file
- `Save-PSCueLearning` - Force save learned data to disk

**Debugging/Testing**:
- `Test-PSCueCompletion -Input <string>` - Test completion generation locally
- `Get-PSCueModuleInfo` - Module version, status, configuration

#### Implementation Plan

##### Phase 16.1: Core Infrastructure ✅ COMPLETE
- [x] Add public API methods to existing classes for PowerShell access:
  - [x] `CompletionCache.GetAllEntries()` → already existed
  - [x] `CompletionCache.GetStatistics()` → already existed
  - [x] `ArgumentGraph.GetAllCommands()` → made public (was internal)
  - [x] `ArgumentGraph.GetCommandKnowledge(string command)` → already existed
  - [x] `PersistenceManager.Export(string path)` → added
  - [x] `PersistenceManager.Import(string path, bool merge)` → added
  - [x] `CommandHistory.AddEntry()` with custom timestamp → added
  - [x] `PersistenceManager.DatabasePath` property → added
- [x] Created PSCueModule static class (split from Init)
  - [x] `public static CompletionCache Cache { get; internal set; }`
  - [x] `public static ArgumentGraph KnowledgeGraph { get; internal set; }`
  - [x] `public static CommandHistory CommandHistory { get; internal set; }`
  - [x] `public static PersistenceManager Persistence { get; internal set; }`
- [x] Refactored Init → ModuleInitializer + PSCueModule for better separation
- [x] Added string overload to CommandCompleter for PowerShell compatibility
- [x] All tests pass (325 tests)

##### Phase 16.2: Cache Management Functions ✅ COMPLETE
- [x] Created `module/Functions/CacheManagement.ps1` (181 lines)
- [x] `Get-PSCueCache` function - view cached completions
  - [x] Parameters: `-Filter <string>`, `-AsJson`
  - [x] Returns rich objects with Key, CompletionCount, HitCount, Age
  - [x] Pipeline-friendly
- [x] `Clear-PSCueCache` function - clear cache
  - [x] Parameters: `-WhatIf`, `-Confirm`
  - [x] Shows count of removed entries
- [x] `Get-PSCueCacheStats` function - cache statistics
  - [x] Returns TotalEntries, TotalHits, AverageHits, OldestEntry
  - [x] Optional `-AsJson` output
- [x] Added functions to `PSCue.psd1` FunctionsToExport
- [x] Dot-sourced in `PSCue.psm1`
- [x] Comprehensive comment-based help with examples
- [x] Updated install script to copy Functions directory
- [x] Tested and working

##### Phase 16.3: Learning System Functions ✅ COMPLETE
- [x] Created `module/Functions/LearningManagement.ps1` (327 lines)
- [x] `Get-PSCueLearning` function - view learned data
  - [x] Parameters: `-Command <string>`, `-AsJson`
  - [x] Shows all commands or filter by specific command
  - [x] Returns usage counts, scores, top arguments
- [x] `Clear-PSCueLearning` function - clear learned data
  - [x] Parameters: `-WhatIf`, `-Confirm` (ConfirmImpact=High)
  - [x] Clears database and in-memory state
  - [x] Shows counts of cleared data
- [x] `Export-PSCueLearning` function - export to JSON
  - [x] Parameters: `-Path <string>` (mandatory)
  - [x] Creates directory if needed
  - [x] Shows file size
- [x] `Import-PSCueLearning` function - import from JSON
  - [x] Parameters: `-Path <string>` (mandatory), `-Merge` (switch)
  - [x] Replace or merge modes
  - [x] Validates file exists
- [x] `Save-PSCueLearning` function - force immediate save
  - [x] Bypasses auto-save timer
  - [x] Shows database path and size
- [x] Added to PSCue.psd1 and PSCue.psm1
- [x] Comprehensive comment-based help with examples

##### Phase 16.4: Debugging Functions ✅ COMPLETE
- [x] Created `module/Functions/Debugging.ps1` (220 lines)
- [x] `Test-PSCueCompletion` function - test completion generation
  - [x] Parameters: `-InputString <string>` (mandatory), `-IncludeTiming`
  - [x] Shows up to 20 completions with descriptions
  - [x] Returns objects for pipeline use
  - [x] Optional timing information
- [x] `Get-PSCueModuleInfo` function - module diagnostics
  - [x] Module version and path
  - [x] Configuration settings (learning, history size, etc.)
  - [x] Component status (cache, knowledge graph, etc.)
  - [x] Database info (path, size, exists)
  - [x] Cache, learning, and history statistics
  - [x] Optional `-AsJson` output
- [x] Added to PSCue.psd1 and PSCue.psm1
- [x] Comprehensive comment-based help
- [x] Fixed PowerShell compatibility issues (ReadOnlySpan, $Input variable)
- [x] Added string overload to CommandCompleter for PowerShell
- [x] Tested and working

**Total Functions Implemented: 10**
- 3 Cache Management
- 5 Learning Management
- 2 Debugging

##### Phase 16.5: Testing (2-3 hours)
- [ ] Create test script: `test-scripts/test-module-functions.ps1`
  - [ ] Test each function with various parameters
  - [ ] Test pipeline support where applicable
  - [ ] Test `-WhatIf` and `-Confirm` for destructive operations
  - [ ] Test `-AsJson` output is valid JSON
  - [ ] Test error handling (invalid paths, etc.)
- [ ] Add unit tests: `test/PSCue.Module.Tests/ModuleFunctionsTests.cs`
  - [ ] Test public APIs added to classes
  - [ ] Test Export/Import functionality
  - [ ] Test GetAllEntries, GetStatistics, etc.
- [ ] Manual testing:
  - [ ] Load module, run each function
  - [ ] Verify tab completion works on function parameters
  - [ ] Verify Get-Help works for each function
  - [ ] Test in fresh PowerShell session

##### Phase 16.6: Documentation ✅ COMPLETE (1 hour)
- [x] Update CLAUDE.md:
  - [x] Document new PowerShell functions (organized by category)
  - [x] Update "Common Tasks" section with function examples
  - [x] Add cross-reference to DATABASE-FUNCTIONS.md
  - [x] Add Function files to "Key Files & Line References"
  - [x] Note PSCue.Debug is deprecated (will be removed in Phase 16.7)
- [x] Update README.md:
  - [x] Add "Database Management" subsection with detailed examples
  - [x] Update "Learning System Management" section
  - [x] Add cross-reference to DATABASE-FUNCTIONS.md
  - [x] Show comparison workflow (in-memory vs database)
  - [x] Document database locations per platform
  - [x] Add DATABASE-FUNCTIONS.md to Links section
- [x] DATABASE-FUNCTIONS.md already has comprehensive examples:
  - [x] Real-world scenarios for each function
  - [x] Pipeline examples where applicable
  - [x] Use cases and troubleshooting

##### Phase 16.7: Remove IPC Layer + PSCue.Debug (2-3 hours)
- [ ] **Remove PSCue.Debug project**
  - [ ] Delete `src/PSCue.Debug/` directory
  - [ ] Remove from solution file (`PSCue.sln`)
  - [ ] Remove from CI/CD build (`.github/workflows/`)
  - [ ] Delete test scripts that use PSCue.Debug (`test-scripts/test-pscue-debug.ps1`)
- [ ] **Remove IPC infrastructure**
  - [ ] Delete `src/PSCue.Module/IpcServer.cs` (~400 lines)
  - [ ] Delete `src/PSCue.Shared/IpcProtocol.cs` (~150 lines)
  - [ ] Delete `src/PSCue.Shared/IpcJsonContext.cs` (~50 lines)
  - [ ] Remove IpcServer initialization from `Init.cs:OnImport()` (lines 69-78)
  - [ ] Remove IpcServer disposal from `Init.cs:OnRemove()`
  - [ ] Remove IpcServer field from `Init.cs` (line 19)
  - [ ] Remove IpcServer parameter from `FeedbackProvider` constructor (no longer needed)
- [ ] **Remove IPC tests** (all passing, but no longer needed)
  - [ ] Delete `test/PSCue.Module.Tests/IpcServerIntegrationTests.cs` (5 tests)
  - [ ] Delete `test/PSCue.Module.Tests/IpcServerErrorHandlingTests.cs` (10 tests)
  - [ ] Delete `test/PSCue.Module.Tests/IpcServerConcurrencyTests.cs` (7 tests)
  - [ ] Delete `test/PSCue.Module.Tests/IpcServerLifecycleTests.cs` (10 tests)
  - [ ] Delete `test/PSCue.Module.Tests/IpcFilteringTests.cs` (12 tests)
  - [ ] **Total removed**: 44 tests (all IPC-related)
  - [ ] **New test count**: ~252 tests (down from 296)
- [ ] **Remove IPC-related test scripts**
  - [ ] Delete or update `test-scripts/test-ipc.ps1`
  - [ ] Delete or update `test-scripts/test-ipc-simple.ps1`
  - [ ] Delete or update `test-scripts/test-ipc-path.ps1`
  - [ ] Delete or update `test-scripts/test-cache-debug.ps1`
- [ ] **Update documentation**
  - [ ] Remove IPC references from CLAUDE.md
  - [ ] Remove IPC references from README.md
  - [ ] Remove IPC references from TECHNICAL_DETAILS.md
  - [ ] Update architecture diagrams (no more IPC layer)
  - [ ] Note in Phase 16 completion: "IPC removed, module functions replace PSCue.Debug"
- [ ] **Verify build and tests**
  - [ ] `dotnet build` succeeds
  - [ ] `dotnet test` passes (all remaining tests)
  - [ ] Module loads correctly without IPC server
  - [ ] No references to IpcServer, IpcProtocol, or IpcJsonContext remain

#### Success Criteria

1. [ ] All PSCue.Debug functionality available as PowerShell functions
2. [ ] Functions are discoverable via `Get-Command -Module PSCue`
3. [ ] `Get-Help <function>` provides comprehensive documentation
4. [ ] Functions return rich objects (not strings), with optional `-AsJson`
5. [ ] Destructive operations support `-WhatIf` and `-Confirm`
6. [ ] Direct in-process access (no IPC overhead)
7. [ ] Export/Import functions enable backup and migration scenarios
8. [ ] All tests pass (~252 tests after removing IPC tests)
9. [ ] Documentation updated (README, CLAUDE.md, TECHNICAL_DETAILS.md)
10. [ ] **PSCue.Debug completely removed** (project deleted)
11. [ ] **IPC layer completely removed** (IpcServer, IpcProtocol, IpcJsonContext deleted)
12. [ ] **44 IPC tests removed** (no longer needed)
13. [ ] Module loads faster (no IPC server startup)
14. [ ] Simpler architecture (fewer moving parts)

#### Implementation Order

1. **Phase 16.1**: Infrastructure (public APIs, module state) - enables everything else ✅
2. **Phase 16.2**: Cache functions (most commonly used) ✅
3. **Phase 16.3**: Learning functions (high value for power users) ✅
4. **Phase 16.4**: Debug functions (lower priority, but useful) ✅
5. **Phase 16.5**: Testing (validate everything works)
6. **Phase 16.6**: Documentation (help users discover and use)
7. **Phase 16.7**: Remove IPC + PSCue.Debug (massive simplification!)

**Note**: Phases 16.1-16.6 can be done BEFORE removing IPC (module functions coexist with IPC temporarily). Phase 16.7 is the final cleanup once module functions are proven to work.

#### Performance Considerations

- **Direct access** is faster than IPC (no serialization, no pipe overhead)
- **Cache operations**: Should be <5ms (in-memory access)
- **Learning system queries**: Should be <10ms (in-memory data structures)
- **Export/Import**: May take longer (disk I/O, serialization), but acceptable
- **Test completion**: Should match ArgumentCompleter performance (<50ms)
- **Module loading**: Faster without IPC server startup (saves ~10-20ms)

#### Open Questions ✅ RESOLVED

1. **Module state access pattern**: How should PowerShell functions access module internals?
   - ✅ **Decision**: Static properties on `Init` class (simpler, direct C# access)

2. **File format for Export/Import**: JSON or SQLite backup?
   - ✅ **Decision**: JSON (human-readable, easier to edit/merge)

3. **Keep PSCue.Debug?**
   - ✅ **Decision**: Remove it entirely (all functionality in module functions now)
   - ✅ **Decision**: Remove IPC entirely (only used by PSCue.Debug)

4. **Should functions be in separate .ps1 files or all in PSCue.psm1?**
   - ✅ **Decision**: Separate files in `module/Functions/` (better organization)
   - Dot-source them in PSCue.psm1: `. $PSScriptRoot/Functions/CacheManagement.ps1`

#### Benefits Summary

**Removing IPC saves**:
- ~600 lines of code (IpcServer, IpcProtocol, IpcJsonContext)
- 44 test files (~1,500 lines of test code)
- Named pipe overhead (10-20ms module startup)
- Cross-process serialization complexity
- Maintenance burden (fewer moving parts)

**Adding module functions gains**:
- Native PowerShell integration
- Better discoverability
- Pipeline support
- Rich object output
- Standard cmdlet patterns

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

## Completed Work

For details on all completed implementation phases (Phases 1-13, 15), see [COMPLETED.md](COMPLETED.md).

---

## Notes

- This plan is a living document and will be updated as implementation progresses
- Check off items as they are completed
- Add new items as they are discovered
