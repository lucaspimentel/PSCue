# PSCue.Module

## Description

PowerShell module library (`PSCue.Module.dll`) that provides inline predictions via `ICommandPredictor` and learning capabilities via `IFeedbackProvider` (PowerShell 7.4+). Implements the learning system with cross-session persistence and provides PowerShell functions for managing learned data.

## Key Features

- **Inline Predictions**: Real-time command suggestions as you type
- **Generic Learning**: Learns from ALL commands, not just predefined ones
- **Multi-Word Suggestions**: Shows common argument combinations (e.g., "git checkout master")
- **Workflow Learning**: Predicts next command based on usage patterns and timing
- **Smart Directory Navigation**: `pcd` command with fuzzy matching, frecency scoring, exact match prioritization, and symlink resolution
- **Cross-Session Persistence**: SQLite database stores learned data (8 tables)
- **PowerShell Functions**: 14 functions for learning, database, workflow, and navigation management

## Key Components

- **CommandPredictor**: Implements `ICommandPredictor` for inline suggestions
- **FeedbackProvider**: Implements `IFeedbackProvider` for automatic learning (PS 7.4+)
- **ArgumentGraph**: Knowledge graph tracking command → arguments with frequency/recency scoring
- **GenericPredictor**: Context-aware suggestions with multi-word generation
- **SequencePredictor**: ML-based n-gram prediction for command sequences
- **WorkflowLearner**: Dynamic workflow learning with timing-aware predictions
- **PcdCompletionEngine**: Enhanced directory navigation with fuzzy matching and frecency
- **PersistenceManager**: SQLite-based cross-session persistence
- **PSCue Functions**: PowerShell module functions (Learning, Database, Workflow, Navigation, Debugging)

## Dependencies

### NuGet Packages
- `Microsoft.PowerShell.SDK` (7.5.0) - PowerShell SDK for ICommandPredictor and IFeedbackProvider
- `Microsoft.Data.Sqlite` (9.0.0) - SQLite database for persistence

### Internal Dependencies
- `PSCue.Shared` - Shared completion logic

## Dependents

This project is referenced by:
- `PSCue.Module.Tests` - Unit tests for Module
- `PSCue.Benchmarks` - Performance benchmarks

## PowerShell Module Functions

### Learning Management
- `Get-PSCueLearning` - View learned data
- `Clear-PSCueLearning` - Clear learned data
- `Export-PSCueLearning` - Export to JSON
- `Import-PSCueLearning` - Import from JSON
- `Save-PSCueLearning` - Force save to disk

### Database Management
- `Get-PSCueDatabaseStats` - Database statistics
- `Get-PSCueDatabaseHistory` - Query command history

### Workflow Management
- `Get-PSCueWorkflows` - View learned workflows
- `Get-PSCueWorkflowStats` - Workflow statistics
- `Clear-PSCueWorkflows` - Clear workflows
- `Export-PSCueWorkflows` - Export workflows
- `Import-PSCueWorkflows` - Import workflows

### Smart Navigation
- `Invoke-PCD` (alias: `pcd`) - Smart directory change with inline predictions

### Debugging
- `Test-PSCueCompletion` - Test completions
- `Get-PSCueModuleInfo` - Module diagnostics

## Performance Targets

- Module function calls: <5ms
- Database queries: <10ms
- Inline prediction generation: <10ms
- PCD tab completion: <10ms
- PCD best-match navigation: <50ms
