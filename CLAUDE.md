# PSCue - PowerShell Completion and Prediction Module

## Project Overview

PSCue is a unified PowerShell module that provides intelligent command-line completion and prediction for PowerShell Core (7.2+).

**Key Components**:
1. **PSCue.ArgumentCompleter** - NativeAOT executable for fast Tab completion via `Register-ArgumentCompleter`
2. **PSCue.Predictor** - Managed DLL implementing `ICommandPredictor` (inline suggestions) and `IFeedbackProvider` (learning)
3. **IPC Communication Layer** - Named Pipe-based API for state sharing and learning feedback loop
4. **CompletionCache** - Smart cache with usage tracking and priority scoring

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
- Request: `{command, args, wordToComplete, requestType}`
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
│   ├── IpcClient.cs            # Connect to Predictor via Named Pipe
│   └── Completions/            # Local completion logic (fallback)
├── PSCue.Predictor/            # Managed DLL
│   ├── Init.cs                 # Module initialization
│   ├── CommandCompleterPredictor.cs  # ICommandPredictor implementation
│   ├── FeedbackProvider.cs     # IFeedbackProvider - learns from execution
│   ├── IpcServer.cs            # Named Pipe server
│   └── CompletionCache.cs      # Cache with usage tracking and scoring
└── PSCue.Shared/               # Shared protocol/models
    ├── IpcProtocol.cs          # Request/response definitions
    └── CompletionModels.cs     # Completion data structures (includes scores)
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
4. **PowerShell Core only**: No PowerShell 5.1 support, requires 7.2+ minimum
   - **IFeedbackProvider requires 7.4+**: Module works on 7.2-7.3 but without learning features
5. **No git submodules**: Clean copy from source projects allows independent evolution
6. **Session-specific pipe names**: Use `PSCue-{PID}` to avoid conflicts between PowerShell sessions
7. **Learning via IFeedbackProvider**: Creates true personalized completion system
   - Tracks command frequency, flag combinations, argument patterns
   - Updates cache priority scores based on actual usage
   - Both Tab completion and inline suggestions benefit from learned behavior

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
- Handle IPC connection failures gracefully

### When working on Predictor:
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
