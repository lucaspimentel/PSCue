# PSCue.ArgumentCompleter

## Description

NativeAOT-compiled executable (`pscue-completer.exe`) that provides fast Tab completion for PowerShell via `Register-ArgumentCompleter`. Optimized for <10ms startup time through ahead-of-time compilation and aggressive trimming.

## Key Features

- **Fast Startup**: NativeAOT compilation removes runtime startup work; the measured cost of a no-op invocation is ~2.8ms on WSL2 and ~87ms on Windows (mostly process creation), see [`benchmark/spawn-cost`](../../benchmark/spawn-cost/README.md)
- **Local Completion**: All completions computed locally, including dynamic arguments (git branches, scoop packages). Dynamic arguments come from child processes, so a Tab press costs one process per lookup; measure changes with the spawn-cost harness
- **Smart Suggestions**: Context-aware completion for git, git-wt, gh, gt, az, azd, func, scoop, winget, wt, code, claude, pi, chezmoi, chafa, rg, fd, tre, lsd, dust, and more
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

Targets are goals, not measurements. `git ` and `scoop update ` were measured in the seconds before the spawn-cost work; run [`benchmark/spawn-cost`](../../benchmark/spawn-cost/README.md) for current numbers.
