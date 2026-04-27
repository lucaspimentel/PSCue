# PSCue - Task List

This document tracks planned work for PSCue. For architectural details, see [TECHNICAL_DETAILS.md](docs/TECHNICAL_DETAILS.md). For completed work, see [COMPLETED.md](docs/COMPLETED.md).

---

## Planned Work

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

### CWD-Scoped Path Argument Suggestions
**Status**: Backlog

**Goal**: Detect path-like arguments in learned CLI commands and scope their suggestions to the working directory where they were recorded.

**Current Limitation**: `ArgumentGraph.RecordUsage()` (`src/PSCue.Module/ArgumentGraph.cs:273`) only uses `workingDirectory` for navigation command normalization (cd/sl/etc). For other commands (e.g., `dotnet build`), path arguments like `tracer\src\Datadog.Trace\Datadog.Trace.csproj` are stored and suggested globally — meaning they appear as suggestions even when in an unrelated directory.

**Solution**:
- Detect path-like arguments (contains `\`, `/`, `.csproj`, `.sln`, etc.) during `RecordUsage`
- Tag them with the CWD where they were recorded
- In `GetSuggestions()` (`src/PSCue.Module/ArgumentGraph.cs:638`), filter or de-prioritize path arguments that don't match the current CWD

- [ ] Decide on detection heuristic: path separators, known file extensions, rooted vs relative
- [ ] Store `workingDirectory` per `ArgumentStats` (or a separate path-scoped index)
- [ ] Update `GetSuggestions()` to accept optional `currentDirectory` and filter path args by origin CWD
- [ ] Update `FeedbackProvider` and `CommandPredictor` to pass current directory when querying suggestions
- [ ] Write tests for path detection, scoped suggestion filtering, and non-path args unaffected

---

### Case-Sensitive Flag Combinations
**Status**: Backlog

**Problem**: `FlagCombinations` dicts in `ArgumentGraph` key joined flag strings (e.g. `"-a -b"`) case-insensitively. Flags that differ only by case get conflated. On Windows this is unnoticed because no commonly-shipped Windows tool relies on case-distinct short flags, but on Linux many tools do (e.g. `git -S` vs `-s`, `grep -V` vs `-v`). Predates the Linux-support PR; deferred from that PR to keep its scope tight.

**Fix**: Switch the `FlagCombinations` dict comparer to `PathComparer.Equality` (introduced by the Linux-support PR — `OrdinalIgnoreCase` on Windows, `Ordinal` on Linux). Touches `src/PSCue.Module/ArgumentGraph.cs:188` (`CommandKnowledge.FlagCombinations`) and `src/PSCue.Module/ArgumentGraph.cs:247` (the matching baseline dict). Consider also switching the `flag_combinations` SQLite column from `COLLATE NOCASE` to `BINARY` on Linux to match.

- [ ] Flip `FlagCombinations` dict comparers to `PathComparer.Equality`
- [ ] Decide whether to also flip the SQLite `flag_combinations.flags` collation on Linux (matches the path-column treatment)
- [ ] Add a regression test: `git -S` and `git -s` are tracked as distinct flag combinations on Linux

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
1. **Phase 18.3**: Workflow Chains (3+ commands) - medium priority
2. **Phase 18.4**: Project-Type Detection - medium-low priority, high user value
3. **Phase 18.5**: Interruption Recovery - low priority
4. **Phase 18.6**: Error-Driven Adjustment - low priority, research needed
5. **Phase 18.7**: Multi-Tool Workflows - low priority
6. **Phase 18.8**: Suggestion Source Telemetry - high priority (informs development)

**Configuration Summary** (All Phases):
```powershell
# Workflow chains (18.3)
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

**PowerShell Functions** (for future phases):
```powershell
# Existing (from Phase 18.1 - see COMPLETED.md):
Get-PSCueWorkflows, Get-PSCueWorkflowStats, Clear-PSCueWorkflows
Export-PSCueWorkflows, Import-PSCueWorkflows

# Planned (Phase 18.8):
Get-PSCueSuggestionStats, Export-PSCueSuggestionMetrics, Clear-PSCueSuggestionMetrics
```

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

### Module Load Time Optimization
**Status**: In Progress (background loading landed; cold-start optimizations remain)
**Priority**: Medium (warm import is now ~200ms; cold import still 2-3s)

**Context**: Background loading landed in commit `2e9691b`. `OnImport` (`src/PSCue.Module/ModuleInitializer.cs:34`) now only registers subsystems synchronously (at `ModuleInitializer.cs:196` and `:216`) and spawns a `Task.Run(InitializeInBackground)` (`:58`) that handles all database I/O off the critical path. `CommandPredictor` and `FeedbackProvider` null-check `PSCueModule.*` statics and return empty results until the background task completes.

The remaining work is (a) reducing the time the background task spends in SQLite during load, since it still blocks the first few predictions, and (b) attacking the pre-`OnImport` cold-start gap described further below.

#### Database I/O optimizations (reduces total background load time)

- [x] **Use a single connection for all load operations** -- `PersistenceManager.CreateSharedConnection()` now exposes a connection the caller can reuse, and the five Load methods take it as a required `SqliteConnection` parameter. `ModuleInitializer.InitializeInBackground` opens one connection and threads it through `LoadArgumentGraph` / `LoadCommandHistory` / `LoadBookmarks` / `LoadCommandSequences` / `LoadWorkflowTransitions`. This cuts 5 redundant open + PRAGMA busy_timeout cycles on the init critical path. Saves are unchanged and continue to open per-call connections to preserve the per-operation thread-safety invariant.
- [x] **Combine ArgumentGraph queries into fewer round-trips** -- `LoadArgumentGraph` now issues a single batched `CommandText` containing all seven SELECTs, iterated via `SqliteDataReader.NextResult()`. Collapses 7 `CreateCommand`/`ExecuteReader`/`Dispose` cycles into one, while preserving the per-table row-processing loops and `Initialize*` call order (important for baseline/delta correctness -- CLAUDE.md pitfall #6). New test `LoadArgumentGraph_RoundTripsAllSevenTables` exercises every table (commands/arguments/co_occurrences/flag_combinations/argument_sequences/parameters/parameter_values) via `RecordUsage` + `RecordParsedUsage`. All 541 tests pass.
- [x] **Skip schema creation on existing databases** -- `PersistenceManager.InitializeDatabase()` now reads `PRAGMA user_version` after enabling WAL and returns early when the schema already matches `CurrentSchemaVersion` (= 1). Fresh databases run the full CREATE TABLE / CREATE INDEX block as before and then set `user_version = 1`. Pre-versioning databases (user_version = 0) upgrade transparently -- the `CREATE ... IF NOT EXISTS` clauses remain as a safety net, so existing tables are left untouched and the version is bumped to 1 at the end. Three new tests in `PersistenceManagerTests.cs` cover fresh-DB version stamping, data preservation across reopen, and the pre-versioning upgrade path.
- [ ] **Replace `DateTime.Parse()` with integer timestamps** -- Every row calls `DateTime.Parse(reader.GetString(N)).ToUniversalTime()` -- ~12,000+ parse calls during load. Storing as Unix epoch integers (`reader.GetInt64`) would be much faster.
  - **Impact**: Medium (significant CPU reduction during deserialization)
  - **Complexity**: High (schema migration, backwards compatibility, update all read/write paths)

#### Cold-start load time (identified after 2e9691b moved data loading to background)

**Context**: After background init landed, *warm* import measured ~78ms via the `PSCUE_DEBUG` phase timers (`IMPORT [phase]` lines in `%LOCALAPPDATA%\PSCue\log.txt`). But cold morning imports still take ~5.5s. The 5.4s delta happens *before* `ModuleInitializer`'s static ctor runs — invisible to our instrumentation. Almost certainly cold-disk DLL I/O, Windows Defender real-time scans on first touch, and JIT compilation. See commit `0c6ae97` for the instrumentation and the discussion that preceded it.

- [x] **ReadyToRun-compile `PSCue.Module.dll` and `PSCue.Shared.dll`** -- Added `<PublishReadyToRun>true</PublishReadyToRun>` to `src/PSCue.Module/PSCue.Module.csproj` (cascades R2R to PSCue.Shared and the SQLite chain during publish). Updated `.github/workflows/release.yml` and `install-local.ps1` to pass `-r <RID>` on Module publish (required for R2R). Removed the now-redundant "Flatten native SQLite library" step since `-r` already flattens the output. Verified: managed DLLs grew ~2.5x (PSCue.Module.dll 182→446 KB), all tests pass, module imports successfully.
- [ ] **Add Windows Defender exclusion guidance to README/install script** -- Cold DLL loads trigger Defender real-time scans; exclusion of the install path (`$HOME\.local\pwsh-modules\PSCue` or `$env:USERPROFILE\Documents\PowerShell\Modules\PSCue`) can shave seconds off first-run load. Document the `Add-MpPreference -ExclusionPath` command in README; optionally prompt in `install-local.ps1`.
  - **Impact**: High on cold-boot (Defender scan-on-access is often the dominant cost)
  - **Complexity**: Low (docs + optional installer prompt). Admin required for the cmdlet.
- [ ] **Audit and reduce runtime dependency count** -- SQLitePCLRaw pulls multiple DLLs (`SQLitePCLRaw.core`, `.provider.e_sqlite3`, `.batteries_v2`, native `e_sqlite3.dll`). Each DLL is an independent cold-disk + Defender-scan hit. Check if any can be dropped, consolidated, or lazy-loaded — e.g., defer SQLite DLL resolution until the background init actually touches the database.
  - **Impact**: Medium (each eliminated DLL saves cold-disk + scan time)
  - **Complexity**: Medium (may require reflection-based lazy load or wrapping SQLite init)
- [x] **Skip or cache `Get-PSReadLineOption` after first session** -- `module/PSCue.psm1` now gates the PSReadLine check behind a `prediction-source-ok` marker file in the PSCue data directory. Once a good prediction source (`Plugin`/`HistoryAndPlugin`) is observed, the marker is written and subsequent imports skip the ~46ms `Get-PSReadLineOption` call. Re-check cadence defaults to 7 days, configurable via `PSCUE_PREDICTION_SOURCE_CHECK_DAYS`. When the source is still bad, no marker is written and the hint continues to show until the user fixes it. `Clear-PSCueLearning -Force` already wipes the data dir, so it resets the marker naturally.
- [x] **Consolidate the five dot-sourced `Functions/*.ps1` files into one** -- Merged into `module/Functions.ps1` with `#region` banners (Learning / Database / Workflow / Smart Navigation (pcd) / Debugging & Diagnostics). `module/PSCue.psm1` now dot-sources a single file. `module/Functions/` directory deleted. `install-local.ps1` and `.github/workflows/release.yml` updated to copy the single file instead of the directory. Warm-cache sample shows 30ms for the single `Dotsource-Functions` phase vs ~39ms combined across the old five phases; cold-disk savings will be larger.

---

### Phase 19.3: Distribution & Packaging
**Status**: Backlog

- [ ] Copy AI model scripts to `ai/` directory
- [x] Create Scoop manifest (via lucaspimentel/scoop-bucket)
- [ ] Publish to PowerShell Gallery
- [ ] Add Homebrew formula (macOS/Linux)
- [ ] Cloud sync (sync learned data across machines, opt-in)

### Distribution & Package Managers (Phase 19.3)
**Status**: In Progress

**Next Steps**:
- [ ] Test v0.2.0 installation on fresh Windows and Linux systems
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

## Notes

- This is a living document - update as tasks progress
- Check off items as completed
- Add new items as discovered
- Move completed phases to docs/COMPLETED.md
- Move large architectural details to docs/TECHNICAL_DETAILS.md
