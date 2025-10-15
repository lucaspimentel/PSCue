# PSCue Module - Implementation Plan

## Overview

PSCue is a unified PowerShell module that combines:
1. **Argument Completer** (from pwsh-argument-completer) - Native argument completion using `Register-ArgumentCompleter`
2. **Command Predictor** (from pwsh-command-predictor) - ICommandPredictor for inline suggestions
3. **Future Features** - IFeedbackProvider, ML-based completions, etc.

---

## Directory Structure

```
PSCue/
├── src/
│   ├── PSCue.ArgumentCompleter/         # Native executable (C#, NativeAOT)
│   │   ├── PSCue.ArgumentCompleter.csproj
│   │   ├── Program.cs
│   │   ├── CommandCompleter.cs
│   │   ├── IpcClient.cs                 # Named Pipe client for communicating with Predictor
│   │   ├── Completions/                 # Copied from pwsh-argument-completer
│   │   ├── KnownCompletions/            # Copied from pwsh-argument-completer
│   │   └── ...
│   │
│   ├── PSCue.Predictor/                 # DLL for ICommandPredictor (C#)
│   │   ├── PSCue.Predictor.csproj
│   │   ├── Init.cs                      # Module initializer
│   │   ├── CommandCompleterPredictor.cs # Copied from pwsh-command-predictor
│   │   ├── IpcServer.cs                 # Named Pipe server for serving completions
│   │   ├── CompletionCache.cs           # Cache for git branches, scoop packages, etc.
│   │   ├── FeedbackProvider.cs          # Future: IFeedbackProvider
│   │   └── ...
│   │
│   ├── PSCue.Cli/                       # CLI testing tool (optional)
│   │   ├── PSCue.Cli.csproj
│   │   └── Program.cs
│   │
│   └── PSCue.Shared/                    # Shared code/utilities (if needed)
│       ├── PSCue.Shared.csproj
│       ├── IpcProtocol.cs               # Shared IPC protocol definitions
│       └── CompletionModels.cs          # Shared completion data models
│
├── module/
│   ├── PSCue.psd1                       # Module manifest
│   └── PSCue.psm1                       # Module script
│
├── test/
│   ├── PSCue.ArgumentCompleter.Tests/
│   │   └── PSCue.ArgumentCompleter.Tests.csproj
│   └── PSCue.Predictor.Tests/
│       └── PSCue.Predictor.Tests.csproj
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
- RequiredAssemblies: `PSCue.Predictor.dll`
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

2. **Build PSCue.Predictor** (Managed DLL):
   - Build as Release for net9.0
   - Output: `PSCue.Predictor.dll`
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
3. Build Predictor as managed DLL
   - `dotnet build src/PSCue.Predictor/PSCue.Predictor.csproj -c Release`
4. Create installation directory: `~/.local/pwsh-modules/PSCue/`
5. Copy files to installation directory:
   - Native executable: `pscue-completer[.exe]`
   - Predictor DLL: `PSCue.Predictor.dll`
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
   - Predictor DLL: `PSCue.Predictor.dll`
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
     - Build Predictor DLL:
       - `dotnet build src/PSCue.Predictor/PSCue.Predictor.csproj -c Release -o dist/common`
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
PSCue.Predictor.dll        # Predictor assembly
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

### Phase 1: Project Structure Setup
- [x] Document plan in TODO.md
- [ ] Create directory structure
- [ ] Create .gitignore
- [ ] Create PSCue.sln solution file
- [ ] Create empty project files:
  - [ ] src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj
  - [ ] src/PSCue.Predictor/PSCue.Predictor.csproj
  - [ ] src/PSCue.Cli/PSCue.Cli.csproj (optional)
  - [ ] test/PSCue.ArgumentCompleter.Tests/PSCue.ArgumentCompleter.Tests.csproj
  - [ ] test/PSCue.Predictor.Tests/PSCue.Predictor.Tests.csproj

### Phase 2: Copy ArgumentCompleter Code
- [ ] Copy source files from `../pwsh-argument-completer/src/` to `src/PSCue.ArgumentCompleter/`
  - [ ] Program.cs
  - [ ] CommandCompleter.cs
  - [ ] AssemblyInfo.cs
  - [ ] Logger.cs
  - [ ] Helpers.cs
  - [ ] Completions/ directory (all files)
  - [ ] KnownCompletions/ directory (all files)
- [ ] Update namespaces from `PowerShellArgumentCompleter` to `PSCue.ArgumentCompleter`
- [ ] Update AssemblyName to `pscue-completer` in .csproj
- [ ] Copy test files from `../pwsh-argument-completer/test/` to `test/PSCue.ArgumentCompleter.Tests/`
- [ ] Update test namespaces and project references
- [ ] Verify build: `dotnet build src/PSCue.ArgumentCompleter/`
- [ ] Verify tests: `dotnet test test/PSCue.ArgumentCompleter.Tests/`

### Phase 3: Copy Predictor Code
- [ ] Copy source files from `../pwsh-command-predictor/src/PowerShellPredictor/` to `src/PSCue.Predictor/`
  - [ ] Init.cs
  - [ ] CommandCompleterPredictor.cs
  - [ ] AssemblyInfo.cs
  - [ ] (Optional) KnownCommandsPredictor.cs
  - [ ] (Optional) SamplePredictor.cs
  - [ ] (Optional) AiPredictor.cs
- [ ] Update namespaces from `PowerShellPredictor` to `PSCue.Predictor`
- [ ] Update AssemblyName to `PSCue.Predictor` in .csproj
- [ ] Update project reference to point to PSCue.ArgumentCompleter
- [ ] Copy test files from `../pwsh-command-predictor/test/` to `test/PSCue.Predictor.Tests/`
- [ ] Update test namespaces and project references
- [ ] Verify build: `dotnet build src/PSCue.Predictor/`
- [ ] Verify tests: `dotnet test test/PSCue.Predictor.Tests/`

### Phase 4: Create Module Files
- [ ] Create `module/PSCue.psd1` (module manifest)
  - [ ] Set RootModule to `PSCue.psm1`
  - [ ] Set ModuleVersion to `1.0.0`
  - [ ] Set Author, Description, etc.
  - [ ] Set PowerShellVersion requirement (7.2+)
  - [ ] Set CompatiblePSEditions to 'Core'
- [ ] Create `module/PSCue.psm1` (module script)
  - [ ] Find pscue-completer executable
  - [ ] Register argument completers for supported commands
  - [ ] Load PSCue.Predictor.dll (auto-registers predictors)
- [ ] Test module loading manually

### Phase 5: Create Installation Scripts
- [ ] Create `scripts/` directory
- [ ] Create `scripts/install-local.ps1` (build from source)
  - [ ] Detect platform (Windows/macOS/Linux, x64/arm64)
  - [ ] Build ArgumentCompleter with NativeAOT
  - [ ] Build Predictor
  - [ ] Create installation directory
  - [ ] Copy all necessary files
  - [ ] Display instructions
- [ ] Create `scripts/install-remote.ps1` (download from GitHub)
  - [ ] Accept optional $version variable from caller
  - [ ] Detect platform and map to release asset name
  - [ ] Query GitHub API for latest release or use specific version
  - [ ] Download and extract release archive
  - [ ] Install to ~/.local/pwsh-modules/PSCue/
  - [ ] Clean up temp files
  - [ ] Display instructions
- [ ] Test local installation script on current platform
- [ ] Verify module works after installation

### Phase 6: GitHub Actions & CI/CD
- [ ] Create `.github/workflows/` directory
- [ ] Create `.github/workflows/ci.yml` (CI workflow)
  - [ ] Configure triggers (push to main, PRs)
  - [ ] Setup build matrix (windows, macos, linux)
  - [ ] Add build and test steps
  - [ ] Add code formatting check (dotnet format)
  - [ ] Upload test results as artifacts
- [ ] Create `.github/workflows/release.yml` (Release workflow)
  - [ ] Configure triggers (tags matching v*, workflow_dispatch)
  - [ ] Build native binaries for all platforms (win-x64, osx-x64, osx-arm64, linux-x64)
  - [ ] Create platform-specific archives (zip for Windows, tar.gz for others)
  - [ ] Generate checksums (SHA256)
  - [ ] Create GitHub release with all artifacts
  - [ ] Auto-generate release notes
- [ ] Test CI workflow with a test commit/PR
- [ ] Document release process in TODO.md or CONTRIBUTING.md

### Phase 7: Documentation
- [ ] Create comprehensive README.md
  - [ ] Overview of PSCue
  - [ ] Features (ArgumentCompleter + Predictor)
  - [ ] Installation instructions
  - [ ] Usage examples
  - [ ] List of supported commands
  - [ ] Architecture overview
  - [ ] Contributing guidelines
- [ ] Copy LICENSE from existing projects (MIT)
- [ ] Create CONTRIBUTING.md (optional)
- [ ] Document differences from original projects

### Phase 7: Optional - CLI Testing Tool
- [ ] Create `src/PSCue.Cli/` for testing
- [ ] Copy Program.cs from pwsh-command-predictor CLI
- [ ] Update to use PSCue namespaces
- [ ] Test: `dotnet run --project src/PSCue.Cli/ -- "git che"`

### Phase 8: IPC Communication Layer
- [ ] Design IPC protocol schema (request/response format)
- [ ] Implement Named Pipe server in PSCue.Predictor
  - [ ] Start server on module initialization
  - [ ] Use session-specific pipe name (PSCue-{PID})
  - [ ] Handle concurrent requests
  - [ ] Implement completion cache
  - [ ] Add request handlers for git, scoop, etc.
- [ ] Implement Named Pipe client in PSCue.ArgumentCompleter
  - [ ] Connection with timeout (<10ms)
  - [ ] Fallback to local logic if unavailable
  - [ ] JSON serialization/deserialization
- [ ] Test IPC communication
  - [ ] Unit tests for protocol serialization
  - [ ] Integration tests for Predictor server
  - [ ] Performance tests (target <5ms round-trip)
- [ ] Add caching strategy
  - [ ] Cache invalidation (time-based, event-based)
  - [ ] Memory management

### Phase 9: Future Enhancements (Not in initial release)
- [ ] Implement IFeedbackProvider
- [ ] Add ML-based prediction support
- [ ] Copy AI model scripts to `ai/` directory
- [ ] Create Scoop manifest
- [ ] Publish to PowerShell Gallery
- [ ] Create GitHub releases with pre-built binaries
- [ ] Add Homebrew formula (macOS/Linux)

---

## Naming Conventions

### Executables
- `pscue-completer` / `pscue-completer.exe` (ArgumentCompleter native executable)

### Assemblies
- `PSCue.Predictor.dll` (Predictor module)
- `PSCue.Shared.dll` (optional shared library, future)

### Namespaces
- `PSCue.ArgumentCompleter.*`
- `PSCue.Predictor.*`
- `PSCue.Shared.*` (future)

### Module Name
- `PSCue` (PowerShell module name)

---

## Key Technical Decisions

### Why separate ArgumentCompleter and Predictor?

1. **Different compilation requirements**:
   - ArgumentCompleter: NativeAOT for fast startup (CLI tool)
   - Predictor: Managed DLL for PowerShell SDK integration

2. **Different lifetimes**:
   - ArgumentCompleter: Launched per-completion (short-lived process)
   - Predictor: Loaded once with module (long-lived)

3. **Clear separation of concerns**:
   - ArgumentCompleter: Handles `Register-ArgumentCompleter` (Tab completion)
   - Predictor: Handles `ICommandPredictor` (inline suggestions)

### ArgumentCompleter-Predictor Communication (API Architecture)

Since the Predictor is long-lived and ArgumentCompleter is short-lived, we can leverage inter-process communication to share state and optimize performance.

**Architecture**:
```
┌─────────────────────────────────────┐
│  PowerShell Session                 │
├─────────────────────────────────────┤
│                                     │
│  PSCue.Predictor.dll (Long-lived)  │
│  ┌──────────────────────────────┐  │
│  │ - ICommandPredictor          │  │
│  │ - CompletionCache            │  │
│  │ - IPC Server (Named Pipes)   │◄─┼──┐
│  │ - State Manager              │  │  │
│  │ - Git/Scoop/etc. parsers     │  │  │
│  └──────────────────────────────┘  │  │
│                                     │  │
└─────────────────────────────────────┘  │
                                         │
         ┌───────────────────────────────┘
         │ IPC Request
         │
┌────────▼─────────────────────────────┐
│  Tab completion request              │
├──────────────────────────────────────┤
│                                      │
│  pscue-completer.exe (Short-lived)  │
│  ┌──────────────────────────────┐   │
│  │ 1. Try connect to Predictor  │   │
│  │ 2. If available, use IPC API │   │
│  │ 3. Else, run local logic     │   │
│  └──────────────────────────────┘   │
│                                      │
└──────────────────────────────────────┘
```

**Benefits**:
1. **State Persistence**: Predictor maintains caches for git branches, scoop packages, etc.
2. **Performance**: Avoid redundant work across multiple completion requests
3. **Consistency**: Both Tab completion and inline predictions use the same data
4. **Learning**: IFeedbackProvider can improve suggestions over time
5. **Resource Efficiency**: Single git/scoop query shared across invocations

**Implementation Options**:
1. **Named Pipes** (recommended): Cross-platform, efficient, secure
   - Use session-specific pipe name (e.g., `PSCue-{PID}`)
   - Fast serialization (JSON or MessagePack)
2. **HTTP localhost**: Simple, but higher overhead
3. **Unix Domain Sockets**: More efficient on macOS/Linux

**Fallback Strategy**:
- ArgumentCompleter works standalone if Predictor isn't loaded
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

### Phase 1-5 (MVP)
- [x] Plan documented
- [ ] Solution builds successfully
- [ ] All tests pass
- [ ] Module installs correctly
- [ ] Tab completion works for all supported commands
- [ ] Inline predictions work with PSReadLine
- [ ] Works on Windows (minimum)

### Phase 6 (Documentation)
- [ ] README is comprehensive
- [ ] Installation instructions are clear
- [ ] Examples work as documented

### Phase 7-8 (Future)
- [ ] Published to PowerShell Gallery
- [ ] Available via Scoop
- [ ] Pre-built binaries in GitHub releases
- [ ] ML-based predictions implemented

---

## Questions & Decisions

### Open Questions
1. Should we maintain a PSCue.Shared project from the start, or add it later if needed?
   - **Decision**: Add later only if significant shared code emerges

2. Should the CLI testing tool be included in the initial release?
   - **Decision**: Yes, useful for development and debugging

3. Should we support older PowerShell versions (5.1)?
   - **Decision**: No, require PowerShell 7.2+ (Core only)

4. What about backward compatibility with old module names?
   - **Decision**: No backward compatibility, clean break

### Resolved Decisions
- ✅ Use NativeAOT for ArgumentCompleter (fast startup)
- ✅ Use managed DLL for Predictor (PowerShell SDK integration)
- ✅ Unified module name: PSCue
- ✅ Copy code strategy (not git submodules/subtrees)
- ✅ Installation location: ~/.local/pwsh-modules/PSCue/
- ✅ Executable name: pscue-completer[.exe]
- ✅ IPC Architecture: ArgumentCompleter calls into Predictor via Named Pipes
- ✅ Predictor hosts completion cache and state for performance optimization
- ✅ ArgumentCompleter has fallback to local logic if Predictor unavailable

---

## Resources

### Source Projects
- ArgumentCompleter: `D:/source/lucaspimentel/pwsh-argument-completer`
- Predictor: `D:/source/lucaspimentel/pwsh-command-predictor`

### Documentation References
- [PowerShell Predictor API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor)
- [Register-ArgumentCompleter](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/register-argumentcompleter)
- [NativeAOT Deployment](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [PowerShell Module Manifests](https://learn.microsoft.com/powershell/scripting/developer/module/how-to-write-a-powershell-module-manifest)

---

## Notes

- This plan is a living document and will be updated as implementation progresses
- Check off items as they are completed
- Add new items as they are discovered
- Update decisions as they are made
