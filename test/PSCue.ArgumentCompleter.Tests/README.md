# PSCue.ArgumentCompleter.Tests

## Description

Unit test project for PSCue.ArgumentCompleter using xUnit. Tests all completion scenarios including command-specific completions, dynamic arguments, and directory navigation.

## Test Coverage

- **Command Completions**: Tests for git, gh, az, scoop, winget, wt, chezmoi, and other commands
- **Dynamic Arguments**: Git branches, scoop packages, and other runtime-generated completions
- **Directory Navigation**: `cd`/`Set-Location` completion behavior
- **Edge Cases**: Empty inputs, invalid commands, partial matches
- **Performance**: Completion speed and memory usage

## Test Statistics

- Total Tests: 142 (as of v0.3.0)
- Skipped Tests: 5 (platform-specific Unix tests)
- Test Framework: xUnit

## Dependencies

### NuGet Packages
- `Microsoft.NET.Test.Sdk` (17.12.0) - Test SDK
- `xunit` (2.9.2) - xUnit test framework
- `xunit.runner.visualstudio` (2.8.2) - Visual Studio test adapter
- `coverlet.collector` (6.0.2) - Code coverage collector
- `Xunit.SkippableFact` (1.4.13) - Platform-specific test skipping

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
