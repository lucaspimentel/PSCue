# PSCue - Task List

**Last Updated**: 2025-10-31

This document tracks active and planned work for PSCue. For architectural details, see [TECHNICAL_DETAILS.md](TECHNICAL_DETAILS.md). For completed work, see [COMPLETED.md](COMPLETED.md).

---

## Current Status

**Phase 17.1 Complete**: Added ML-based N-gram sequence prediction.
- ✅ Created SequencePredictor component with bigram/trigram support
- ✅ Integrated with FeedbackProvider for automatic learning
- ✅ Extended database schema with command_sequences table
- ✅ Added persistence with additive merging for concurrent sessions
- ✅ Comprehensive testing: 25 unit + 8 integration + 9 performance tests
- ✅ Performance validated: <1ms cache lookup, <20ms total prediction
- ✅ Configuration via environment variables (enabled by default)
- ✅ Updated documentation (CLAUDE.md, TODO.md)
- ✅ All 365 tests passing (140 ArgumentCompleter + 225 Module)

**Current Total Tests**: 365 passing
- ArgumentCompleter: 140 tests
- Module: 225 tests (including Phases 11-17.1: learning, persistence, navigation, ML prediction)
- Phase 17.1 added 42 tests (25 unit + 8 integration + 9 performance)

---

## Active Work

No active work items. Phase 17.1 complete.

---

## Planned Work

### Phase 17.2: Advanced ML (Future Enhancement)
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
