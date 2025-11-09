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
- ✅ Dynamic workflow learning (Phase 18.1)
  - Learns command → next command transitions with timing data
  - Time-sensitive scoring adjusts predictions based on timing patterns
  - SQLite persistence with workflow_transitions table (8 tables total)
  - Automatic learning via FeedbackProvider integration
  - CommandPredictor integration for inline predictions
  - PowerShell functions for workflow management
  - Configuration via environment variables
  - Full documentation in README.md and WORKFLOW-IMPROVEMENTS.md
- ✅ Smart directory navigation with `pcd` command (Phases 17.5 + 17.6 + 17.7 + recent fixes)
  - PowerShell function with inline predictions and tab completion
  - Learns from cd/Set-Location command usage
  - **Phase 17.6 Enhancements**:
    - Fuzzy matching with Levenshtein distance + substring matching
    - Frecency scoring: configurable blend of frequency (50%), recency (30%), distance (20%)
    - Distance-aware scoring: prefers nearby directories (parent/child/sibling)
    - Best-match navigation: `pcd datadog` finds match without Tab
    - Well-known shortcuts (~, ..) with highest priority
    - Optional recursive filesystem search
    - Match type indicators in tooltips
  - **Phase 17.7 Enhancements** (2025-11-09):
    - Inline predictions integrated with CommandPredictor
    - Relative path conversion (e.g., .., ./src, ../sibling) to reduce visual noise
    - Full paths still shown in tooltips
    - Filters out unhelpful suggestions (current directory, "-")
  - **Recent Fixes** (2025-11-09):
    - Cross-drive validation: Prevents invalid relative paths across drives
    - Existence filtering: Only suggests directories that exist on filesystem
    - Current directory filtering: Doesn't suggest ~ when already in home directory
    - Path normalization: All paths include trailing \ to prevent duplicates
    - Better fuzzy matching: Requests 10 suggestions for more reliable navigation
    - Display improvements: Shows relative paths in list, inserts absolute paths
  - In-process access to learning data (<1ms)
  - Non-invasive: separate command, doesn't interfere with native cd
  - Performance: <10ms tab completion, <10ms predictor, <50ms best-match

**Installation**:
```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

---

## Planned Work

### Phase 17.5: Smart Directory Navigation (`pcd` command)
**Status**: ✅ **COMPLETE** (2025-11-08)

Add a PowerShell function with smart tab completion for directory navigation, leveraging PSCue's learned directory data without interfering with native `cd` completion.

**Completed**:
- ✅ Created `module/Functions/PCD.ps1` with `Invoke-PCD` function
  - ✅ Function logic: calls `Set-Location` with argument
  - ✅ Fallback to native behavior if no learned data available
  - ✅ Handles empty argument (cd to home directory)
  - ✅ Tab completion: `Register-ArgumentCompleter -CommandName` (in-process)
    - ✅ Queries `PSCueModule.KnowledgeGraph.GetSuggestions("cd", @())`
    - ✅ Filters by `$wordToComplete` (StartsWith matching)
    - ✅ Returns `CompletionResult` objects with usage count and last used date tooltip
  - ✅ Created alias: `pcd` (PowerShell Change Directory)
- ✅ Updated `module/PSCue.psd1`:
  - ✅ Added to `FunctionsToExport`: `'Invoke-PCD'`
  - ✅ Added to `AliasesToExport`: `'pcd'`
  - ✅ Also added missing workflow management functions to exports
- ✅ Created comprehensive tests in `test/PSCue.Module.Tests/PCDTests.cs`:
  - ✅ 19 tests covering all functionality
  - ✅ ArgumentGraph integration tests
  - ✅ Tab completion simulation tests
  - ✅ Scoring and ranking tests
  - ✅ Edge cases and performance tests
- ✅ Documentation:
  - ✅ Updated README.md with `pcd` command usage and examples
  - ✅ Updated CLAUDE.md quick reference

**Files Modified**:
- `module/Functions/PCD.ps1` (new file)
- `module/PSCue.psd1` (added function/alias exports)
- `test/PSCue.Module.Tests/PCDTests.cs` (new file)
- `README.md` (added pcd usage section)
- `CLAUDE.md` (added pcd to function reference)

**Design Decisions**:
- **In-process completion**: Direct call to `PSCueModule.KnowledgeGraph` (<1ms, no IPC overhead)
- **Non-invasive**: Separate command (`pcd`), doesn't interfere with native `cd` tab completion
- **Reuses existing data**: ArgumentGraph already tracks `cd` command with directory normalization
- **Name choice**: `pcd` = PowerShell/PSCue Change Directory (alternatives: `scd`, `lcd`, `qcd`, `cue`)
- **Display**: PSReadLine handles menu display (`MenuComplete`, `ListView`) - we just return ordered results

**Note**: See Phase 17.6 below for enhanced algorithm improvements (fuzzy matching, frecency scoring, distance awareness).

### Phase 17.6: Enhanced PCD Algorithm
**Status**: ✅ **COMPLETE** (2025-11-08)

Enhance the `pcd` command with advanced matching, scoring, and navigation features.

**Goals**:
1. **Well-known shortcuts**: Fast handling of `~`, `..`, `.` with highest priority
2. **Fuzzy matching**: Levenshtein distance + substring matching for flexible directory finding
3. **Frecency scoring**: Configurable blend of frequency + recency + distance
4. **Distance scoring**: Boost directories closer to current location (parent/child/sibling)
5. **Recursive search**: Optional deep filesystem search for matching directories
6. **Best-match navigation**: `pcd <name>` finds best match if no exact path exists

**Implementation Plan**:

**Core Engine** (✅ COMPLETE):
- [x] Create `src/PSCue.Module/PCDCompletionEngine.cs` with multi-stage algorithm
  - [x] Stage 1: Well-known shortcuts (~, .., .) with priority 1000/999/998
  - [x] Stage 2: Learned directories with enhanced scoring
    - [x] Match scoring: Exact (1.0) > Prefix (0.9) > Fuzzy (0.0-0.8)
    - [x] Fuzzy matching: Substring + Levenshtein distance
    - [x] Frecency: Configurable weights (default: 50% frequency, 30% recency, 20% distance)
    - [x] Distance scoring: Same dir (1.0), Parent (0.9), Child (0.85-0.5), Sibling (0.7), Ancestor (0.6-0.1)
  - [x] Stage 3: Recursive filesystem search (optional, configurable depth)

**PowerShell Integration** (✅ COMPLETE):
- [x] Update `module/Functions/PCD.ps1` (205 lines total)
  - [x] Tab completion: Use `PcdCompletionEngine.GetSuggestions()`
    - [x] Create engine instance with config from env vars
    - [x] Call `GetSuggestions($wordToComplete, $currentDirectory, 20)`
    - [x] Convert `PcdSuggestion` objects to `CompletionResult`
    - [x] Show match type indicators in tooltips
  - [x] `Invoke-PCD` function: Smart path resolution with best-match fallback
    - [x] If path doesn't exist, use engine to find best match
    - [x] Show message: "No exact match, navigating to: <best-match>"
    - [x] Fall back to Set-Location error if no good matches
  - [x] Read configuration from environment variables

**Testing** (✅ COMPLETE):
- [x] Create `test/PSCue.Module.Tests/PcdEnhancedTests.cs`
  - [x] Well-known shortcuts tests
  - [x] Fuzzy matching tests
  - [x] Frecency scoring tests
  - [x] Distance scoring tests
  - [x] Recursive search tests
  - [x] Best-match navigation tests
  - [x] Performance tests
  - [x] Edge cases and error handling
  - [x] Tooltip and display tests
  - [x] All tests passing ✅

**Documentation** (✅ COMPLETE):
- [x] Update `README.md`: Enhanced features section with full algorithm details
- [x] Update `CLAUDE.md`: Algorithm details and file references
- [x] Update `TODO.md`: Mark Phase 17.6 complete

**Environment Variables**:
```powershell
# Scoring weights (must sum to ~1.0 with match weight)
$env:PSCUE_PCD_FREQUENCY_WEIGHT = "0.5"  # Default: 0.5 (50%)
$env:PSCUE_PCD_RECENCY_WEIGHT = "0.3"    # Default: 0.3 (30%)
$env:PSCUE_PCD_DISTANCE_WEIGHT = "0.2"   # Default: 0.2 (20%)

# Recursive search (disabled by default for performance)
$env:PSCUE_PCD_RECURSIVE_SEARCH = "true"  # Default: false
$env:PSCUE_PCD_MAX_DEPTH = "3"            # Default: 3 levels deep
```

**Files Modified**:
- `src/PSCue.Module/PcdCompletionEngine.cs` (new file) ✅
- `module/Functions/PCD.ps1` (enhanced) ✅
- `test/PSCue.Module.Tests/PcdEnhancedTests.cs` (new file) ✅
- `README.md` (enhanced pcd section) ✅
- `CLAUDE.md` (algorithm details + file references) ✅
- `TODO.md` (Phase 17.6 marked complete) ✅

**Key Achievements**:
- Multi-stage completion algorithm: Shortcuts → Learned → Recursive
- Fuzzy matching with Levenshtein distance + substring matching
- Configurable frecency scoring (frequency + recency + distance)
- Distance-aware scoring for nearby directories
- Best-match navigation without Tab completion
- Comprehensive test coverage for all features
- Full documentation in README and CLAUDE.md
- Performance targets met: <10ms tab completion, <50ms best-match

### Phase 17.7: PCD Inline Predictions & Relative Paths
**Status**: ✅ **COMPLETE** (2025-11-09)

Enhance `pcd` with inline predictions and relative path display.

**Completed**:
- [x] Inline predictor integration
  - [x] Add pcd command detection to CommandPredictor
  - [x] Create GetPcdSuggestions() method
  - [x] Support both "pcd" and "Invoke-PCD" commands
  - [x] Show suggestions even for "pcd<space>" (always show predictions)
  - [x] Return multiple suggestions (up to 5 for predictor)
  - [x] Add environment variable helper methods
- [x] Relative path conversion
  - [x] Add ToRelativePath() method to PcdCompletionEngine
  - [x] Convert parent directory to ".."
  - [x] Convert child directories to "./subdir" when shorter
  - [x] Convert sibling directories to "../sibling" when shorter
  - [x] Fall back to absolute path if relative isn't shorter
  - [x] Apply to both tab completion and inline predictor
  - [x] Keep full paths in DisplayPath and tooltips
- [x] Filter unhelpful suggestions
  - [x] Don't suggest current directory (".")
  - [x] Don't suggest previous directory ("-")
  - [x] Don't suggest path that matches current directory

**Files Modified**:
- `src/PSCue.Module/CommandPredictor.cs` (added GetPcdSuggestions, env helpers)
- `src/PSCue.Module/PcdCompletionEngine.cs` (added ToRelativePath, updated suggestions)
- `test/PSCue.Module.Tests/PcdEnhancedTests.cs` (updated for filtering behavior)

**Key Achievements**:
- Inline predictions work like other commands (git, gh, etc.)
- Relative paths reduce visual noise significantly
- Full context still available in tooltips
- Performance maintained: <10ms for predictor responses

### Phase 17.8: Advanced ML (Future Enhancement)
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
