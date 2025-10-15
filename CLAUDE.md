# PSCue - PowerShell Completion and Prediction Module

## Project Overview

PSCue is a unified PowerShell module that provides intelligent command-line completion and prediction for PowerShell Core (7.2+).

**Key Components**:
1. **PSCue.ArgumentCompleter** - NativeAOT executable for fast Tab completion via `Register-ArgumentCompleter`
2. **PSCue.Predictor** - Managed DLL for inline suggestions via `ICommandPredictor` interface
3. **IPC Communication Layer** - Named Pipe-based API for state sharing between components

## Architecture Philosophy

### Two-Component Design

**ArgumentCompleter (pscue-completer.exe)**:
- Short-lived process (launches on each Tab press)
- NativeAOT compilation for <10ms startup time
- Can run standalone OR call into Predictor via IPC
- Falls back to local logic if Predictor unavailable

**Predictor (PSCue.Predictor.dll)**:
- Long-lived (loaded with PowerShell module)
- Hosts Named Pipe server for serving completions
- Maintains cached state (git branches, scoop packages, etc.)
- Implements ICommandPredictor for inline suggestions
- Future: IFeedbackProvider for learning from user behavior

### IPC Architecture

The Predictor hosts a Named Pipe server that the ArgumentCompleter can connect to:

```
ArgumentCompleter (short-lived) → IPC Client → Named Pipe → IPC Server ← Predictor (long-lived)
                                                                              ↓
                                                                     CompletionCache
```

**Benefits**:
- Avoid redundant git/scoop queries across multiple completions
- Share learned behavior between Tab completion and inline suggestions
- Performance: Warm cache serves completions instantly
- Consistency: Single source of truth for completion data

**Protocol**: JSON-based request/response over Named Pipes
- Request: `{command, args, wordToComplete, requestType}`
- Response: `{completions: [{text, description}], cached: bool}`

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
│   ├── IpcClient.cs            # Connect to Predictor
│   └── Completions/            # Local completion logic (fallback)
├── PSCue.Predictor/            # Managed DLL
│   ├── IpcServer.cs            # Named Pipe server
│   ├── CompletionCache.cs      # Cache management
│   └── CommandCompleterPredictor.cs
└── PSCue.Shared/               # Shared protocol/models
    ├── IpcProtocol.cs          # Request/response definitions
    └── CompletionModels.cs     # Completion data structures
```

### Namespaces

- `PSCue.ArgumentCompleter.*` - ArgumentCompleter code
- `PSCue.Predictor.*` - Predictor code
- `PSCue.Shared.*` - Shared types and protocol

### Building

```bash
# ArgumentCompleter (NativeAOT, per platform)
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Predictor (managed DLL)
dotnet build src/PSCue.Predictor/ -c Release

# Quick compile check (for dd-trace-dotnet habit compatibility)
dotnet build src/PSCue.Predictor/ -c Release -f net9.0
```

### Testing

```bash
# Unit tests
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Predictor.Tests/

# CLI testing tool
dotnet run --project src/PSCue.Cli/ -- "git checkout ma"
```

## Installation

**Local (development)**:
```powershell
./scripts/install-local.ps1
```

**Remote (end users)**:
```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

Installs to: `~/.local/pwsh-modules/PSCue/`

## Key Technical Decisions

1. **NativeAOT for ArgumentCompleter**: Fast startup critical for Tab completion responsiveness
2. **Managed DLL for Predictor**: Needs PowerShell SDK, doesn't need fast startup
3. **Named Pipes for IPC**: Cross-platform, secure, efficient (<5ms round-trip target)
4. **PowerShell Core only**: No PowerShell 5.1 support, requires 7.2+
5. **No git submodules**: Clean copy from source projects allows independent evolution
6. **Session-specific pipe names**: Use `PSCue-{PID}` to avoid conflicts between PowerShell sessions

## Performance Targets

- ArgumentCompleter startup: <10ms
- IPC round-trip: <5ms
- Total Tab completion time: <50ms
- Predictor cache hit: <1ms

## Platform Support

**Tier 1** (full CI/CD support):
- Windows x64
- macOS x64 (Intel)
- macOS arm64 (Apple Silicon)
- Linux x64

## Implementation Status

See TODO.md for detailed implementation plan and progress tracking.

Current phase: **Phase 1** (Project Structure Setup)

## Common Tasks for AI Assistants

### When working on ArgumentCompleter:
- Use `ReadOnlySpan<char>` for string operations (NativeAOT optimization)
- Minimize allocations
- Keep startup time <10ms
- Always implement fallback logic if IPC unavailable

### When working on Predictor:
- Can use async/await (long-lived process)
- Implement proper cache invalidation
- Handle concurrent IPC requests safely
- Clean up resources on module unload

### When working on IPC layer:
- Keep protocol simple and fast
- Use JSON for human-readability (can optimize to MessagePack later)
- Always set timeouts on client connections
- Validate session ownership for security

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
- [Register-ArgumentCompleter](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/register-argumentcompleter)
- [NativeAOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
