# PSCue - Task List

**Last Updated**: 2025-11-08

This document tracks active and planned work for PSCue. For architectural details, see [TECHNICAL_DETAILS.md](TECHNICAL_DETAILS.md). For completed work, see [COMPLETED.md](COMPLETED.md).

---

## Current Status

**Latest Release**: v0.2.0 (2025-11-05)
- ✅ ML-based N-gram sequence prediction (Phase 17.1)
- ✅ Privacy & security - sensitive data protection (Phase 17.2)
- ✅ Partial word completion filtering (Phase 17.3)
- ✅ Automated CI/CD with GitHub Actions
- ✅ Production-ready with comprehensive test coverage
- 🎉 Available for installation via one-line command

**Recent Improvements** (unreleased):
- ✅ Multi-word prediction suggestions (Phase 17.4)
  - Shows common argument combinations like "git checkout master"
  - Tracks sequential argument patterns with usage frequency
  - Persists learned sequences to database
  - Comprehensive test coverage (28 new tests)
- ✅ Dynamic workflow learning (Phase 18.1) - **COMPLETE**
  - Learns command → next command transitions with timing data
  - Time-sensitive scoring adjusts predictions based on timing patterns
  - SQLite persistence with workflow_transitions table (8 tables total)
  - Automatic learning via FeedbackProvider integration
  - CommandPredictor integration for inline predictions
  - 5 PowerShell functions for workflow management
  - 35+ comprehensive tests
  - Configuration via environment variables
  - Full documentation in README.md and WORKFLOW-IMPROVEMENTS.md

**Installation**:
```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

---

## Planned Work

### Phase 17.5: Smart Directory Navigation (`pcd` command)
**Status**: Planned

Add a PowerShell function with smart tab completion for directory navigation, leveraging PSCue's learned directory data without interfering with native `cd` completion.

**Implementation**:
- [ ] Create `module/Functions/PCD.ps1` with `Invoke-PCD` function
  - [ ] Function logic: call `Set-Location` with best match from learned data
  - [ ] Fallback to native behavior if no learned data available
  - [ ] Handle empty argument (cd to home directory)
  - [ ] Tab completion: `Register-ArgumentCompleter -CommandName` (in-process)
    - [ ] Query `PSCueModule.KnowledgeGraph.GetSuggestions("cd", @())`
    - [ ] Filter by `$wordToComplete` (substring matching)
    - [ ] Return `CompletionResult` objects with full path tooltip
  - [ ] Create alias: `pcd` (PowerShell Change Directory)
- [ ] Update `module/PSCue.psd1`:
  - [ ] Add to `FunctionsToExport`: `'Invoke-PCD'`
  - [ ] Add to `AliasesToExport`: `'pcd'`
  - [ ] Add to `NestedModules` if needed (or dot-source in PSCue.psm1)
- [ ] Tests in `test/PSCue.Module.Tests/PCDTests.cs`:
  - [ ] Test function returns learned directories
  - [ ] Test tab completion ScriptBlock
  - [ ] Test fallback behavior when no learned data
  - [ ] Test filtering by partial input
- [ ] Documentation:
  - [ ] Update README.md with `pcd` command usage
  - [ ] Update CLAUDE.md quick reference

**Design Decisions**:
- **In-process completion**: Direct call to `PSCueModule.KnowledgeGraph` (<1ms, no IPC overhead)
- **Non-invasive**: Separate command (`pcd`), doesn't interfere with native `cd` tab completion
- **Reuses existing data**: ArgumentGraph already tracks `cd` command with directory normalization
- **Name choice**: `pcd` = PowerShell/PSCue Change Directory (alternatives: `scd`, `lcd`, `qcd`, `cue`)
- **Display**: PSReadLine handles menu display (`MenuComplete`, `ListView`) - we just return ordered results

**Future Enhancements** (algorithm improvements):
- [ ] Fuzzy matching (e.g., "dt" → "D:\source\datadog\dd-trace-dotnet")
- [ ] Frecency scoring (frequency + recency blend) for better ranking
- [ ] Multi-segment substring matching (e.g., "dog/trace" → "datadog/dd-trace-dotnet")
- [ ] Working directory proximity (prefer subdirectories of current location)

### Phase 17.6: Advanced ML (Future Enhancement)
**Status**: Backlog

- [ ] Semantic embeddings for argument similarity (ONNX Runtime)
- [ ] Pre-trained or user-trained models
- [ ] Background pre-computation for heavy ML
- [ ] Research ONNX Runtime + NativeAOT compatibility

### Phase 18.1: Dynamic Workflow Learning
**Status**: ✅ **COMPLETE** (2025-11-07)

**Completed**:
- ✅ WorkflowLearner.cs core infrastructure (~500 lines)
- ✅ SQLite schema extension (workflow_transitions table - 8 tables total)
- ✅ Module initialization and lifecycle integration
- ✅ FeedbackProvider integration (automatic learning)
- ✅ CommandPredictor integration (inline predictions at empty prompt)
- ✅ Configuration via environment variables (4 new vars)
- ✅ PersistenceManager save/load methods
- ✅ Comprehensive testing (35+ unit tests)
- ✅ PowerShell module functions (5 functions: Get/Stats/Clear/Export/Import)
- ✅ Documentation updates (CLAUDE.md, README.md, TODO.md)

**Phase 18.2 (Time-Based Detection)** also completed as part of 18.1:
- ✅ Time-sensitive scoring with timing pattern awareness
- ✅ Time delta tracking in database
- ✅ Confidence adjustments based on timing (1.5× to 0.8× boost)

**See**: [COMPLETED.md](COMPLETED.md) for detailed implementation notes
**Design**: [WORKFLOW-IMPROVEMENTS.md](WORKFLOW-IMPROVEMENTS.md) for full design details

### Phase 18.3+: Future Workflow Enhancements
**Status**: Planned (see WORKFLOW-IMPROVEMENTS.md)

- [ ] Workflow chains 3+ commands / trigrams (Phase 18.3)
- [ ] Project-type detection (Phase 18.4)
- [ ] Workflow interruption recovery (Phase 18.5)
- [ ] Error-driven workflow adjustment (Phase 18.6)
- [ ] Multi-tool workflows (Phase 18.7)

**Note**: Phase 18.2 (Time-Based Detection) was completed as part of Phase 18.1.

### Phase 19: Distribution & Packaging
**Status**: Backlog

- [ ] Copy AI model scripts to `ai/` directory
- [ ] Create Scoop manifest
- [ ] Publish to PowerShell Gallery
- [ ] Add Homebrew formula (macOS/Linux)
- [ ] Cloud sync (sync learned data across machines, opt-in)

### Distribution & Package Managers
**Status**: In Progress

**Next Steps**:
- [ ] Test v0.2.0 installation on fresh Windows and Linux systems
- [ ] Create Scoop manifest (Windows package manager)
- [ ] Publish to PowerShell Gallery
- [ ] Optional: Enable dotnet format checking in CI (requires .editorconfig)
- [ ] Optional: Automate release notes generation

**Release Process** (for reference):
```bash
# 1. Update version in module/PSCue.psd1
# 2. Commit and tag
git add module/PSCue.psd1
git commit -m "chore: bump version to X.Y.Z"
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin main vX.Y.Z

# 3. GitHub Actions automatically builds and publishes
```

---

## Quick Reference

### Key Files
- **Task Tracking**: `TODO.md` (this file)
- **Architecture**: `TECHNICAL_DETAILS.md`
- **AI Agent Guide**: `CLAUDE.md`
- **User Guide**: `README.md`
- **Database Functions**: `DATABASE-FUNCTIONS.md`
- **Completed Phases**: `COMPLETED.md`

### Build & Test Commands
```bash
# Build
dotnet build src/PSCue.Module/ -c Release -f net9.0
dotnet publish src/PSCue.ArgumentCompleter/ -c Release -r win-x64

# Test
dotnet test test/PSCue.ArgumentCompleter.Tests/
dotnet test test/PSCue.Module.Tests/
dotnet test --filter "FullyQualifiedName~ModuleFunctions"

# Install locally
./scripts/install-local.ps1
```

### Module Functions (Phase 16 + Phase 18.1)
```powershell
# Learning Management
Get-PSCueLearning, Clear-PSCueLearning, Export-PSCueLearning, Import-PSCueLearning, Save-PSCueLearning

# Database Management
Get-PSCueDatabaseStats, Get-PSCueDatabaseHistory

# Workflow Management (Phase 18.1)
Get-PSCueWorkflows, Get-PSCueWorkflowStats, Clear-PSCueWorkflows, Export-PSCueWorkflows, Import-PSCueWorkflows

# Debugging
Test-PSCueCompletion, Get-PSCueModuleInfo
```

---

## Notes

- This is a living document - update as tasks progress
- Check off items as completed
- Add new items as discovered
- Move completed phases to COMPLETED.md
- Move large architectural details to TECHNICAL_DETAILS.md
