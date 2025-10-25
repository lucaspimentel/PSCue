# PSCue - PowerShell Completion and Prediction Module

## Project Overview

PSCue is a unified PowerShell module that provides intelligent command-line completion and prediction for PowerShell Core (7.2+).

**Key Components**:
1. **PSCue.ArgumentCompleter** - NativeAOT executable for fast Tab completion via `Register-ArgumentCompleter`
2. **PSCue.Module** - Managed DLL implementing `ICommandPredictor` (inline suggestions) and `IFeedbackProvider` (learning)
3. **IPC Communication Layer** - Named Pipe-based API for state sharing and learning feedback loop ✅ **IMPLEMENTED**
4. **CompletionCache** - Smart cache with usage tracking and priority scoring ✅ **IMPLEMENTED**

## Architecture Philosophy

### Two-Component Design

**ArgumentCompleter (pscue-completer.exe)**:
- Short-lived process (launches on each Tab press)
- NativeAOT compilation for <10ms startup time
- Can run standalone OR call into Predictor via IPC
- Falls back to local logic if Predictor unavailable

**CommandPredictor (PSCue.Module.dll)**:
- Long-lived (loaded with PowerShell module)
- Hosts Named Pipe server for serving completions
- Maintains cached state (git branches, scoop packages, etc.)
- Implements `ICommandPredictor` for inline suggestions
- Implements `IFeedbackProvider` for learning from command execution (PowerShell 7.4+)
- Updates cache with usage patterns to prioritize frequently-used completions

### IPC Architecture with Learning Loop

The Predictor hosts a Named Pipe server that the ArgumentCompleter can connect to, creating a learning feedback loop:

```
User types command
    ↓
ArgumentCompleter (short-lived) → IPC Request → Named Pipe → Predictor (long-lived)
                                                                    ↓
                                                             CompletionCache
                                                          (with usage stats)
                                                                    ↓
ArgumentCompleter ← IPC Response (learned suggestions) ← Named Pipe
    ↓
User executes command
    ↓
IFeedbackProvider observes execution
    ↓
Cache updated with usage patterns
    ↓
Next completion request gets smarter suggestions
```

**Benefits**:
- **State Persistence**: Cache git branches, scoop packages across Tab presses
- **Learning Loop**: IFeedbackProvider updates cache based on actual command usage
- **Consistency**: Tab completion and inline suggestions use same learned data
- **Performance**: Warm cache serves completions <1ms, avoids redundant queries
- **Personalization**: Learns user preferences (e.g., "git commit -am" vs "git commit -m")

**Protocol**: JSON-based request/response over Named Pipes
- Request: `{command, args, wordToComplete, requestType, includeDynamicArguments}`
  - `includeDynamicArguments`: Controls whether to include slow operations (git branches, scoop packages)
- Response: `{completions: [{text, description, score}], cached: bool}`
  - `score`: Usage-based priority (higher = more frequently used by this user)

## Supported Commands

- **git** - branches, tags, remotes, files, subcommands
- **code** - workspace suggestions, extensions
- **az** - Azure CLI completions
- **azd** - Azure Developer CLI
- **func** - Azure Functions Core Tools
- **gh** - GitHub CLI
- **scoop** (Windows) - apps, buckets
- **winget** (Windows) - packages
- **chezmoi** - dotfile management
- **tre** - tree alternative
- **lsd** - ls alternative
- **dust** - du alternative

## Source Projects

PSCue consolidates two existing projects:
- **pwsh-argument-completer**: D:/source/lucaspimentel/pwsh-argument-completer
- **pwsh-command-predictor**: D:/source/lucaspimentel/pwsh-command-predictor

Code is copied (not linked) to allow PSCue-specific enhancements.

## Development Guidelines

### Project Structure

```
src/
├── PSCue.ArgumentCompleter/    # NativeAOT exe
│   ├── Program.cs              # Entry point for Tab completion
│   ├── IpcClient.cs            # Named Pipe client ✅
│   ├── Logger.cs               # Debug logging
│   └── AssemblyInfo.cs         # NativeAOT trim settings
├── PSCue.Module/     # Managed DLL
│   ├── Init.cs                 # IModuleAssemblyInitializer - auto-registers predictor & starts IPC server ✅
│   ├── CommandCompleterPredictor.cs  # ICommandPredictor implementation
│   ├── IpcServer.cs            # Named Pipe server ✅
│   ├── CompletionCache.cs      # Cache with usage tracking and scoring ✅
│   └── FeedbackProvider.cs     # IFeedbackProvider - learns from execution (Phase 9)
└── PSCue.Shared/               # Shared completion logic
    ├── CommandCompleter.cs     # Main completion orchestrator
    ├── IpcProtocol.cs          # IPC request/response definitions ✅
    ├── IpcJsonContext.cs       # JSON source generation for NativeAOT ✅
    ├── Logger.cs               # Debug logging
    ├── Helpers.cs              # Utility functions
    ├── Completions/            # Completion framework
    │   ├── ICompletion.cs      # Base completion interface
    │   ├── Command.cs          # Command node
    │   ├── CommandParameter.cs # Parameter/flag node
    │   ├── StaticArgument.cs   # Static argument values
    │   └── DynamicArgument.cs  # Dynamic argument provider
    ├── KnownCompletions/       # Command-specific completions
    │   ├── GitCommand.cs       # git completions
    │   ├── GhCommand.cs        # GitHub CLI
    │   ├── ScoopCommand.cs     # Scoop package manager
    │   ├── WingetCommand.cs    # Windows Package Manager
    │   ├── VsCodeCommand.cs    # VS Code CLI
    │   └── Azure/              # Azure tools
    │       ├── AzCommand.cs    # Azure CLI
    │       ├── AzdCommand.cs   # Azure Developer CLI
    │       └── FuncCommand.cs  # Azure Functions Core Tools
```

### Namespaces

- `PSCue.ArgumentCompleter.*` - ArgumentCompleter code
- `PSCue.Module.*` - CommandPredictor code
- `PSCue.Shared.*` - Shared types and protocol

### Building

```bash
# ArgumentCompleter (NativeAOT, per platform)
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# CommandPredictor (managed DLL)
dotnet build src/PSCue.Module/ -c Release

# Quick compile check (for dd-trace-dotnet habit compatibility)
dotnet build src/PSCue.Module/ -c Release -f net9.0
```

### Testing

```bash
# Unit tests
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/

# CLI testing tool (tests CommandPredictor GetSuggestion)
dotnet run --project src/PSCue.Cli/ -- "git checkout ma"

# Test predictor registration and GetSuggestion
pwsh -NoProfile -File test-inline-predictions.ps1

# Test manual IModuleAssemblyInitializer.OnImport()
pwsh -NoProfile -File test-manual-init.ps1

# Test predictor subsystem registration
pwsh -NoProfile -File test-predictor.ps1
```

**Testing inline predictions interactively**:
```powershell
# Load the module
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1

# Enable inline predictions
Set-PSReadLineOption -PredictionSource HistoryAndPlugin

# Type a command and wait - suggestions appear in gray text
git checkout ma<wait for suggestion>
```

## Installation

**Local (development)**:
```powershell
# PowerShell
./scripts/install-local.ps1

# Or from Windows Command Prompt
scripts\install-local.cmd
```

**Remote (end users)**:
```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

Installs to: `~/.local/pwsh-modules/PSCue/`

## Key Technical Decisions

1. **NativeAOT for ArgumentCompleter**: Fast startup critical for Tab completion responsiveness
2. **Managed DLL for Predictor**: Needs PowerShell SDK, doesn't need fast startup
3. **Shared completion logic in PSCue.Shared**: Avoids NativeAOT assembly reference issues
   - ArgumentCompleter (NativeAOT exe) cannot be referenced by CommandPredictor at runtime
   - Solution: Move all completion logic to PSCue.Shared (managed DLL)
   - Both projects reference PSCue.Shared for consistent behavior
4. **NestedModules in manifest**: Required for IModuleAssemblyInitializer to trigger
   - Module.dll must be listed in PSCue.psd1 NestedModules
   - Loading via Import-Module in .psm1 does NOT trigger IModuleAssemblyInitializer
5. **Named Pipes for IPC** ✅ **IMPLEMENTED**: Cross-platform, secure, efficient (<5ms round-trip target)
   - Uses `System.IO.Pipes.NamedPipeServerStream` and `NamedPipeClientStream`
   - Works on Windows (Named Pipes) and Linux/macOS (Unix Domain Sockets) transparently
   - JSON-based protocol with source generation for NativeAOT compatibility
6. **PowerShell Core only**: No PowerShell 5.1 support, requires 7.2+ minimum
   - **IFeedbackProvider requires 7.4+**: Module works on 7.2-7.3 but without learning features
7. **No git submodules**: Clean copy from source projects allows independent evolution
8. **Session-specific pipe names** ✅ **IMPLEMENTED**: `PSCue-{PID}` avoids conflicts between PowerShell sessions
9. **JSON Source Generation** ✅ **IMPLEMENTED**: `IpcJsonContext` for NativeAOT-compatible serialization
   - Eliminates trimming warnings for ArgumentCompleter
   - Better performance than reflection-based JSON serialization
10. **Learning via IFeedbackProvider** (Phase 9): Creates true personalized completion system
    - Tracks command frequency, flag combinations, argument patterns
    - Updates cache priority scores based on actual usage
    - Both Tab completion and inline suggestions benefit from learned behavior
11. **Dynamic Argument Performance Optimization** ✅ **IMPLEMENTED**: Separate performance profiles for Tab vs inline predictions
    - `includeDynamicArguments` parameter controls expensive operations (git branches, scoop packages, etc.)
    - **ICommandPredictor**: `includeDynamicArguments: false` for fast inline suggestions (<10ms response)
    - **ArgumentCompleter**: `includeDynamicArguments: true` for complete Tab completions (user expects delay)
    - **IPC Protocol**: Supports flag to let client control behavior
    - Result: Predictor responds instantly with flags/subcommands, Tab completion still gets full lists

## Performance Targets

- ArgumentCompleter startup: <10ms
- IPC round-trip: <5ms
- Total Tab completion time: <50ms
- Predictor cache hit: <1ms

## Platform Support

**Tier 1** (full CI/CD support):
- Windows x64
- macOS arm64 (Apple Silicon)
- Linux x64

**Not Supported**:
- macOS x64 (Intel) - skipped in favor of Apple Silicon

## Implementation Status

See TODO.md for detailed implementation plan and progress tracking.

**Current Status**: Learning system (Phase 9) partially implemented! FeedbackProvider observes command execution and updates completion scores.

**Completed phases:**
- ✅ Phase 1: Project Structure Setup
- ✅ Phase 2: Copy ArgumentCompleter Code
- ✅ Phase 3: Copy CommandPredictor Code
- ✅ Phase 4: Create Module Files
- ✅ Phase 5: Create Installation Scripts
- ✅ Phase 6: GitHub Actions & CI/CD
- ✅ Phase 7: Documentation
- ✅ Phase 7.5: CLI Testing Tool
- ✅ Phase 8: IPC Communication Layer
  - Named Pipe server in CommandPredictor
  - Named Pipe client in ArgumentCompleter
  - CompletionCache with usage tracking
  - JSON source generation for NativeAOT
  - Graceful fallback when IPC unavailable
- 🔄 Phase 9: Learning System (IFeedbackProvider)
  - ✅ Implemented CommandCompleterFeedbackProvider
  - ✅ Registered in Init with PowerShell 7.4+ detection
  - ✅ Observes successful command execution
  - ✅ Updates cache scores via CompletionCache.IncrementUsage()
  - ✅ Graceful degradation on PowerShell 7.2-7.3
  - ⏳ Needs real-world testing and refinement

**Future enhancements**:
- Enhanced learning algorithms (frequency × recency scoring)
- Track flag combinations and argument patterns
- Cross-session persistence (save learned data to disk)
- ML-based predictions

**Known Issues Fixed:**
- **Phase 5:**
  - Fixed `$IsWindows` read-only variable conflict in install-local.ps1
  - Fixed PSCue.psm1 completer invocation to pass 3 required arguments (wordToComplete, line, cursorPosition)
  - Updated output parsing from JSON format to pipe-delimited format (completionText|tooltip)
- **Post-Phase 7.5:**
  - Fixed install-local.ps1 warning about missing PSCue.ArgumentCompleter.dll (ArgumentCompleter is a NativeAOT exe, not a DLL)
  - Fixed scoop command completions: added installed package suggestions for uninstall, cleanup, hold, unhold, home, info, prefix, and reset commands
- **CommandPredictor Registration (2025-01-22):**
  - Fixed IModuleAssemblyInitializer not being called: Added Module.dll to NestedModules in PSCue.psd1
  - Fixed NativeAOT assembly reference error: Moved completion logic from ArgumentCompleter to PSCue.Shared
  - CommandPredictor now successfully registers and provides inline suggestions
  - Test files added: test-predictor.ps1, test-manual-init.ps1, test-inline-predictions.ps1

**Phase 6 Highlights:**
- Created CI workflow for multi-platform builds and tests
- Created Release workflow for automated binary releases
- Skipped macOS x64 (Intel) support - focusing on Apple Silicon (osx-arm64)
- Added minimal README.md with installation instructions
- Fixed platform-specific tests using Xunit.SkippableFact
- CI now passing on all platforms (Windows, macOS, Linux)

## Troubleshooting Guide

### Platform-Specific Tests in CI

When tests fail on Linux/macOS but pass on Windows:

1. **Identify Windows-only tools**: Commands like `winget` and `scoop` only exist on Windows
2. **Use SkippableFact instead of Fact**:
   ```csharp
   using System.Runtime.InteropServices;
   using Xunit;

   [SkippableFact]
   public void Winget_Install()
   {
       Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

       // Test code...
   }
   ```
3. **Add Xunit.SkippableFact package**: Add to test project csproj:
   ```xml
   <PackageReference Include="Xunit.SkippableFact" Version="1.4.13" />
   ```
4. **Pattern for bulk updates**: When updating many tests at once, create a PowerShell script that:
   - Finds `[Fact]` followed by specific test name patterns
   - Replaces with `[SkippableFact]`
   - Adds `Skip.IfNot()` check as first line in method body

**Common Windows-only commands to watch for:**
- `winget` - Windows Package Manager
- `scoop` - Windows package manager
- PowerShell 5.1 specific features

### Testing Installation Scripts

When testing PowerShell installation scripts:
1. **Always test with `-Force` flag first** to avoid interactive prompts during automated testing
2. **Check for read-only automatic variables**: `$IsWindows`, `$IsMacOS`, `$IsLinux` cannot be reassigned
   - Solution: Use different variable names like `$IsWindowsPlatform`
3. **Verify file paths after installation**: Use `Get-ChildItem` to confirm all expected files were copied
4. **Test module loading with `-Verbose`**: Helps identify issues with module initialization

### Testing ArgumentCompleter Integration

When the completer returns no output:
1. **Check the argument count**: The Program.cs expects exactly 3 arguments (line 38-41 in src/PSCue.ArgumentCompleter/Program.cs)
   - `wordToComplete`, `commandAst`, `cursorPosition`
2. **Test the executable directly** with correct arguments:
   ```powershell
   & 'path/to/pscue-completer.exe' 'che' 'git che' 7
   ```
3. **Check output format**: Program.cs outputs pipe-delimited format `completionText|tooltip`, not JSON
4. **Verify the module script matches the executable's API**:
   - PSCue.psm1 must pass all 3 arguments to the completer
   - Output parsing must match the actual format (pipe-delimited, not JSON)

### Testing Tab Completions

Use `TabExpansion2` to test completions programmatically:
```powershell
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1
$result = TabExpansion2 'git che' 7
$result.CompletionMatches | Select-Object CompletionText, ToolTip
```

**Common issues**:
- Cursor position must be valid (≥0 and ≤ command length)
- For testing, use `$commandLine.Length` as cursor position for end-of-line completions

### Debugging Module Loading

If the CommandPredictor doesn't register:
1. Check module imports with: `Get-Module PSCue | Format-List NestedModules`
2. Verify DLL is loaded: The output should show `PSCue.Module` in NestedModules
3. Check for assembly loading errors in verbose output: `Import-Module -Verbose`
4. Verify IModuleAssemblyInitializer.OnImport() is being called (should register subsystem automatically)

## Common Tasks for AI Assistants

### When working on ArgumentCompleter:
- Use `ReadOnlySpan<char>` for string operations (NativeAOT optimization) - but note they can't cross async boundaries
- Minimize allocations
- Keep startup time <10ms
- Always implement fallback logic if IPC unavailable
- Handle IPC connection failures gracefully
- Uses async/await for IPC communication (proper CancellationToken-based timeouts)

### When working on CommandPredictor:
- Can use async/await (long-lived process)
- Implement proper cache invalidation (time-based, event-based)
- Handle concurrent IPC requests safely
- Clean up resources on module unload
- Register both `ICommandPredictor` and `IFeedbackProvider` on initialization

### When working on CompletionCache:
- Track usage statistics: command frequency, flag combinations, patterns
- Implement priority scoring algorithm (e.g., frequency * recency)
- Update scores atomically for thread-safety
- Expire old entries to manage memory
- Provide cache statistics endpoint for debugging

### When working on IFeedbackProvider:
- Handle both `FeedbackTrigger.Success` and `FeedbackTrigger.Error`
- Extract command patterns from `FeedbackContext`
- Update cache scores based on observed usage
- Be performant - runs after every command execution
- Test on PowerShell 7.4+ (requires experimental feature)

### When working on IPC layer:
- Keep protocol simple and fast
- Use JSON for human-readability (can optimize to MessagePack later)
- Always set timeouts on client connections (<10ms for ArgumentCompleter)
- Validate session ownership for security
- Include usage scores in completion responses

### When copying code from source projects:
- Update namespaces from `PowerShellArgumentCompleter`/`PowerShellPredictor` to `PSCue.*`
- Update assembly names in .csproj files
- Update test project references
- Keep git history references in comments for attribution

## Git Workflow

- Main branch: `main`
- Commit style: Follow repository's existing style (see `git log`)
- GitHub username: `lucaspimentel`

## Helpful Context

The developer (Lucas) works on:
- Datadog APM .NET tracer (dd-trace-dotnet)
- Azure Functions serverless instrumentation
- Prefers pwsh over powershell, uses `-NoProfile` flag
- Uses Windows with `/` paths in bash commands

## Quick Reference

**Important files**:
- `TODO.md` - Complete implementation plan with phases and checklists
- `module/PSCue.psd1` - Module manifest
- `module/PSCue.psm1` - Module initialization script

**Documentation**:
- [ICommandPredictor API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor)
- [IFeedbackProvider API](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider)
- [Register-ArgumentCompleter](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/register-argumentcompleter)
- [NativeAOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

**Key Concepts**:
- **ICommandPredictor**: Provides suggestions BEFORE command execution (as you type)
- **IFeedbackProvider**: Learns from command execution AFTER it completes (success or error)
- **Learning Loop**: IFeedbackProvider updates cache → ICommandPredictor/ArgumentCompleter use updated cache → Better suggestions next time
