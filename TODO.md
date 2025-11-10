# PSCue - Task List

**Last Updated**: 2025-11-09

This document tracks active and planned work for PSCue. For architectural details, see [TECHNICAL_DETAILS.md](docs/TECHNICAL_DETAILS.md). For completed work, see [COMPLETED.md](docs/COMPLETED.md).

---

## Current Status

**Latest Release**: v0.3.0 (2025-11-09)
- ✅ Multi-word prediction suggestions (Phase 17.4)
- ✅ Dynamic workflow learning (Phase 18.1)
- ✅ Smart directory navigation with `pcd` command (Phases 17.5-17.7)
- ✅ Enhanced path handling and fuzzy matching
- ✅ Comprehensive test coverage (300+ tests)
- 🎉 Available for installation via one-line command

**Previous Release**: v0.2.0 (2025-11-05)
- ML-based N-gram sequence prediction (Phase 17.1)
- Privacy & security - sensitive data protection (Phase 17.2)
- Partial word completion filtering (Phase 17.3)
- Automated CI/CD with GitHub Actions

**Recent Improvements** (not yet released):
- None currently - all features shipped in v0.3.0

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

### Phase 17.8: Partial Command Predictions
**Status**: ✅ **COMPLETE** (2025-11-09)
**Priority**: Medium
**Estimated Effort**: 8-12 hours (Actual: ~2 hours)

**Goal**: Show inline predictions for partial commands (e.g., typing "g" suggests "git", "gh", "gt").

**Why This Matters**:
- Saves keystrokes for frequently-used commands
- Improves discoverability of available commands
- Complements existing argument/workflow predictions

**Current Behavior**: Inline predictions only show after command + space (e.g., "git "). Partial commands like "g" get no inline suggestions.

**Architecture**:
```csharp
// Extend src/PSCue.Module/CommandPredictor.cs (~100 lines)
public override SuggestionPackage GetSuggestion(
    PredictionClient client,
    PredictionContext context,
    CancellationToken cancellationToken)
{
    var commandLine = context.InputAst.Extent.Text;

    // NEW: If line contains no space, suggest full commands
    if (!commandLine.Contains(' '))
    {
        var partial = commandLine.Trim();
        if (string.IsNullOrEmpty(partial))
            return null; // Empty prompt = workflow predictions only

        return GetPartialCommandSuggestions(partial);
    }

    // Existing: Suggest arguments/next commands
    // ...
}

private SuggestionPackage GetPartialCommandSuggestions(string partial)
{
    // 1. Query ArgumentGraph for commands starting with partial
    // 2. Query WorkflowLearner for recently used commands
    // 3. Filter by StartsWith(partial, StringComparison.OrdinalIgnoreCase)
    // 4. Score by frequency + recency
    // 5. Return top 5 suggestions
}
```

**Data Sources**:
1. **Learned Commands**: Query `ArgumentGraph` for commands matching partial
   - Use existing frequency + recency scoring
2. **Workflow History**: Query `WorkflowLearner` for recently executed commands
   - Boost commands used in last 10 minutes (1.5× multiplier)
3. **Known Commands**: Fallback to PSCue's hardcoded command list (git, gh, az, etc.)
   - Lower priority (0.5× multiplier) - prefer learned commands

**Tasks**:
1. [ ] Add partial command detection to `CommandPredictor.GetSuggestion()` (~30 lines)
2. [ ] Implement `GetPartialCommandSuggestions()` method (~70 lines)
   - [ ] Query ArgumentGraph for matching commands
   - [ ] Query WorkflowLearner for recent commands
   - [ ] Apply filtering and scoring
   - [ ] Return top 5 suggestions
3. [ ] Add configuration environment variable
   - [ ] `PSCUE_PARTIAL_COMMAND_PREDICTIONS` (default: true)
4. [ ] Write tests (~10 test cases)
   - [ ] Single-character partial ("g" → "git", "gh")
   - [ ] Multi-character partial ("do" → "docker", "dotnet")
   - [ ] Case-insensitive matching ("GI" → "git")
   - [ ] Scoring accuracy (frequency + recency)
   - [ ] Empty prompt (no suggestions)
   - [ ] Workflow boost for recent commands
5. [ ] Documentation updates
   - [ ] README: Add partial command section
   - [ ] CLAUDE.md: Update feature list

**Example Behavior**:
```powershell
# User types "g" (no space, no Tab)
PS> g█
    # Inline predictions:
    # → git     (most frequent)
    # → gh      (second most frequent)
    # → gt      (least frequent)

# User types "doc" (no space, no Tab)
PS> doc█
    # → docker  (exact prefix match)

# User types "GI" (case-insensitive)
PS> GI█
    # → git    (case-insensitive match)

# Empty prompt - no command suggestions (only workflow predictions)
PS> █
    # → git status  (workflow prediction, not command prediction)
```

**Performance Targets**:
- Query time: <5ms (same as existing inline predictions)
- No noticeable typing lag
- Efficient filtering with StartsWith() + case-insensitive comparison

**Edge Cases**:
- Empty prompt: No command suggestions (reserve for workflow predictions only)
- Single space: No command suggestions (move to argument suggestions)
- Very long partial (10+ chars): Likely not a command, no suggestions
- No matching commands: Return empty (no fallback noise)

**Configuration**:
```powershell
# Enable/disable partial command predictions (default: true)
$env:PSCUE_PARTIAL_COMMAND_PREDICTIONS = "true"
```

**Dependencies**: None (uses existing ArgumentGraph + WorkflowLearner)

**Success Criteria**:
- ✅ Partial commands show relevant suggestions
- ✅ Scoring prioritizes frequent/recent commands
- ✅ Performance meets <5ms target
- ✅ No interference with existing workflow predictions
- ✅ All tests passing
- ✅ User can disable feature via environment variable

**Follow-up Questions** (to test after implementation):
- Is this helpful or noisy? (May need user feedback to decide)
- Should we show command suggestions at empty prompt? (Currently reserved for workflows)
- Should we limit to commands with high confidence? (Avoid suggesting rarely-used commands)

**Completed**:
- [x] Added `GetPartialCommandSuggestions()` method to CommandPredictor
- [x] Integrated with existing suggestion pipeline (merges with genericSuggestions)
- [x] Environment variable configuration: `PSCUE_PARTIAL_COMMAND_PREDICTIONS` (default: true)
- [x] Scoring algorithm: 60% frequency + 40% recency, with 1.5× boost for recently used commands
- [x] Filters: Very long partials (>10 chars) ignored, case-insensitive matching
- [x] Comprehensive unit tests (7 test cases covering core functionality)
- [x] Performance: <5ms target met (reuses existing ArgumentGraph queries)

**Files Modified**:
- `src/PSCue.Module/CommandPredictor.cs` (~100 lines added)
  - `GetPartialCommandSuggestions()` method
  - `FormatLastUsed()` helper method
  - Integration into GetSuggestion() workflow
- `test/PSCue.Module.Tests/CommandPredictorTests.cs` (7 new tests)

**Key Achievements**:
- Frequency-based command suggestions from learned data
- Works without workflow context (fresh sessions, unrelated commands)
- Complements existing workflow predictions
- User-friendly tooltips show usage stats ("Used X times, last: Xm ago")
- Can be disabled via environment variable

---

### Phase 17.9: Tab Completion on Empty Input
**Status**: Planned
**Priority**: Medium-Low
**Estimated Effort**: 8-12 hours

**Goal**: Show tab completions when user presses `<Tab>` on empty command line, displaying frequent/recent commands from PSCue's learned data.

**Current Limitation**:
- PowerShell's `CompleteInput()` explicitly returns empty results for empty input (by design)
- `Register-ArgumentCompleter` cannot intercept Tab on empty command line
- `ICommandPredictor` does NOT show predictions until user begins typing

**Solution**: Override `TabExpansion2` to provide custom completions for empty input, then delegate to standard completion for non-empty input. PSReadLine automatically handles display based on user's `PredictionViewStyle` setting.

**Documentation**: See [docs/TAB_COMPLETION_EMPTY_INPUT.md](docs/TAB_COMPLETION_EMPTY_INPUT.md) for detailed research, implementation approaches, and code snippets.

**Tasks**:
1. [ ] Implement TabExpansion2 override in module initialization (~80 lines)
   - [ ] Detect empty input and return `CompletionResult` objects
   - [ ] Query `ArgumentGraph.GetTrackedCommands()` for frequent commands
   - [ ] Create CompletionResult objects with tooltips
   - [ ] Delegate to original TabExpansion2 or CompleteInput for non-empty input
2. [ ] Add environment variable configuration
   - [ ] `PSCUE_EMPTY_TAB_COMPLETION` (default: false, opt-in)
   - [ ] `PSCUE_EMPTY_TAB_MAX_SUGGESTIONS` (default: 10)
   - [ ] `PSCUE_EMPTY_TAB_MIN_USAGE` (default: 3)
3. [ ] Fix `ArgumentGraph.GetAllCommands()` enumeration issue
   - [ ] Currently returns empty keys/null values in PowerShell
   - [ ] Needed for sorting by usage count
4. [ ] Enhance suggestions with usage data (~50 lines)
   - [ ] Sort commands by `TotalUsageCount` descending
   - [ ] Add tooltips with usage count and last used date
   - [ ] Filter out rarely-used commands (min usage threshold)
5. [ ] Optional: Combine with workflow predictions
   - [ ] Boost workflow-predicted commands in the list
   - [ ] Show different tooltip for workflow vs frequency suggestions
6. [ ] Write tests (~8 test cases)
   - [ ] Empty input returns completions
   - [ ] Non-empty input delegates to standard completion
   - [ ] Respects max suggestions limit
   - [ ] Filters by min usage threshold
   - [ ] Conflict detection with existing TabExpansion2
7. [ ] Documentation updates
   - [ ] README: Add empty tab completion section
   - [ ] CLAUDE.md: Update configuration reference

**Example Behavior**:
```powershell
# User presses <Tab> on empty line
PS> █  # Press Tab
    # Shows completions based on PredictionViewStyle:
    # - MenuComplete: Arrow keys to select from list
    # - InlineView: Right arrow to accept, Tab to cycle
    # - ListView: F2 to show list view

    # Suggestions (sorted by usage):
    # git        (Used 245 times, last: 2m ago)
    # dotnet     (Used 189 times, last: 5m ago)
    # code       (Used 156 times, last: 10m ago)
    # ...
```

**Risks**:
- Potential conflicts with other modules that override TabExpansion2 (last one wins)
- Must handle both parameter sets (ScriptInput and AstInput)
- Not officially supported (but commonly used pattern)

**Dependencies**: None (uses existing ArgumentGraph)

**Success Criteria**:
- ✅ Tab on empty input shows frequent commands
- ✅ Commands sorted by usage count
- ✅ Respects user's PSReadLine settings (InlineView/ListView/MenuComplete)
- ✅ Non-empty input works normally (delegates to standard completion)
- ✅ Can be disabled via environment variable
- ✅ No performance impact (<50ms)
- ✅ All tests passing

---

### Phase 17.10: Advanced ML (Future Enhancement)
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

**See**: [COMPLETED.md](docs/COMPLETED.md) for detailed implementation notes

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

**Total Estimated Effort**: 170-210 hours

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
