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

## Test Statistics

- Total Tests: 336 (as of v0.3.0)
- Test Framework: xUnit

## Test Categories

- Completion and Prediction Tests
- Learning System Tests
- Workflow Learning Tests (35+ tests)
- Persistence Tests (Unit + Concurrency + Integration)
- PCD Tests (Phase 17.5 + Enhanced Phase 17.6)
- Module Function Tests
- Edge Cases and Error Handling

## Dependencies

### NuGet Packages
- `Microsoft.NET.Test.Sdk` (17.12.0) - Test SDK
- `xunit` (2.9.2) - xUnit test framework
- `xunit.runner.visualstudio` (2.8.2) - Visual Studio test adapter
- `coverlet.collector` (6.0.2) - Code coverage collector
- `Microsoft.PowerShell.SDK` (7.5.0) - PowerShell SDK for testing CommandPredictor

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
