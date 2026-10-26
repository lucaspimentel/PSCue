# PSCue.Module.Tests

## Description

Unit test project for PSCue.Module using xUnit. Tests all module functionality including learning system, prediction engine, workflow learning, persistence, and PowerShell module functions.

## Test Coverage

- **CommandPredictor**: Inline prediction generation and filtering
- **FeedbackProvider**: Automatic learning from command execution
- **ArgumentGraph**: Knowledge graph operations and scoring
- **GenericPredictor**: Single-word and multi-word suggestion generation
- **SequencePredictor**: N-gram based command sequence prediction
- **WorkflowLearner**: Dynamic workflow learning with timing patterns
- **PersistenceManager**: SQLite database save/load operations
- **Concurrency**: Multi-session database access
- **PCD Engine**: Smart directory navigation with fuzzy matching and frecency
- **Module Functions**: PowerShell function behavior and edge cases
- **Integration**: End-to-end workflows

## Test Framework

xUnit v3

## Test Categories

- Completion and Prediction Tests
- Learning System Tests
- Workflow Learning Tests
- Persistence Tests (Unit + Concurrency + Integration)
- PCD Tests (smart directory navigation with fuzzy matching and frecency)
- Module Function Tests
- Edge Cases and Error Handling

## Dependencies

### NuGet Packages
- `Microsoft.NET.Test.Sdk` - Test SDK
- `xunit.v3` - xUnit test framework
- `xunit.runner.visualstudio` - Visual Studio test adapter
- `coverlet.collector` - Code coverage collector
- `Microsoft.PowerShell.SDK` - PowerShell SDK for testing CommandPredictor

See `PSCue.Module.Tests.csproj` for exact pinned versions.

### Internal Dependencies
- `PSCue.Module` - The project being tested
- `PSCue.Shared` - Shared types and logic

## Dependents

None - this is a test project.

## Running Tests

```bash
# Run all tests
dotnet test test/PSCue.Module.Tests/

# Run specific test categories
dotnet test test/PSCue.Module.Tests/ --filter "FullyQualifiedName~Persistence"
dotnet test test/PSCue.Module.Tests/ --filter "FullyQualifiedName~CommandPredictor"
dotnet test test/PSCue.Module.Tests/ --filter "FullyQualifiedName~WorkflowLearner"
dotnet test test/PSCue.Module.Tests/ --filter "FullyQualifiedName~PcdEnhanced"

# Run with coverage
dotnet test test/PSCue.Module.Tests/ --collect:"XPlat Code Coverage"
```
