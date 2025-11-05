# PSCue - Task List

**Last Updated**: 2025-11-05

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
- ✅ All tests passing

---

## Active Work

**Phase 17.2 Complete**: Privacy & Security - Sensitive Data Protection (2025-11-05)
- ✅ Built-in keyword filtering (*password*, *secret*, *api*key*, *token*, etc.)
- ✅ Heuristic detection (GitHub/Stripe keys, AWS keys, JWT tokens, Bearer tokens)
- ✅ Smart filtering (ignores long hex/base64 strings outside quotes)
- ✅ Custom patterns via PSCUE_IGNORE_PATTERNS environment variable
- ✅ Comprehensive test coverage for all filtering scenarios
- ✅ Updated documentation (CLAUDE.md, README.md with dedicated Privacy section)
- ✅ All tests passing

**v0.2.0 Released!** (2025-11-05)
- ✅ Fixed test compilation errors from architecture refactoring
- ✅ Updated FeedbackProvider tests to use PSCueModule static properties
- ✅ Removed obsolete CompletionCache tests
- ✅ Fixed xUnit async/await warning
- ✅ Successfully released v0.2.0 with working CI/CD
- ✅ Release includes: PSCue-win-x64.zip, PSCue-linux-x64.tar.gz, checksums.txt
- 🎉 First successful release available for installation!

**CI/CD Complete** (2025-11-04/05)
- ✅ GitHub Actions CI workflow configured for win-x64 and linux-x64
- ✅ GitHub Actions Release workflow configured and tested
- ✅ Fixed missing Functions/ directory in release archives (critical bug from v0.1.0)
- ✅ Added platform validation to install-remote.ps1
- ✅ v0.1.0 attempted (build failures), v0.2.0 successful

---

## Planned Work

### Phase 17.3: Advanced ML (Future Enhancement)
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
**Status**: Complete ✅

**Completed Items**:
- ✅ GitHub Actions CI workflow (.github/workflows/ci.yml)
  - Build & test on ubuntu-latest, windows-latest (win-x64, linux-x64 only)
  - Run `dotnet test` for all tests
  - Lint/format check temporarily disabled (needs .editorconfig)
- ✅ GitHub Actions Release workflow (.github/workflows/release.yml)
  - Trigger on git tags `v*` (e.g., `v1.0.0`)
  - Build NativeAOT binaries for win-x64 and linux-x64
  - Create platform-specific archives (zip for Windows, tar.gz for Linux)
  - Generate SHA256 checksums
  - Create GitHub release with all artifacts
  - **Fixed**: Added Functions/ directory to release archives
- ✅ Updated `scripts/install-remote.ps1`
  - Download from GitHub releases (latest or specific version)
  - Platform validation (win-x64, linux-x64 only)
  - **Fixed**: Copy Functions/ directory from archives

**Platform Support**: Windows x64, Linux x64 only (macOS removed)

**Manual Release Process**:
```bash
# 1. Update version in module/PSCue.psd1
# 2. Commit and create tag
git add module/PSCue.psd1
git commit -m "chore: bump version to 1.0.0"
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin main v1.0.0

# 3. GitHub Actions automatically:
#    - Builds for win-x64 and linux-x64
#    - Runs all tests on both platforms
#    - Creates release archives with Functions/ directory
#    - Generates checksums
#    - Creates GitHub release
```

**Next Distribution Steps**:
- [ ] Test v0.2.0 installation on fresh Windows and Linux systems
- [ ] Create Scoop manifest (Windows package manager)
- [ ] Publish to PowerShell Gallery
- [ ] Optional: Enable dotnet format checking (requires .editorconfig)
- [ ] Consider adding release notes generation automation

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
