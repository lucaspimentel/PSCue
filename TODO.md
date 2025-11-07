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
- 🚧 Dynamic workflow learning (Phase 18.1) - **In Progress**
  - Learns command → next command transitions with timing data
  - Time-sensitive scoring adjusts predictions based on timing patterns
  - SQLite persistence with workflow_transitions table
  - Automatic learning via FeedbackProvider integration
  - Configuration via environment variables
  - **Next**: CommandPredictor integration for inline predictions

**Installation**:
```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

---

## Planned Work

### Phase 17.5: Advanced ML (Future Enhancement)
**Status**: Backlog

- [ ] Semantic embeddings for argument similarity (ONNX Runtime)
- [ ] Pre-trained or user-trained models
- [ ] Background pre-computation for heavy ML
- [ ] Research ONNX Runtime + NativeAOT compatibility

### Phase 18.1: Dynamic Workflow Learning
**Status**: 🚧 In Progress (~75% complete)

**Completed**:
- ✅ WorkflowLearner.cs core infrastructure (~500 lines)
- ✅ SQLite schema extension (workflow_transitions table)
- ✅ Module initialization and lifecycle integration
- ✅ FeedbackProvider integration (automatic learning)
- ✅ Configuration via environment variables
- ✅ PersistenceManager save/load methods

**In Progress**:
- 🔄 CommandPredictor integration (show workflow predictions inline)

**Remaining**:
- [ ] Comprehensive testing (~25 unit tests)
- [ ] Integration tests (workflow recording & predictions)
- [ ] PowerShell module functions (Get-PSCueWorkflows, etc.)
- [ ] Documentation updates (README.md, design doc status)

**See**: [WORKFLOW-IMPROVEMENTS.md](WORKFLOW-IMPROVEMENTS.md) for full design details

### Phase 18.2+: Future Workflow Enhancements
**Status**: Planned (see WORKFLOW-IMPROVEMENTS.md)

- [ ] Time-based workflow detection (Phase 18.2)
- [ ] Workflow chains 3+ commands (Phase 18.3)
- [ ] Project-type detection (Phase 18.4)
- [ ] Workflow interruption recovery (Phase 18.5)
- [ ] Error-driven workflow adjustment (Phase 18.6)
- [ ] Multi-tool workflows (Phase 18.7)

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
