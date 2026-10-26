# PSCue.ArgumentCompleter.Tests

## Description

Unit test project for PSCue.ArgumentCompleter using xUnit. Tests all completion scenarios including command-specific completions, dynamic arguments, and directory navigation.

## Test Coverage

- **Command Completions**: Tests for git, gh, az, scoop, winget, wt, chezmoi, and other commands
- **Dynamic Arguments**: Git branches, scoop packages, and other runtime-generated completions, evaluated once per request so a completion never launches the same children twice
- **Directory Navigation**: `cd`/`Set-Location` completion behavior
- **Edge Cases**: Empty inputs, invalid commands, partial matches
- **Child Processes**: Executable resolution order, argument quoting, stderr isolation, bounded timeout, and the Windows `.cmd` fallback in `CommandProcess`
- **Performance**: Completion speed and memory usage

## Test Framework

xUnit (with `Xunit.SkippableFact` for platform-specific tests)

## Dependencies

### NuGet Packages
- `Microsoft.NET.Test.Sdk` - Test SDK
- `xunit` - xUnit test framework
- `xunit.runner.visualstudio` - Visual Studio test adapter
- `coverlet.collector` - Code coverage collector
- `Xunit.SkippableFact` - Platform-specific test skipping

See `PSCue.ArgumentCompleter.Tests.csproj` for exact pinned versions.

### Internal Dependencies
- `PSCue.ArgumentCompleter` - The project being tested
- `PSCue.Shared` - Shared types and logic

## Dependents

None - this is a test project.

## Running Tests

```bash
# Run all tests
dotnet test test/PSCue.ArgumentCompleter.Tests/

# Run with coverage
dotnet test test/PSCue.ArgumentCompleter.Tests/ --collect:"XPlat Code Coverage"
```
