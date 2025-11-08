# PSCue - Task List

**Last Updated**: 2025-11-06

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

### Phase 18: Future Enhancements
**Status**: Backlog

- [ ] Copy AI model scripts to `ai/` directory
- [ ] Create Scoop manifest
- [ ] Publish to PowerShell Gallery
- [ ] Add Homebrew formula (macOS/Linux)
- [ ] Cloud sync (sync learned data across machines, opt-in)
- [ ] Advanced learning: command sequences, workflow detection
- [ ] Semantic argument understanding (detect file paths, URLs, etc.)

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

### Module Functions (Phase 16)
```powershell
# Learning Management
Get-PSCueLearning, Clear-PSCueLearning, Export-PSCueLearning, Import-PSCueLearning, Save-PSCueLearning

# Database Management
Get-PSCueDatabaseStats, Get-PSCueDatabaseHistory

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
