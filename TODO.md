# PSCue - Task List

**Last Updated**: 2025-10-30

This document tracks active and planned work for PSCue. For architectural details, see [TECHNICAL_DETAILS.md](TECHNICAL_DETAILS.md). For completed work, see [COMPLETED.md](COMPLETED.md).

---

## Current Status

**Phase 16 Complete**: PowerShell module functions fully implemented, IPC layer removed.
- ✅ 10 PowerShell functions for cache, learning, database, and debugging
- ✅ PSCue.Debug CLI tool removed
- ✅ All IPC infrastructure removed (600+ lines of code)
- ✅ 44 IPC tests removed
- ✅ All 315 tests passing (140 ArgumentCompleter + 175 Module)
- ✅ Documentation updated across all files

**Current Total Tests**: 315 passing
- ArgumentCompleter: 140 tests
- Module: 175 tests (including Phases 11-15 learning, persistence, navigation)

---

## Active Work

### Phase 16.5: Testing ⚠️ IN PROGRESS
**Status**: PowerShell function testing needed

- [ ] Create test script: `test-scripts/test-module-functions.ps1`
  - [ ] Test each function with various parameters
  - [ ] Test pipeline support where applicable
  - [ ] Test `-WhatIf` and `-Confirm` for destructive operations
  - [ ] Test `-AsJson` output is valid JSON
  - [ ] Test error handling (invalid paths, etc.)
- [ ] Add unit tests: `test/PSCue.Module.Tests/ModuleFunctionsTests.cs`
  - [ ] Test public APIs added to classes
  - [ ] Test Export/Import functionality
  - [ ] Test GetAllEntries, GetStatistics, etc.
- [ ] Manual testing:
  - [ ] Load module, run each function
  - [ ] Verify tab completion works on function parameters
  - [ ] Verify Get-Help works for each function
  - [ ] Test in fresh PowerShell session

---

## Planned Work

### Phase 14: Future Enhancements
**Status**: Backlog

- [ ] Add ML-based prediction support
- [ ] Copy AI model scripts to `ai/` directory
- [ ] Create Scoop manifest
- [ ] Publish to PowerShell Gallery
- [ ] Add Homebrew formula (macOS/Linux)
- [ ] Cloud sync (sync learned data across machines, opt-in)
- [ ] Advanced learning: command sequences, workflow detection
- [ ] Semantic argument understanding (detect file paths, URLs, etc.)

### CI/CD & Distribution
**Status**: Not started

See [TECHNICAL_DETAILS.md#cicd-architecture](TECHNICAL_DETAILS.md#cicd-architecture) for full workflow design.

**Quick Summary**:
- [ ] GitHub Actions CI workflow (.github/workflows/ci.yml)
  - Build & test on ubuntu-latest, windows-latest, macos-latest
  - Run `dotnet test` and `dotnet format --verify-no-changes`
- [ ] GitHub Actions Release workflow (.github/workflows/release.yml)
  - Trigger on git tags `v*` (e.g., `v1.0.0`)
  - Build NativeAOT binaries for all platforms (win-x64, osx-x64, osx-arm64, linux-x64)
  - Create platform-specific archives (zip for Windows, tar.gz for others)
  - Generate SHA256 checksums
  - Create GitHub release with all artifacts
- [ ] Update `scripts/install-remote.ps1` for downloading from GitHub releases

**Manual Release Process**:
```bash
# 1. Update version in module/PSCue.psd1
# 2. Commit version bump
git add module/PSCue.psd1
git commit -m "Bump version to 1.0.0"

# 3. Create and push tag
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# 4. GitHub Actions automatically builds and creates release
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
# Cache Management
Get-PSCueCache, Clear-PSCueCache, Get-PSCueCacheStats

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
