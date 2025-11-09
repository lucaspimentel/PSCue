# PSCue.Shared

## Description

Shared completion logic library used by both the ArgumentCompleter and Module projects. Contains the core completion framework including command definitions, parameter structures, and completion logic that must be available to both NativeAOT-compiled executables and managed DLLs.

## Key Components

- **Completion Framework**: `Command`, `CommandParameter`, `CommandArgument` classes with alias support
- **Known Completions**: Command-specific completion logic for git, gh, az, scoop, winget, wt, etc.
- **CommandCompleter**: Main orchestrator for completion logic
- **Shared Utilities**: Common utilities for both ArgumentCompleter and Module

## Dependencies

### NuGet Packages
- `System.Text.Json` (9.0.0) - For JSON serialization

### Internal Dependencies
- None (this is the base shared library)

## Dependents

This project is referenced by:
- `PSCue.ArgumentCompleter` - NativeAOT executable for Tab completion
- `PSCue.Module` - PowerShell module for inline predictions
- `PSCue.ArgumentCompleter.Tests` - Test project
- `PSCue.Module.Tests` - Test project
- `PSCue.Benchmarks` - Performance benchmarks

## Why This Exists

NativeAOT executables cannot be referenced by managed DLLs at runtime due to ahead-of-time compilation constraints. PSCue.Shared solves this by providing a common library that both projects can reference, enabling code reuse without circular dependencies.
