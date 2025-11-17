# PSCue - Task List

**Last Updated**: 2025-11-17

This document tracks active and planned work for PSCue. For architectural details, see [TECHNICAL_DETAILS.md](docs/TECHNICAL_DETAILS.md). For completed work, see [COMPLETED.md](docs/COMPLETED.md).

---

## Current Status

**Latest Release**: v0.3.0 (2025-11-09)
- ✅ Multi-word prediction suggestions (Phase 17.4) - archived to docs/COMPLETED.md
- ✅ Dynamic workflow learning (Phase 18.1) - archived to docs/COMPLETED.md
- ✅ Smart directory navigation with `pcd` command (Phases 17.5-17.8) - archived to docs/COMPLETED.md
  - Phase 17.5: Basic `pcd` command with tab completion
  - Phase 17.6: Enhanced algorithm (fuzzy matching, frecency, distance scoring)
  - Phase 17.7: Inline predictions and relative paths
  - Phase 17.8: Partial command predictions (typing "g" suggests "git")
- ✅ Comprehensive test coverage (300+ tests)
- 🎉 Available for installation via one-line command

**Previous Release**: v0.2.0 (2025-11-05)
- ML-based N-gram sequence prediction (Phase 17.1) - archived
- Privacy & security - sensitive data protection (Phase 17.2) - archived
- Partial word completion filtering (Phase 17.3) - archived
- Automated CI/CD with GitHub Actions

**Recent Improvements** (not yet released):
- ✅ Phase 17.9: PCD completion improvements - archived to docs/COMPLETED.md
- ✅ Phase 19.0: PCD precedence review and optimization - archived to docs/COMPLETED.md
- ✅ Phase 20: Parameter-value binding - archived to docs/COMPLETED.md
- **Bug Fixes**:
  - PCD exact match priority: Exact path matches now always appear first (100× score boost)
  - PCD trailing separators: Directory paths consistently end with `\` in both tab completion and inline predictions

**Next Up**:
- Phase 21: PCD quality improvements (symlink deduplication, cache/metadata filtering, exact match boost, improved fuzzy matching)

**Installation**:
```powershell
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/scripts/install-remote.ps1 | iex
```

---

## Planned Work

### Phase 21: PCD Quality Improvements (Symlinks, Filtering, Match Quality)
**Status**: Planned
**Priority**: High
**Estimated Effort**: 15-20 hours

**Goal**: Fix PCD suggestion quality issues: symlink deduplication, cache/metadata directory filtering, exact match prioritization, and improved fuzzy matching.

**Issues Identified**:
1. **Symlink resolution causes duplicate suggestions**: Both real path (`D:\source\...`) and symlinked path (`C:\Users\lucas\source\...`) appear as separate entries
2. **ICommandPredictor wrong ordering**: Fuzzy matches rank higher than exact substring matches (e.g., `dd-trace-dotnet-APMSVLS-58` before `dd-trace-dotnet`)
3. **ArgumentCompleter includes cache/metadata noise**: `.codeium`, `.claude`, `.dotnet`, etc. clutter results
4. **Fuzzy matching too permissive**: Unrelated directories match (e.g., `dd-trace-js` matches "dd-trace-dotnet")
5. **No exact match boost**: Exact substring matches don't get prioritized over partial/fuzzy matches

**Tasks**:

1. **Phase 21.1: Symlink Resolution & Deduplication** (~4 hours)
   - [ ] Add symlink resolution in path normalization (`PcdCompletionEngine.cs`)
   - [ ] Use `Directory.ResolveLinkTarget()` or `FileInfo.LinkTarget` to resolve symlinks
   - [ ] Normalize all paths to real paths before deduplication
   - [ ] Store and compare paths as resolved real paths
   - [ ] Test on Windows with symlinks, junctions, and directory links
   - [ ] Add regression test for user's scenario: `C:\Users\lucas\source` → `D:\source` symlink

2. **Phase 21.2: Cache/Metadata Directory Filtering** (~4 hours)
   - [ ] Add blocklist of cache/metadata patterns in `PcdConfiguration.cs`:
     - `.codeium/`, `.claude/`, `.dotnet/`, `.nuget/`, `.git/` (internals only, not `.github/`)
     - `node_modules/`, `bin/`, `obj/`, `target/` (build artifacts)
   - [ ] Filter out blocklisted directories in `PcdCompletionEngine` UNLESS user explicitly typed pattern
   - [ ] Detection: Check if input string contains blocklisted segment (e.g., ".claude" in input allows `.claude/` results)
   - [ ] Add configuration: `$env:PSCUE_PCD_ENABLE_DOT_DIR_FILTER` (default: true)
   - [ ] Add configuration: `$env:PSCUE_PCD_CUSTOM_BLOCKLIST` (comma-separated patterns, optional)
   - [ ] Test filtering behavior (both tab and predictor)
   - [ ] Test explicit typing override (typing `.claude` shows `.claude/` directories)

3. **Phase 21.3: Exact Match Scoring Boost** (~3 hours)
   - [ ] Add exact substring match detection in `PcdCompletionEngine.cs` scoring logic
   - [ ] Apply significant boost (e.g., +10.0 or 3x multiplier) when:
     - Directory name exactly equals search term
     - Directory path ends with exact search term (e.g., `D:\foo\bar\dd-trace-dotnet`)
   - [ ] Ensure exact matches always rank above fuzzy matches
   - [ ] Add scoring configuration: `$env:PSCUE_PCD_EXACT_MATCH_BOOST` (default: 3.0)
   - [ ] Add regression test: "dd-trace-dotnet" should rank `dd-trace-dotnet` above `dd-trace-dotnet-APMSVLS-58`
   - [ ] Test with various scenarios (exact name, exact suffix, partial match)

4. **Phase 21.4: Improve Fuzzy Matching Quality** (~3 hours)
   - [ ] Tighten fuzzy matching criteria in `PcdCompletionEngine.cs`:
     - Option A: Reduce acceptable Levenshtein distance threshold
     - Option B: Require minimum substring match percentage (e.g., 70% of search term must match)
     - Option C: Reject matches that only share a prefix if search term is long (e.g., >10 chars)
   - [ ] Add configuration: `$env:PSCUE_PCD_FUZZY_MIN_MATCH_PCT` (default: 0.7 = 70%)
   - [ ] Test that `dd-trace-js` no longer matches "dd-trace-dotnet"
   - [ ] Test that legitimate typos still match (e.g., "dd-trac-dotnet" matches "dd-trace-dotnet")
   - [ ] Balance precision vs recall (don't break valid fuzzy matches)

5. **Phase 21.5: Integration Testing** (~2 hours)
   - [ ] Create comprehensive test suite for user's scenario:
     - Input: "pcd dd-trace-dotnet" from `C:\Users\lucas`
     - Expected top results: `D:\source\datadog\dd-trace-dotnet`, `D:\source\datadog\dd-trace-dotnet-APMSVLS-58` (in that order)
     - Expected filtered out: `.codeium`, `.claude`, `.dotnet`, `dd-trace-js`
   - [ ] Test symlink deduplication (no duplicates for symlinked paths)
   - [ ] Test exact match prioritization (predictor + tab completion)
   - [ ] Test cache directory filtering (with and without explicit typing)
   - [ ] Performance regression tests (ensure <50ms tab, <10ms predictor)
   - [ ] Cross-platform tests (Windows symlinks, Linux symlinks)

6. **Phase 21.6: Documentation Updates** (~1 hour)
   - [ ] Update `CLAUDE.md`: Document new PCD configuration options
   - [ ] Update `CLAUDE.md`: Document symlink resolution behavior
   - [ ] Update `CLAUDE.md`: Document cache/metadata filtering rules
   - [ ] Update `README.md`: Configuration section with new env vars
   - [ ] Add troubleshooting entry for symlink duplicates

**New Configuration Variables**:
```powershell
# Cache/metadata filtering (Phase 21.2)
$env:PSCUE_PCD_ENABLE_DOT_DIR_FILTER = "true"      # Default: true
$env:PSCUE_PCD_CUSTOM_BLOCKLIST = ".myapp,.temp"   # Optional: additional patterns

# Match quality scoring (Phase 21.3, 21.4)
$env:PSCUE_PCD_EXACT_MATCH_BOOST = "3.0"           # Default: 3.0 (multiply score by 3)
$env:PSCUE_PCD_FUZZY_MIN_MATCH_PCT = "0.7"         # Default: 0.7 (70% of search term must match)
```

**Success Criteria**:
- ✅ No duplicate suggestions for symlinked paths
- ✅ Exact matches rank higher than fuzzy matches
- ✅ Cache/metadata directories filtered by default
- ✅ Fuzzy matching rejects unrelated directories
- ✅ Explicit typing overrides blocklist filtering
- ✅ Performance targets maintained (<50ms tab, <10ms predictor)
- ✅ All tests passing
- ✅ Cross-platform support (Windows + Linux symlinks)

**Dependencies**: Phase 19.0 (PCD shared configuration infrastructure)

---

### Phase 19.1: Tab Completion on Empty Input
**Status**: Deferred (more complex than initially estimated)
**Priority**: Low
**Estimated Effort**: TBD (requires more research)

**Goal**: Show tab completions when user presses `<Tab>` on empty command line, displaying frequent/recent commands from PSCue's learned data.

**Current Limitation**:
- PowerShell's `CompleteInput()` explicitly returns empty results for empty input (by design)
- `Register-ArgumentCompleter` cannot intercept Tab on empty command line
- `ICommandPredictor` does NOT show predictions until user begins typing

**Challenges Discovered**:
- TabExpansion2 override approach has compatibility issues
- More research needed on PSReadLine integration
- Potential conflicts with other modules

**Documentation**: See [docs/TAB_COMPLETION_EMPTY_INPUT.md](docs/TAB_COMPLETION_EMPTY_INPUT.md) for research notes.

**Next Steps** (when revisited):
- [ ] Research PSReadLine integration approaches
- [ ] Prototype TabExpansion2 override compatibility
- [ ] Test with common PowerShell modules
- [ ] Evaluate feasibility and user value

---

### Phase 19.2: Advanced ML (Future Enhancement)
**Status**: Backlog

- [ ] Semantic embeddings for argument similarity (ONNX Runtime)
- [ ] Pre-trained or user-trained models
- [ ] Background pre-computation for heavy ML
- [ ] Research ONNX Runtime + NativeAOT compatibility

---

## Phase 18: Workflow Improvements

### Phase 18.3: Workflow Chains (3+ Commands)
**Status**: Planned
**Priority**: Medium
**Estimated Effort**: 25-30 hours

**Goal**: Track and predict based on 2-3 command history (trigrams/4-grams).

**Current Limitation**: Only considers last command. Misses longer workflow patterns like `git add → git commit → git push`.

**Solution**: Extend `SequencePredictor.cs` to support variable-order n-grams (bigrams + trigrams).

**Tasks**:
1. [ ] Extend `WorkflowLearner` to support trigrams (~150 lines)
   - [ ] Add trigram graph data structure alongside bigram graph
   - [ ] Implement n-gram key generation (e.g., "git add→git commit")
2. [ ] Add n-gram order configuration
   - [ ] Environment variable: `PSCUE_WORKFLOW_NGRAM_ORDER` (2 or 3)
   - [ ] Prefer higher-order matches when available
3. [ ] Update database schema with ngram_order column
   ```sql
   ALTER TABLE workflow_transitions ADD COLUMN ngram_order INTEGER NOT NULL DEFAULT 2;
   CREATE INDEX idx_workflow_sequence ON workflow_transitions(ngram_order, command_sequence);
   ```
4. [ ] Modify prediction logic to try trigram then bigram (~100 lines)
   - [ ] Query trigram first (more specific, higher confidence boost 1.2×)
   - [ ] Fallback to bigram if no trigram match
   - [ ] De-duplicate results by command
5. [ ] Write tests for n-gram matching (~15 test cases)
   - [ ] Trigram recording and querying
   - [ ] Confidence boosting for longer matches
   - [ ] Graceful fallback to bigrams
6. [ ] Documentation updates

**Example Behavior**:
```powershell
# After learning: git add . → git commit → git push
PS> git add .<Enter>
PS> git c█
    # Bigram (git add → ?): "git commit" (70% confidence)

PS> git commit -m "fix"<Enter>
PS> git p█
    # Trigram (git add → git commit → ?): "git push" (90% confidence)
    # ↑ Higher confidence because sequence is more specific
```

**Dependencies**: Phase 18.1 (WorkflowLearner must exist)

**Success Criteria**:
- ✅ Trigrams recorded and queried
- ✅ Higher confidence for longer matches
- ✅ Graceful fallback to bigrams
- ✅ All tests passing

---

### Phase 18.4: Project-Type Detection
**Status**: Planned
**Priority**: Medium-Low
**Estimated Effort**: 20-25 hours

**Goal**: Context-aware suggestions based on project type (Rust, Node, .NET, Python, Go, Docker).

**Current Limitation**: PSCue suggests irrelevant commands (e.g., `npm` in Rust projects, `cargo` in Node projects).

**Solution**: Detect project type from directory contents and adjust suggestion priorities.

**Architecture**:
```csharp
// New class: src/PSCue.Module/ProjectTypeDetector.cs (~250 lines)
public class ProjectTypeDetector
{
    // Cache detection results per directory (5-minute TTL)
    private ConcurrentDictionary<string, ProjectContext> _projectCache;

    public enum ProjectType
    {
        Unknown, Rust, Node, DotNet, Python, Go, Docker, Kubernetes, Git
    }

    public ProjectContext DetectProjectType(string currentDirectory)
    {
        // Walk up directory tree looking for indicator files:
        // - Rust: Cargo.toml
        // - Node: package.json
        // - .NET: *.csproj, *.sln
        // - Python: requirements.txt, setup.py, pyproject.toml
        // - Go: go.mod
        // - Docker: Dockerfile
        // - Git: .git directory
    }
}
```

**Scoring Adjustments**:
- **Boost relevant commands**: 1.5× for matching project types (e.g., `cargo` in Rust project)
- **De-prioritize unrelated**: 0.5× for non-matching tools (e.g., `npm` in Rust project)

**Tasks**:
1. [ ] Create `ProjectTypeDetector.cs` (~250 lines)
   - [ ] File-based detection logic
   - [ ] Directory tree walking (up to root)
   - [ ] Caching with 5-minute TTL
   - [ ] Support for multi-type projects (e.g., Node + Docker)
2. [ ] Integrate with GenericPredictor (~80 lines)
   - [ ] Call detector before scoring suggestions
   - [ ] Apply project-type boost multipliers
3. [ ] Add project-type boost mappings
   - [ ] Define boost/penalty per project type and command
4. [ ] Add configuration environment variables
   - [ ] `PSCUE_PROJECT_DETECTION` (default: true)
   - [ ] `PSCUE_PROJECT_CACHE_TTL` (default: 300 seconds)
   - [ ] `PSCUE_PROJECT_BOOST_FACTOR` (default: 1.5)
5. [ ] Write tests for detection logic (~12 test cases)
   - [ ] Project type detection accuracy
   - [ ] Cache hit/miss behavior
   - [ ] Performance (<10ms cache miss)
6. [ ] Documentation updates

**Example Behavior**:
```powershell
# In a Rust project (Cargo.toml present)
PS> ca█
    # Suggestions:
    # - cargo build (score: 0.95)  ← boosted 1.5×
    # - cargo test  (score: 0.90)  ← boosted 1.5×
    # - cat         (score: 0.45)  ← no boost

# In a Node project (package.json present)
PS> ca█
    # - cat         (score: 0.60)  ← no boost
    # - cargo build (score: 0.30)  ← de-prioritized 0.5×
```

**Dependencies**: None (standalone feature)

**Success Criteria**:
- ✅ Project types detected correctly
- ✅ Relevant commands boosted
- ✅ Irrelevant commands de-prioritized
- ✅ Performance acceptable (<10ms cache miss)
- ✅ All tests passing

---

### Phase 18.5: Workflow Interruption Recovery
**Status**: Planned
**Priority**: Low
**Estimated Effort**: 15-20 hours

**Goal**: Remember and resume interrupted workflows.

**Current Limitation**: Unrelated commands break workflow context. After `git add` → `ls` → `cat README.md`, PSCue forgets the git workflow.

**Solution**: Track workflow sessions in-memory, detect resumption of interrupted workflows.

**Architecture**:
```csharp
// New class: src/PSCue.Module/WorkflowSessionTracker.cs (~200 lines)
public class WorkflowSessionTracker
{
    private List<WorkflowSession> _activeSessions = new();

    public class WorkflowSession
    {
        public Guid SessionId { get; set; }
        public List<CommandHistoryEntry> Commands { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public WorkflowType Type { get; set; }  // Git, Docker, Build, etc.
        public bool IsActive => (DateTime.UtcNow - LastActivityTime) < TimeSpan.FromMinutes(30);
    }

    // Heuristic: Commands with shared context belong to same session
    public WorkflowSession? GetActiveSession(string command)
    {
        // Group by command family (git, docker, cargo, npm, etc.)
        // Return most recent matching session within 30-minute window
    }
}
```

**Tasks**:
1. [ ] Create `WorkflowSessionTracker.cs` (~200 lines)
   - [ ] Session tracking data structures
   - [ ] Command family detection (git, docker, rust, node, etc.)
   - [ ] Session timeout (30 minutes default)
   - [ ] LRU eviction (max 5 sessions)
2. [ ] Track active sessions in-memory (no persistence needed)
3. [ ] Detect session resumption
   - [ ] Match new command to existing session by family
4. [ ] Integrate with GenericPredictor (~50 lines)
   - [ ] Boost suggestions from active sessions
   - [ ] Show tooltip: "Resume git workflow (3 minutes ago)"
5. [ ] Add configuration
   - [ ] `PSCUE_WORKFLOW_SESSION_TIMEOUT` (default: 30 minutes)
   - [ ] `PSCUE_WORKFLOW_MAX_SESSIONS` (default: 5)
6. [ ] Write tests (~10 test cases)
   - [ ] Session creation and tracking
   - [ ] Interruption detection
   - [ ] Timeout and expiration
7. [ ] Documentation updates

**Example Behavior**:
```powershell
10:00 PS> git add .
      [Session #1: Git workflow started]

10:01 PS> ls             # Interruption (unrelated command)
10:02 PS> cat README.md  # Another interruption

10:03 PS> git c█
          # "git commit" still suggested (session #1 remembered)
          # Tooltip: "Resume git workflow (3 minutes ago)"

10:35 PS> git c█
          # "git commit" no longer boosted (session expired after 30 min)
```

**Dependencies**: Phase 18.1 (workflow learning foundation)

**Success Criteria**:
- ✅ Sessions tracked correctly
- ✅ Interruptions don't break context
- ✅ Sessions expire appropriately
- ✅ All tests passing

---

### Phase 18.6: Error-Driven Workflow Adjustment
**Status**: Planned
**Priority**: Low
**Estimated Effort**: 30-35 hours

**Goal**: Suggest recovery actions after command failures.

**Current Limitation**: Failed commands don't trigger helpful suggestions. After `git push` fails with "non-fast-forward", PSCue doesn't suggest `git pull`.

**Solution**: Recognize error patterns and boost recovery command suggestions.

**Architecture**:
```csharp
// New class: src/PSCue.Module/ErrorRecoveryPatterns.cs (~300 lines)
public class ErrorRecoveryPatterns
{
    private static readonly Dictionary<string, List<RecoveryAction>> Patterns = new()
    {
        ["git push"] = new()
        {
            new RecoveryAction
            {
                ErrorPattern = "rejected.*non-fast-forward",
                SuggestedCommands = new[] { "git pull", "git fetch", "git pull --rebase" },
                Confidence = 0.9
            }
        },
        ["docker run"] = new()
        {
            new RecoveryAction
            {
                ErrorPattern = "port is already allocated",
                SuggestedCommands = new[] { "docker ps", "docker stop" },
                Confidence = 0.9
            }
        }
        // ... more patterns
    };

    public List<RecoverySuggestion> GetRecoverySuggestions(string command, string errorOutput);
}
```

**Tasks**:
1. [ ] Create `ErrorRecoveryPatterns.cs` (~300 lines)
   - [ ] Define error patterns for common tools (git, docker, cargo, npm)
   - [ ] Recovery suggestion logic with confidence scores
   - [ ] Regex-based error matching
2. [ ] Extend FeedbackProvider with error handling (~100 lines)
   - [ ] Hook into `OnCommandFailed` (if available)
   - [ ] Extract error output from context
   - [ ] Query ErrorRecoveryPatterns
3. [ ] Create error recovery cache (~50 lines)
   - [ ] Store recovery suggestions with 5-minute TTL
   - [ ] Clear on successful command execution
4. [ ] Integrate with GenericPredictor (~60 lines)
   - [ ] Boost recovery suggestions when available
   - [ ] Show tooltip: "Suggested recovery for failed push"
5. [ ] Add configuration
   - [ ] `PSCUE_ERROR_RECOVERY` (default: true)
   - [ ] `PSCUE_ERROR_CACHE_TTL` (default: 300 seconds)
6. [ ] Write tests for error patterns (~20 test cases)
   - [ ] Pattern matching accuracy
   - [ ] Recovery suggestion generation
   - [ ] Cache behavior
7. [ ] Documentation updates

**Example Behavior**:
```powershell
PS> git push
error: failed to push some refs
hint: Updates were rejected because the remote contains work
hint: that you do not have locally.

PS> git p█
    # Top suggestion: "git pull" (recovery, 0.95 confidence)
    # Tooltip: "Suggested recovery for failed push"
```

**Challenges**:
- **Error output availability**: Not all error output may be accessible via FeedbackProvider
- **API exploration needed**: May require alternative APIs for error capture

**Dependencies**: Requires PowerShell error output access (research needed)

**Success Criteria**:
- ✅ Error patterns detected
- ✅ Recovery suggestions shown
- ✅ Suggestions expire appropriately
- ✅ All tests passing

---

### Phase 18.7: Multi-Tool Workflows
**Status**: Planned
**Priority**: Low
**Estimated Effort**: 25-30 hours

**Goal**: Recognize workflows spanning multiple tools (editor → compiler → version control).

**Current Limitation**: PSCue focuses on single-tool workflows. Doesn't recognize cross-tool patterns like `vim → cargo build → cargo test → git add`.

**Solution**: Define workflow categories and recognize multi-tool command sequences.

**Architecture**:
```csharp
// New class: src/PSCue.Module/WorkflowPatternRecognizer.cs (~250 lines)
public class WorkflowPatternRecognizer
{
    public enum WorkflowCategory
    {
        Unknown, Development, Deployment, Debugging, VCS, Investigation
    }

    private static readonly Dictionary<WorkflowCategory, List<string[]>> Patterns = new()
    {
        [WorkflowCategory.Development] = new()
        {
            new[] { "vim", "cargo build", "cargo test" },
            new[] { "code", "npm run build", "npm test" },
            new[] { "vim", "dotnet build", "dotnet test" }
        },
        [WorkflowCategory.Deployment] = new()
        {
            new[] { "docker build", "docker tag", "docker push" },
            new[] { "npm run build", "npm publish" }
        }
        // ... more categories
    };

    public (WorkflowCategory Category, int StepIndex)? DetectWorkflow(List<CommandHistoryEntry> history);
    public string? GetNextStep(WorkflowCategory category, int stepIndex);
}
```

**Tasks**:
1. [ ] Create `WorkflowPatternRecognizer.cs` (~250 lines)
   - [ ] Define workflow categories (Development, Deployment, Debugging, VCS, Investigation)
   - [ ] Pattern matching logic
   - [ ] Next step prediction
2. [ ] Define common multi-tool workflow patterns
   - [ ] Development: edit → build → test
   - [ ] Deployment: build → package → push
   - [ ] VCS: add → commit → push
3. [ ] Integrate with WorkflowLearner (~100 lines)
   - [ ] Query pattern recognizer alongside dynamic learning
   - [ ] Boost next-step suggestions (1.5-1.6×)
4. [ ] Add workflow category tracking
   - [ ] Show workflow type in tooltips
5. [ ] Add configuration
   - [ ] `PSCUE_MULTITOOL_WORKFLOWS` (default: true)
6. [ ] Write tests for pattern matching (~15 test cases)
   - [ ] Multi-tool pattern detection
   - [ ] Workflow category recognition
   - [ ] Next step prediction accuracy
7. [ ] Documentation updates

**Example Behavior**:
```powershell
PS> vim src/main.rs<Enter>
    [Workflow detected: Development (edit phase)]

PS> cargo build<Enter>
    [Workflow detected: Development (build phase)]

PS> cargo t█
    # "cargo test" boosted 1.6× (next step in Development workflow)
    # Tooltip: "Next step in development workflow"

PS> cargo test<Enter>
PS> git a█
    # "git add" boosted 1.5× (transitioning to VCS workflow)
    # Tooltip: "Common next workflow after testing"
```

**Dependencies**: Phase 18.1 (workflow learning foundation)

**Success Criteria**:
- ✅ Multi-tool patterns recognized
- ✅ Cross-tool transitions suggested
- ✅ Workflow categories detected
- ✅ All tests passing

---

### Phase 18: Workflow Improvements Summary

**Total Estimated Effort (Phases 18.3-18.7)**: 170-210 hours

**Implementation Priority**:
1. ✅ **Phase 18.1**: Dynamic Workflow Learning (COMPLETE)
2. ✅ **Phase 18.2**: Time-Based Detection (COMPLETE - integrated with 18.1)
3. **Phase 18.3**: Workflow Chains (3+ commands) - **NEXT** (medium priority)
4. **Phase 18.4**: Project-Type Detection (medium-low priority, high user value)
5. **Phase 18.5**: Interruption Recovery (low priority, nice-to-have)
6. **Phase 18.6**: Error-Driven Adjustment (low priority, research needed)
7. **Phase 18.7**: Multi-Tool Workflows (low priority, polish)

**Configuration Summary** (All Phases):
```powershell
# Dynamic workflow learning (18.1, 18.2, 18.3)
$env:PSCUE_WORKFLOW_LEARNING = "true"
$env:PSCUE_WORKFLOW_MIN_FREQUENCY = "5"
$env:PSCUE_WORKFLOW_MAX_TIME_DELTA = "15"         # minutes
$env:PSCUE_WORKFLOW_MIN_CONFIDENCE = "0.6"
$env:PSCUE_WORKFLOW_NGRAM_ORDER = "3"             # 18.3: bigrams(2) or trigrams(3)

# Project-type detection (18.4)
$env:PSCUE_PROJECT_DETECTION = "true"
$env:PSCUE_PROJECT_CACHE_TTL = "300"              # seconds
$env:PSCUE_PROJECT_BOOST_FACTOR = "1.5"

# Workflow sessions (18.5)
$env:PSCUE_WORKFLOW_SESSION_TIMEOUT = "30"        # minutes
$env:PSCUE_WORKFLOW_MAX_SESSIONS = "5"

# Error recovery (18.6)
$env:PSCUE_ERROR_RECOVERY = "true"
$env:PSCUE_ERROR_CACHE_TTL = "300"                # seconds

# Multi-tool workflows (18.7)
$env:PSCUE_MULTITOOL_WORKFLOWS = "true"
```

**PowerShell Functions** (Phase 18.1 complete, others TBD):
```powershell
Get-PSCueWorkflows [-Command <string>] [-AsJson]
Get-PSCueWorkflowStats [-Detailed] [-AsJson]
Clear-PSCueWorkflows [-WhatIf] [-Confirm]
Export-PSCueWorkflows -Path <path>
Import-PSCueWorkflows -Path <path> [-Merge]
```

**Note**: Phase 18.2 (Time-Based Detection) was completed as part of Phase 18.1.

---

### Phase 18.8: Suggestion Source Telemetry & Analytics
**Status**: Planned
**Priority**: High (informs future development)
**Estimated Effort**: 10-15 hours

**Goal**: Track which suggestion sources users accept most often to guide future feature development.

**Why This Matters**: Know which features provide the most value (high acceptance rate) vs which to deprecate (low acceptance). Data-driven prioritization for Phases 18.3-18.7 and beyond.

**Current State**: PSCue already tracks executed commands and arguments for learning. This adds lightweight metadata about where suggestions came from.

**Architecture**:
```csharp
// Extend existing classes with source tracking
public enum SuggestionSource
{
    Unknown,
    KnownCompletion,        // Hardcoded git/gh/az/etc completions
    GenericLearning,        // Learned from ArgumentGraph (single-word)
    MultiWordLearning,      // Multi-word argument suggestions
    MLSequence,             // N-gram sequence predictor
    WorkflowLearning,       // Phase 18.1 workflow transitions
    PcdNavigation,          // PCD directory suggestions
    // Future phases:
    WorkflowChain,          // Phase 18.3 trigrams
    ProjectTypeBoost,       // Phase 18.4 influenced
    SessionResumption,      // Phase 18.5 session recovery
    ErrorRecovery,          // Phase 18.6 recovery suggestions
    MultiToolWorkflow       // Phase 18.7 cross-tool patterns
}

public class SuggestionMetrics
{
    public string Command { get; set; }
    public string Suggestion { get; set; }
    public SuggestionSource Source { get; set; }
    public DateTime Timestamp { get; set; }
    public bool WasAccepted { get; set; }
}
```

**Database Schema**:
```sql
CREATE TABLE suggestion_metrics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    command TEXT NOT NULL,
    suggestion TEXT NOT NULL,
    source TEXT NOT NULL,          -- 'KnownCompletion', 'GenericLearning', etc.
    timestamp TEXT NOT NULL,
    was_accepted INTEGER NOT NULL, -- 1 = accepted, 0 = ignored
    session_id TEXT                -- Optional: group by PowerShell session
);

CREATE INDEX idx_metrics_source ON suggestion_metrics(source, was_accepted);
CREATE INDEX idx_metrics_timestamp ON suggestion_metrics(timestamp);
```

**Tasks**:
1. [ ] Add `SuggestionSource` enum and tracking infrastructure (~50 lines)
2. [ ] Extend SQLite schema with `suggestion_metrics` table
3. [ ] Tag suggestions with source in CommandPredictor (~100 lines)
   - [ ] Known completions → `KnownCompletion`
   - [ ] GenericPredictor single-word → `GenericLearning`
   - [ ] GenericPredictor multi-word → `MultiWordLearning`
   - [ ] SequencePredictor → `MLSequence`
   - [ ] WorkflowLearner → `WorkflowLearning`
   - [ ] PCD suggestions → `PcdNavigation`
4. [ ] Hook FeedbackProvider to record metrics (~100 lines)
   - [ ] Track "suggestion shown" event
   - [ ] Track "suggestion accepted" event (command executed matches suggestion)
   - [ ] Handle both Tab completion and inline predictions
5. [ ] Add PowerShell functions (~200 lines)
   - [ ] `Get-PSCueSuggestionStats [-Days <int>] [-Source <source>] [-Command <name>] [-Detailed] [-AsJson]`
   - [ ] `Export-PSCueSuggestionMetrics -Path <path> [-Days <int>]`
   - [ ] `Clear-PSCueSuggestionMetrics [-Days <int>] [-WhatIf] [-Confirm]`
6. [ ] Add data retention policy
   - [ ] Auto-purge metrics older than 90 days (configurable)
   - [ ] Environment variable: `PSCUE_METRICS_RETENTION_DAYS` (default: 90)
7. [ ] Write tests (~12 test cases)
   - [ ] Source tagging accuracy
   - [ ] Acceptance tracking (Tab vs inline)
   - [ ] Stats aggregation correctness
   - [ ] Data retention/purging
8. [ ] Documentation updates
   - [ ] README: Add metrics section
   - [ ] CLAUDE.md: Add function reference

**Example Output**:
```powershell
PS> Get-PSCueSuggestionStats -Days 30

Source              Shown    Accepted    Acceptance Rate
------              -----    --------    ---------------
KnownCompletion     1,234    892         72.3%
GenericLearning     856      623         72.8%
WorkflowLearning    445      312         70.1%
MLSequence          267      134         50.2%  ← Low acceptance - deprioritize Phase 17.8
MultiWordLearning   189      145         76.7%
PcdNavigation       156      142         91.0%  ← Very high - promote in docs!

PS> Get-PSCueSuggestionStats -Command "git" -Detailed

Command: git
Source              Shown    Accepted    Top Suggestions (by acceptance)
------              -----    --------    ---------------------------
KnownCompletion     543      412         status (89%), commit (78%), push (71%)
GenericLearning     234      167         add (82%), pull (69%), checkout (65%)
WorkflowLearning    189      145         commit → push (87%), add → commit (84%)
```

**Benefits**:
1. **Data-driven prioritization**: Know which phases to implement next
2. **Feature validation**: Detect low-value features early
3. **Regression detection**: Acceptance rate drops indicate bugs/issues
4. **User insights**: Understand how different features are used
5. **Future personalization**: Auto-adjust weights based on user patterns

**Acceptance Detection Logic**:
- **Tab completion**: Easy - FeedbackProvider sees accepted completion
- **Inline predictions**: Check if executed command starts with shown suggestion
- **Multi-word suggestions**: Check if executed arguments match suggested sequence

**Configuration**:
```powershell
# Data retention (default: 90 days)
$env:PSCUE_METRICS_RETENTION_DAYS = "90"

# Disable metrics tracking entirely (if needed)
$env:PSCUE_METRICS_ENABLED = "true"  # Default: true
```

**Privacy Notes**:
- All data stored locally in SQLite database
- Same privacy guarantees as existing learning system
- Uses existing `PSCUE_IGNORE_PATTERNS` filtering
- User can view/export/delete metrics at any time
- Never uploaded anywhere

**Dependencies**: None (uses existing infrastructure)

**Success Criteria**:
- ✅ Suggestion sources tracked accurately
- ✅ Acceptance rates calculated correctly
- ✅ Stats functions provide actionable insights
- ✅ Data retention policy works
- ✅ All tests passing
- ✅ Performance impact negligible (<1ms per suggestion)

---

### Phase 19.3: Distribution & Packaging
**Status**: Backlog

- [ ] Copy AI model scripts to `ai/` directory
- [ ] Create Scoop manifest
- [ ] Publish to PowerShell Gallery
- [ ] Add Homebrew formula (macOS/Linux)
- [ ] Cloud sync (sync learned data across machines, opt-in)

### Distribution & Package Managers (Phase 19.3)
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
- **Architecture**: `docs/TECHNICAL_DETAILS.md`
- **AI Agent Guide**: `CLAUDE.md`
- **User Guide**: `README.md`
- **Database Functions**: `docs/DATABASE-FUNCTIONS.md`
- **Completed Phases**: `docs/COMPLETED.md`

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
- Move completed phases to docs/COMPLETED.md
- Move large architectural details to docs/TECHNICAL_DETAILS.md
