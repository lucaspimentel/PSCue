# PSCue.ArgumentCompleter

## Description

NativeAOT-compiled executable (`pscue-completer.exe`) that provides fast Tab completion for PowerShell via `Register-ArgumentCompleter`. Optimized for <10ms startup time through ahead-of-time compilation and aggressive trimming.

## Key Features

- **Fast Startup**: <10ms cold start via NativeAOT compilation
- **Local Completion**: All completions computed locally, including dynamic arguments (git branches, scoop packages)
- **Smart Suggestions**: Context-aware completion for git, gh, az, scoop, winget, wt, chezmoi, and more
- **Directory Navigation**: Enhanced `cd`/`Set-Location` completions with learned directory suggestions

## Build Configuration

- **NativeAOT**: Ahead-of-time compiled for maximum performance
- **Optimizations**: Speed-optimized with trimming enabled
- **Size Optimizations**: ~2MB executable with aggressive feature trimming
- **Target**: Executable (OutputType: Exe)

## Dependencies

### NuGet Packages
- None (uses .NET BCL only)

### Internal Dependencies
- `PSCue.Shared` - Shared completion logic and command definitions

## Dependents

This project is referenced by:
- `PSCue.ArgumentCompleter.Tests` - Unit tests for ArgumentCompleter
- `PSCue.Benchmarks` - Performance benchmarks

## Performance Targets

- Startup time: <10ms
- Total Tab completion: <50ms
- Memory footprint: <20MB
