# Workflow Detection Improvements for PSCue

**Last Updated**: 2025-11-07

**Status**: Phase 18.1 Implementation In Progress (~75% complete)

This document outlines the design for enhancing PSCue's workflow detection capabilities to provide smarter, more context-aware command suggestions based on user behavior patterns.

## Table of Contents

- [Overview](#overview)
- [Current State Analysis](#current-state-analysis)
- [Proposed Improvements](#proposed-improvements)
  - [1. Dynamic Workflow Learning](#1-dynamic-workflow-learning)
  - [2. Project-Type Detection](#2-project-type-detection)
  - [3. Time-Based Workflow Detection](#3-time-based-workflow-detection)
  - [4. Workflow Chains (3+ Commands)](#4-workflow-chains-3-commands)
  - [5. Workflow Interruption Recovery](#5-workflow-interruption-recovery)
  - [6. Error-Driven Workflow Adjustment](#6-error-driven-workflow-adjustment)
  - [7. Multi-Tool Workflows](#7-multi-tool-workflows)
- [Implementation Roadmap](#implementation-roadmap)
- [Technical Architecture](#technical-architecture)
- [Testing Strategy](#testing-strategy)
- [Configuration](#configuration)
- [Success Metrics](#success-metrics)
- [Open Questions](#open-questions)

---

## Overview

**Goal**: Transform PSCue from recognizing static, hard-coded command patterns to dynamically learning and predicting workflows based on actual user behavior.

**Why This Matters**:
- Users have unique workflows that PSCue should adapt to
- Real workflows span multiple commands and tools
- Context matters: the same command means different things in different situations
- Current static patterns only cover ~20 common scenarios

**Expected Benefits**:
- **Personalization**: Learn each user's unique command patterns
- **Better Predictions**: Suggest next command based on actual workflow history
- **Reduced Typing**: Anticipate multi-step operations (build → test → commit)
- **Context Awareness**: Project-type and time-based suggestions

---

## Current State Analysis

### What Works Today (`ContextAnalyzer.cs`)

**1. Static Pattern Recognition** (Lines 46-85)
- Hard-coded sequences for ~20 common workflows
- Examples:
  - Git: `git add` → `git commit` → `git push`
  - Docker: `docker build` → `docker run` → `docker ps`
  - Build tools: `npm install` → `npm start` → `npm test`

**2. Simple Context Boosts** (Lines 176-206)
- Recently-used arguments: 1.2× boost
- Recently-used flags: 1.15× boost
- Applied within last 3 commands

**3. Workflow-Specific Boosts** (Lines 211-262)
- Git workflow: After `git add`, boost `-m` flag (1.5×) and `commit` subcommand (1.4×)
- Docker workflow: After `docker build`, boost `run`, `-d`, `-p` flags
- Build tools: After any `build`, boost `test` and `run`

### Current Limitations

| Problem | Impact | Example |
|---------|--------|---------|
| **Static patterns only** | Can't learn user-specific workflows | User always does `cargo check` → `cargo clippy` → `cargo test` but PSCue doesn't learn this |
| **Limited to 2-command sequences** | Misses longer workflows | After `git add` → `git commit`, PSCue doesn't strongly predict `git push` |
| **No project-type awareness** | Irrelevant suggestions | Suggests `npm` commands in Rust projects, `cargo` in Node projects |
| **No time consideration** | Unrelated commands treated equally | `git commit` (5 minutes ago) vs `npm install` (yesterday) both boost equally |
| **Simple boost multipliers** | Not data-driven | All boosts are hard-coded (1.2×, 1.5×, etc.) instead of learned from frequency |
| **No error awareness** | Doesn't suggest recovery commands | After failed `git push`, doesn't boost `git pull` or `git fetch` |
| **Single-tool focus** | Real workflows span tools | Edit → test → commit workflow not recognized across vim/cargo/git |

### Usage Data

From Phase 17.1 (ML Predictions), we have infrastructure for:
- ✅ Command sequence tracking via SequencePredictor
- ✅ SQLite persistence for cross-session learning
- ✅ N-gram based prediction (bigrams/trigrams)
- ✅ FeedbackProvider integration for automatic learning

**Opportunity**: Extend this infrastructure to track workflow patterns, not just command sequences.

---

## Proposed Improvements

### 1. Dynamic Workflow Learning

**Problem**: Only recognizes hard-coded patterns. Doesn't learn from actual user behavior.

**Solution**: Automatically learn workflow patterns from command history.

#### Architecture

Extend `SequencePredictor.cs` to track command-level workflows in addition to argument sequences.

```csharp
// New class in src/PSCue.Module/WorkflowLearner.cs
public class WorkflowLearner
{
    // Tracks command → next command relationships with frequency
    private ConcurrentDictionary<string, WorkflowNode> _workflowGraph = new();

    public class WorkflowNode
    {
        public string Command { get; set; }
        public Dictionary<string, WorkflowTransition> NextCommands { get; set; }
    }

    public class WorkflowTransition
    {
        public string NextCommand { get; set; }
        public int Frequency { get; set; }           // How often this transition occurs
        public double AverageTimeDelta { get; set; } // Average time between commands
        public DateTime LastSeen { get; set; }
        public double Probability => /* Calculate from frequency */
    }

    // Called by FeedbackProvider after each successful command
    public void RecordTransition(string fromCommand, string toCommand, TimeSpan timeDelta)
    {
        // Update workflow graph with new transition
        // Track frequency and timing information
    }

    // Called by GenericPredictor during suggestion generation
    public List<WorkflowSuggestion> GetNextCommandPredictions(
        string currentCommand,
        List<CommandHistoryEntry> recentCommands,
        int maxResults = 5)
    {
        // Look up current command in workflow graph
        // Return top N next commands by probability
        // Filter by time proximity (see Improvement #3)
    }
}
```

#### Database Schema

Extend SQLite database with workflow graph table:

```sql
CREATE TABLE workflow_transitions (
    from_command TEXT NOT NULL,
    to_command TEXT NOT NULL,
    frequency INTEGER NOT NULL DEFAULT 1,
    total_time_delta_ms INTEGER NOT NULL DEFAULT 0, -- Sum for averaging
    last_seen TEXT NOT NULL,
    PRIMARY KEY (from_command, to_command)
);

CREATE INDEX idx_workflow_from ON workflow_transitions(from_command);
```

#### Integration Points

1. **FeedbackProvider** (src/PSCue.Module/FeedbackProvider.cs:50-100)
   - After successful command, record transition from previous command
   - Pass time delta between commands

2. **GenericPredictor** (src/PSCue.Module/GenericPredictor.cs:66-377)
   - Query WorkflowLearner for next command predictions
   - Blend with existing argument suggestions
   - Apply confidence scores based on transition frequency

3. **CommandPredictor** (src/PSCue.Module/CommandPredictor.cs:43-82)
   - Surface workflow predictions as inline suggestions
   - Show confidence indicator for learned workflows

#### Example Behavior

```powershell
# User's typical workflow (happens 10+ times):
cargo check
cargo clippy
cargo test
git add .
git commit -m "fix"

# After learning, PSCue predicts:
PS> cargo check<Enter>
    ↓ (PSCue learns transition)
PS> cargo c█
    # Inline suggestion: "cargo clippy" (based on workflow, 85% confidence)

PS> cargo clippy<Enter>
    ↓
PS> cargo t█
    # Inline suggestion: "cargo test" (based on workflow, 90% confidence)

PS> cargo test<Enter>
    ↓
PS> git a█
    # Inline suggestion: "git add ." (cross-tool workflow, 70% confidence)
```

#### Configuration

```powershell
# Enable/disable dynamic workflow learning
$env:PSCUE_WORKFLOW_LEARNING = "true"

# Minimum occurrences before suggesting workflow
$env:PSCUE_WORKFLOW_MIN_FREQUENCY = "5"

# Maximum time between commands to consider them related (minutes)
$env:PSCUE_WORKFLOW_MAX_TIME_DELTA = "15"

# Confidence threshold for showing workflow suggestions
$env:PSCUE_WORKFLOW_MIN_CONFIDENCE = "0.6"
```

#### Performance Targets

- Workflow graph lookup: <2ms (cached in-memory)
- Database persistence: Async, no blocking
- Memory: ~50 bytes per transition, max 1000 transitions (~50KB)

---

### 2. Project-Type Detection

**Problem**: PSCue suggests irrelevant commands (e.g., `npm` in Rust projects, `cargo` in Node projects).

**Solution**: Detect project type from directory contents and adjust suggestion priorities.

#### Architecture

```csharp
// New class in src/PSCue.Module/ProjectTypeDetector.cs
public class ProjectTypeDetector
{
    // Cache detection results per directory (TTL: 5 minutes)
    private ConcurrentDictionary<string, ProjectContext> _projectCache = new();

    public enum ProjectType
    {
        Unknown,
        Rust,           // Cargo.toml
        Node,           // package.json
        DotNet,         // *.csproj, *.sln
        Python,         // requirements.txt, setup.py, pyproject.toml
        Go,             // go.mod
        Docker,         // Dockerfile
        Kubernetes,     // *.yaml with apiVersion
        Git             // .git directory
    }

    public class ProjectContext
    {
        public List<ProjectType> DetectedTypes { get; set; }  // Can have multiple
        public string RootDirectory { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    public ProjectContext DetectProjectType(string currentDirectory)
    {
        // Check cache first (5-minute TTL)
        if (_projectCache.TryGetValue(currentDirectory, out var cached))
        {
            if (DateTime.UtcNow - cached.DetectedAt < TimeSpan.FromMinutes(5))
                return cached;
        }

        var context = new ProjectContext { DetectedTypes = new() };

        // Walk up directory tree looking for indicator files
        var dir = currentDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Cargo.toml")))
                context.DetectedTypes.Add(ProjectType.Rust);

            if (File.Exists(Path.Combine(dir, "package.json")))
                context.DetectedTypes.Add(ProjectType.Node);

            if (Directory.Exists(Path.Combine(dir, ".git")))
                context.DetectedTypes.Add(ProjectType.Git);

            // ... check other indicators

            if (context.DetectedTypes.Any())
            {
                context.RootDirectory = dir;
                break;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        context.DetectedAt = DateTime.UtcNow;
        _projectCache[currentDirectory] = context;
        return context;
    }
}
```

#### Scoring Adjustments

Modify `GenericPredictor.cs` to adjust scores based on project type:

```csharp
private double ApplyProjectTypeBoost(
    string command,
    double baseScore,
    ProjectContext projectContext)
{
    // Boost relevant commands for detected project types
    var boostMap = new Dictionary<ProjectType, Dictionary<string, double>>
    {
        [ProjectType.Rust] = new()
        {
            ["cargo"] = 1.5,
            ["rustc"] = 1.3,
            ["rust-analyzer"] = 1.2,
            // De-prioritize unrelated tools
            ["npm"] = 0.5,
            ["dotnet"] = 0.5
        },
        [ProjectType.Node] = new()
        {
            ["npm"] = 1.5,
            ["node"] = 1.4,
            ["yarn"] = 1.3,
            ["pnpm"] = 1.3,
            // De-prioritize unrelated tools
            ["cargo"] = 0.5,
            ["dotnet"] = 0.5
        },
        // ... more project types
    };

    foreach (var projectType in projectContext.DetectedTypes)
    {
        if (boostMap.TryGetValue(projectType, out var boosts))
        {
            foreach (var (pattern, boost) in boosts)
            {
                if (command.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    baseScore *= boost;
                    break;
                }
            }
        }
    }

    return baseScore;
}
```

#### Example Behavior

```powershell
# In a Rust project (Cargo.toml present)
PS> ca█
    # Suggestions:
    # - cargo build (score: 0.95)  ← boosted 1.5×
    # - cargo test  (score: 0.90)  ← boosted 1.5×
    # - cat         (score: 0.45)  ← no boost

# In a Node project (package.json present)
PS> ca█
    # Suggestions:
    # - cat         (score: 0.60)  ← no boost
    # - cargo build (score: 0.30)  ← de-prioritized 0.5×
```

#### Performance Considerations

- File system checks cached (5-minute TTL)
- Only walk up tree on cache miss (~10-20ms)
- Negligible impact on hot path (<1ms cache hit)

---

### 3. Time-Based Workflow Detection

**Problem**: Commands from days ago treated the same as commands from 5 minutes ago.

**Solution**: Weight workflow suggestions by time proximity.

#### Time-Based Scoring

```csharp
// Extend WorkflowTransition class
public class WorkflowTransition
{
    public string NextCommand { get; set; }
    public int Frequency { get; set; }
    public double AverageTimeDelta { get; set; }  // In seconds
    public DateTime LastSeen { get; set; }

    // Calculate time-adjusted score
    public double GetTimeSensitiveScore(TimeSpan timeSinceLastCommand)
    {
        double baseScore = Frequency / 100.0; // Normalize to 0-1 range

        // Time proximity boost based on typical transition timing
        double timeBoost = CalculateTimeBoost(timeSinceLastCommand, AverageTimeDelta);

        return baseScore * timeBoost;
    }

    private double CalculateTimeBoost(TimeSpan actualDelta, double avgDelta)
    {
        // Commands that typically follow quickly get lower boost if delayed
        // Commands that typically follow slowly aren't penalized as much

        double ratio = actualDelta.TotalSeconds / avgDelta;

        if (ratio < 1.5)        // Within expected timeframe
            return 1.5;
        else if (ratio < 5)     // Moderately delayed
            return 1.2;
        else if (ratio < 30)    // Significantly delayed
            return 1.0;
        else                     // Very old, weak relationship
            return 0.8;
    }
}
```

#### Time Buckets

Define relationship strength based on time proximity:

| Time Delta | Relationship | Boost | Example |
|-----------|-------------|-------|---------|
| < 30 seconds | **Immediate** | 1.8× | `git add` → `git commit` |
| 30s - 5 minutes | **Strong** | 1.5× | `cargo build` → `cargo test` |
| 5 - 30 minutes | **Moderate** | 1.2× | `docker build` → `docker run` |
| 30m - 2 hours | **Weak** | 1.0× | `git clone` → `cd` (after reading docs) |
| > 2 hours | **Unrelated** | 0.8× | Commands likely unrelated |

#### Example Behavior

```powershell
# Scenario 1: Quick succession (typical workflow)
10:00:00 PS> git add .
10:00:15 PS> git c█
             # "git commit" boosted 1.8× (immediate follow-up)

# Scenario 2: Delayed (interrupted workflow)
10:00:00 PS> git add .
10:30:00 PS> git c█
             # "git commit" boosted 1.2× (moderate delay, still relevant)

# Scenario 3: Very old (unrelated)
10:00:00 PS> git add .
14:00:00 PS> git c█
             # "git commit" boosted 0.8× (too old, weak relationship)
```

#### Recording Time Deltas

Modify `FeedbackProvider.cs` to track timing:

```csharp
private DateTime? _lastCommandTime = null;

protected override void OnCommandExecuted(FeedbackContext context)
{
    var currentTime = DateTime.UtcNow;
    TimeSpan? timeDelta = null;

    if (_lastCommandTime.HasValue)
    {
        timeDelta = currentTime - _lastCommandTime.Value;
    }

    // Record workflow transition with timing
    var currentCommand = context.CommandLine;
    var previousCommand = PSCueModule.CommandHistory.GetRecent(1).FirstOrDefault();

    if (previousCommand != null && timeDelta.HasValue)
    {
        PSCueModule.WorkflowLearner.RecordTransition(
            previousCommand.Command,
            currentCommand,
            timeDelta.Value
        );
    }

    _lastCommandTime = currentTime;
}
```

---

### 4. Workflow Chains (3+ Commands)

**Problem**: Only considers last command. Misses longer workflow patterns.

**Solution**: Track and predict based on last 2-3 commands (trigrams/4-grams).

#### N-gram Extension

Extend `SequencePredictor.cs` to support variable-order n-grams:

```csharp
public class WorkflowLearner
{
    // Support bigrams (A→B) and trigrams (A→B→C)
    private ConcurrentDictionary<string, WorkflowNode> _bigramGraph = new();
    private ConcurrentDictionary<string, WorkflowNode> _trigramGraph = new();

    public List<WorkflowSuggestion> GetNextCommandPredictions(
        string currentCommand,
        List<CommandHistoryEntry> recentCommands,
        int maxResults = 5)
    {
        var suggestions = new List<WorkflowSuggestion>();

        // Try trigram first (more specific)
        if (recentCommands.Count >= 2)
        {
            var trigramKey = $"{recentCommands[1].Command}→{recentCommands[0].Command}";
            if (_trigramGraph.TryGetValue(trigramKey, out var trigramNode))
            {
                suggestions.AddRange(trigramNode.GetTopSuggestions(maxResults)
                    .Select(s => new WorkflowSuggestion
                    {
                        Command = s.NextCommand,
                        Confidence = s.Probability * 1.2, // Boost for trigram match
                        Source = "Trigram"
                    }));
            }
        }

        // Fallback to bigram (less specific, but more data)
        if (recentCommands.Count >= 1)
        {
            var bigramKey = recentCommands[0].Command;
            if (_bigramGraph.TryGetValue(bigramKey, out var bigramNode))
            {
                suggestions.AddRange(bigramNode.GetTopSuggestions(maxResults)
                    .Select(s => new WorkflowSuggestion
                    {
                        Command = s.NextCommand,
                        Confidence = s.Probability,
                        Source = "Bigram"
                    }));
            }
        }

        // De-duplicate and return top N by confidence
        return suggestions
            .GroupBy(s => s.Command)
            .Select(g => g.OrderByDescending(s => s.Confidence).First())
            .OrderByDescending(s => s.Confidence)
            .Take(maxResults)
            .ToList();
    }
}
```

#### Database Schema Update

```sql
-- Add n-gram order column
CREATE TABLE workflow_transitions (
    ngram_order INTEGER NOT NULL,              -- 2 for bigram, 3 for trigram
    command_sequence TEXT NOT NULL,            -- e.g., "git add→git commit" or "git add→git commit→git push"
    next_command TEXT NOT NULL,
    frequency INTEGER NOT NULL DEFAULT 1,
    total_time_delta_ms INTEGER NOT NULL DEFAULT 0,
    last_seen TEXT NOT NULL,
    PRIMARY KEY (ngram_order, command_sequence, next_command)
);

CREATE INDEX idx_workflow_sequence ON workflow_transitions(ngram_order, command_sequence);
```

#### Example Behavior

```powershell
# User's typical workflow (learned over time):
git add .
git commit -m "message"
git push

# Prediction behavior:
PS> git add .<Enter>
PS> git c█
    # Bigram (git add → ?): "git commit" (70% confidence)

PS> git commit -m "fix"<Enter>
PS> git p█
    # Trigram (git add → git commit → ?): "git push" (90% confidence)
    # ↑ Higher confidence because sequence is more specific

# Compare to unrelated sequence:
PS> git status<Enter>
PS> git c█
    # Bigram (git status → ?): "git checkout" (60% confidence)
    # ↑ Different prediction based on different prior command
```

#### Configuration

```powershell
# Maximum n-gram order (2 = bigrams only, 3 = trigrams)
$env:PSCUE_WORKFLOW_NGRAM_ORDER = "3"

# Prefer higher-order n-grams when available
$env:PSCUE_WORKFLOW_PREFER_SPECIFIC = "true"
```

---

### 5. Workflow Interruption Recovery

**Problem**: Unrelated commands break workflow context.

**Solution**: Detect and resume interrupted workflows.

#### Workflow Session Tracking

```csharp
public class WorkflowSession
{
    public Guid SessionId { get; set; }
    public List<CommandHistoryEntry> Commands { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime LastActivityTime { get; set; }
    public WorkflowType Type { get; set; }  // Git, Docker, Build, etc.
    public bool IsActive => (DateTime.UtcNow - LastActivityTime) < TimeSpan.FromMinutes(30);
}

public class WorkflowSessionTracker
{
    private List<WorkflowSession> _activeSessions = new();

    // Heuristic: Commands with shared context belong to same session
    public WorkflowSession? GetActiveSession(string command)
    {
        return _activeSessions
            .Where(s => s.IsActive)
            .FirstOrDefault(s => IsRelatedToSession(command, s));
    }

    private bool IsRelatedToSession(string command, WorkflowSession session)
    {
        // Check if command is same family as session
        var commandFamily = GetCommandFamily(command);
        var sessionFamily = GetCommandFamily(session.Commands.Last().Command);

        return commandFamily == sessionFamily;
    }

    private string GetCommandFamily(string command)
    {
        // Group related commands
        if (command.StartsWith("git")) return "git";
        if (command.StartsWith("docker")) return "docker";
        if (command.StartsWith("cargo") || command.StartsWith("rustc")) return "rust";
        if (command.StartsWith("npm") || command.StartsWith("node")) return "node";
        // ... more families
        return "unknown";
    }
}
```

#### Example Behavior

```powershell
# User starts git workflow
10:00 PS> git add .
      [Session #1: Git workflow started]

10:01 PS> ls             # Interruption (unrelated command)
10:02 PS> cat README.md  # Another interruption

# PSCue detects resumption of git workflow
10:03 PS> git c█
          # "git commit" still suggested (session #1 remembered)
          # Tooltip: "Resume git workflow (3 minutes ago)"

# Session expires after 30 minutes of inactivity
10:35 PS> git c█
          # "git commit" no longer boosted (session expired)
```

#### Implementation Notes

- Track sessions in-memory only (no persistence needed)
- Max 5 active sessions
- 30-minute timeout for inactive sessions
- LRU eviction if limit reached

---

### 6. Error-Driven Workflow Adjustment

**Problem**: Failed commands indicate need for recovery actions.

**Solution**: Boost recovery suggestions after command errors.

#### Error Pattern Recognition

```csharp
public class ErrorRecoveryPatterns
{
    private static readonly Dictionary<string, List<RecoveryAction>> Patterns = new()
    {
        // Git errors
        ["git push"] = new()
        {
            new RecoveryAction
            {
                ErrorPattern = "rejected.*non-fast-forward",
                SuggestedCommands = new[] { "git pull", "git fetch", "git pull --rebase" },
                Confidence = 0.9
            },
            new RecoveryAction
            {
                ErrorPattern = "authentication failed",
                SuggestedCommands = new[] { "git credential", "gh auth login" },
                Confidence = 0.85
            }
        },
        ["git merge"] = new()
        {
            new RecoveryAction
            {
                ErrorPattern = "CONFLICT",
                SuggestedCommands = new[] { "git status", "git mergetool", "git merge --abort" },
                Confidence = 0.95
            }
        },

        // Docker errors
        ["docker run"] = new()
        {
            new RecoveryAction
            {
                ErrorPattern = "port is already allocated",
                SuggestedCommands = new[] { "docker ps", "docker stop" },
                Confidence = 0.9
            },
            new RecoveryAction
            {
                ErrorPattern = "Unable to find image",
                SuggestedCommands = new[] { "docker pull", "docker images" },
                Confidence = 0.95
            }
        },

        // Build errors
        ["cargo build"] = new()
        {
            new RecoveryAction
            {
                ErrorPattern = "could not compile",
                SuggestedCommands = new[] { "cargo check", "cargo fix" },
                Confidence = 0.8
            }
        }
    };

    public List<RecoverySuggestion> GetRecoverySuggestions(
        string command,
        string errorOutput)
    {
        if (!Patterns.TryGetValue(command, out var recoveryActions))
            return new List<RecoverySuggestion>();

        var suggestions = new List<RecoverySuggestion>();

        foreach (var action in recoveryActions)
        {
            if (Regex.IsMatch(errorOutput, action.ErrorPattern, RegexOptions.IgnoreCase))
            {
                suggestions.AddRange(action.SuggestedCommands.Select(cmd =>
                    new RecoverySuggestion
                    {
                        Command = cmd,
                        Reason = $"Recovery for: {action.ErrorPattern}",
                        Confidence = action.Confidence
                    }));
            }
        }

        return suggestions;
    }
}
```

#### Integration with FeedbackProvider

```csharp
// In FeedbackProvider.cs
protected override void OnCommandFailed(FeedbackContext context)
{
    // Get error output from context
    var errorOutput = context.CommandResult?.ErrorOutput ?? "";

    // Check for known error patterns
    var recoveryPatterns = new ErrorRecoveryPatterns();
    var suggestions = recoveryPatterns.GetRecoverySuggestions(
        context.Command,
        errorOutput);

    if (suggestions.Any())
    {
        // Store recovery suggestions for next prediction
        PSCueModule.ErrorRecoveryCache.Set(
            context.Command,
            suggestions,
            TimeSpan.FromMinutes(5) // Short TTL
        );
    }
}
```

#### Example Behavior

```powershell
PS> git push
error: failed to push some refs to 'origin'
hint: Updates were rejected because the remote contains work
hint: that you do not have locally. You may want to integrate the
hint: remote changes (e.g., 'git pull ...') before pushing again.

PS> git p█
    # Top suggestion: "git pull" (recovery suggestion, 0.95 confidence)
    # Tooltip: "Suggested recovery for failed push"
    # Also shows: "git pull --rebase" (0.90 confidence)
```

---

### 7. Multi-Tool Workflows

**Problem**: Real workflows span multiple tools (editor → compiler → version control).

**Solution**: Recognize cross-tool workflow patterns.

#### Workflow Categories

```csharp
public enum WorkflowCategory
{
    Unknown,
    Development,     // edit → build → test
    Deployment,      // build → package → push
    Debugging,       // run → logs → inspect
    VCS,            // add → commit → push
    Investigation   // ls → cd → cat → grep
}

public class WorkflowPatternRecognizer
{
    private static readonly Dictionary<WorkflowCategory, List<string[]>> Patterns = new()
    {
        [WorkflowCategory.Development] = new()
        {
            new[] { "vim", "cargo build", "cargo test" },
            new[] { "code", "npm run build", "npm test" },
            new[] { "vim", "dotnet build", "dotnet test" },
            new[] { "nano", "gcc", "./a.out" }
        },

        [WorkflowCategory.Deployment] = new()
        {
            new[] { "docker build", "docker tag", "docker push" },
            new[] { "npm run build", "npm publish" },
            new[] { "cargo build --release", "cargo publish" }
        },

        [WorkflowCategory.VCS] = new()
        {
            new[] { "git add", "git commit", "git push" },
            new[] { "git checkout", "git pull", "git merge" }
        },

        [WorkflowCategory.Investigation] = new()
        {
            new[] { "ls", "cd", "cat" },
            new[] { "find", "grep", "vim" },
            new[] { "docker ps", "docker logs", "docker exec" }
        }
    };

    // Detect if recent commands match a known multi-tool workflow
    public (WorkflowCategory Category, int StepIndex)? DetectWorkflow(
        List<CommandHistoryEntry> recentCommands)
    {
        foreach (var (category, patternList) in Patterns)
        {
            foreach (var pattern in patternList)
            {
                var matchIndex = MatchPattern(recentCommands, pattern);
                if (matchIndex >= 0)
                {
                    return (category, matchIndex);
                }
            }
        }

        return null;
    }

    private int MatchPattern(List<CommandHistoryEntry> history, string[] pattern)
    {
        // Check if recent history matches pattern
        // Returns the index of current step in pattern, or -1 if no match

        if (history.Count < pattern.Length - 1)
            return -1;

        // Reverse match (history is newest-first)
        for (int i = 0; i < pattern.Length - 1; i++)
        {
            var expectedCommand = pattern[pattern.Length - 2 - i];
            var actualCommand = history[i].Command;

            if (!actualCommand.StartsWith(expectedCommand, StringComparison.OrdinalIgnoreCase))
                return -1;
        }

        // Match found, return the next expected step index
        return pattern.Length - 1;
    }

    public string? GetNextStep(WorkflowCategory category, int stepIndex)
    {
        if (Patterns.TryGetValue(category, out var patterns))
        {
            // Return the next command in any matching pattern
            return patterns
                .Where(p => p.Length > stepIndex)
                .Select(p => p[stepIndex])
                .FirstOrDefault();
        }

        return null;
    }
}
```

#### Example Behavior

```powershell
# User working on Rust project
PS> vim src/main.rs<Enter>
    [Workflow detected: Development (edit phase)]

PS> cargo build<Enter>
    [Workflow detected: Development (build phase)]

PS> cargo t█
    # "cargo test" boosted 1.6× (next step in Development workflow)
    # Tooltip: "Next step in development workflow"

PS> cargo test<Enter>
    [Workflow detected: Development (test phase)]

PS> git a█
    # "git add" boosted 1.5× (transitioning to VCS workflow)
    # Tooltip: "Common next workflow after testing"
```

---

## Implementation Roadmap

### Phase 18.1: Dynamic Workflow Learning (HIGH PRIORITY)
**Status**: 🚧 **IN PROGRESS** (~75% complete)

**Estimated Effort**: 40-50 hours

**Goal**: Learn workflow patterns from user behavior dynamically.

**Tasks**:
1. ✅ Create `WorkflowLearner.cs` class (~500 lines - more comprehensive than planned)
   - ✅ Workflow graph data structures (WorkflowTransition, WorkflowSuggestion)
   - ✅ Record transitions from FeedbackProvider with timing data
   - ✅ Query predictions with time-sensitive scoring
   - ✅ Command normalization (extract base command + subcommand)
   - ✅ Memory management (max 20 transitions per command, LRU eviction)
2. ✅ Extend SQLite schema with `workflow_transitions` table
   - ✅ Columns: from_command, to_command, frequency, total_time_delta_ms, first_seen, last_seen
   - ✅ Indexed on from_command for fast lookups
   - ✅ Additive merging for concurrent sessions
3. ✅ Integrate with FeedbackProvider (~50 lines changes)
   - ✅ Record transitions after successful commands
   - ✅ Calculate time delta between commands
   - ✅ Filter by max time delta (15 min default)
4. ✅ Integrate with ModuleInitializer
   - ✅ Load/save workflow data on module lifecycle
   - ✅ Auto-save every 5 minutes
5. ✅ Add configuration environment variables
   - ✅ PSCUE_WORKFLOW_LEARNING (default: true)
   - ✅ PSCUE_WORKFLOW_MIN_FREQUENCY (default: 5)
   - ✅ PSCUE_WORKFLOW_MAX_TIME_DELTA (default: 15 minutes)
   - ✅ PSCUE_WORKFLOW_MIN_CONFIDENCE (default: 0.6)
6. 🔄 Integrate with CommandPredictor (~100 lines changes) - **NEXT**
7. ⏳ Write comprehensive tests (~25 test cases)
8. ⏳ Documentation updates (README.md status)

**Dependencies**: None (uses existing infrastructure)

**Implementation Details**:
- **Core Class**: `src/PSCue.Module/WorkflowLearner.cs` (500+ lines)
- **Database**: `workflow_transitions` table with timing data
- **Automatic Learning**: Integrated with FeedbackProvider
- **Time-Sensitive Scoring**: Adjusts confidence based on timing patterns
  - Within expected timeframe: 1.5× boost
  - Moderately delayed: 1.2× boost
  - Very old: 0.8× boost (weak relationship)

**Success Criteria**:
- ✅ Workflow transitions recorded automatically
- ✅ Cross-session persistence works
- ⏳ Predictions improve based on learned workflows (CommandPredictor integration pending)
- ⏳ All tests passing (tests not yet written)
- ✅ Performance targets met (<2ms lookup - architecture supports this)

---

### Phase 18.2: Time-Based Detection (MEDIUM PRIORITY)
**Status**: ✅ **COMPLETED** (implemented as part of Phase 18.1)

**Estimated Effort**: 15-20 hours (already included in 18.1)

**Goal**: Weight suggestions by time proximity.

**Implementation**: This was implemented directly in Phase 18.1 as part of WorkflowLearner's time-sensitive scoring feature.

**Completed**:
- ✅ `WorkflowTransition` tracks time deltas (TotalTimeDeltaMs, AverageTimeDelta)
- ✅ `FeedbackProvider` records timing between commands
- ✅ Time-based scoring in `WorkflowLearner.GetTimeSensitiveScore()`
- ✅ Database schema includes time delta column
- ⏳ Tests for time-based scoring (pending with Phase 18.1 tests)

**Dependencies**: Phase 18.1 (WorkflowLearner must exist)

**Success Criteria**:
- ✅ Time deltas recorded in database
- ✅ Scoring adjusted based on time proximity
- ✅ Old commands don't over-boost suggestions
- ⏳ All tests passing (pending)

---

### Phase 18.3: Workflow Chains (3+ Commands) (MEDIUM PRIORITY)
**Estimated Effort**: 25-30 hours

**Goal**: Track and predict based on 2-3 command history (trigrams).

**Tasks**:
1. Extend `WorkflowLearner` to support trigrams (~150 lines)
2. Add n-gram order configuration
3. Update database schema with ngram_order column
4. Modify prediction logic to try trigram then bigram (~100 lines)
5. Write tests for n-gram matching (~15 test cases)
6. Documentation updates

**Dependencies**: Phase 18.1 (WorkflowLearner must exist)

**Success Criteria**:
- ✅ Trigrams recorded and queried
- ✅ Higher confidence for longer matches
- ✅ Graceful fallback to bigrams
- ✅ All tests passing

---

### Phase 18.4: Project-Type Detection (MEDIUM-LOW PRIORITY)
**Estimated Effort**: 20-25 hours

**Goal**: Context-aware suggestions based on project type.

**Tasks**:
1. Create `ProjectTypeDetector.cs` (~250 lines)
   - File-based detection logic
   - Directory tree walking
   - Caching with TTL
2. Integrate with GenericPredictor (~80 lines)
3. Add project-type boost mappings
4. Write tests for detection logic (~12 test cases)
5. Documentation updates

**Dependencies**: None (standalone feature)

**Success Criteria**:
- ✅ Project types detected correctly
- ✅ Relevant commands boosted
- ✅ Irrelevant commands de-prioritized
- ✅ Performance acceptable (<10ms cache miss)
- ✅ All tests passing

---

### Phase 18.5: Workflow Interruption Recovery (LOW PRIORITY)
**Estimated Effort**: 15-20 hours

**Goal**: Remember and resume interrupted workflows.

**Tasks**:
1. Create `WorkflowSessionTracker.cs` (~200 lines)
2. Track active sessions in-memory
3. Detect session resumption
4. Integrate with GenericPredictor (~50 lines)
5. Write tests (~10 test cases)
6. Documentation updates

**Dependencies**: Phase 18.1 (workflow learning foundation)

**Success Criteria**:
- ✅ Sessions tracked correctly
- ✅ Interruptions don't break context
- ✅ Sessions expire appropriately
- ✅ All tests passing

---

### Phase 18.6: Error-Driven Workflow Adjustment (LOW PRIORITY)
**Estimated Effort**: 30-35 hours

**Goal**: Suggest recovery actions after command failures.

**Tasks**:
1. Create `ErrorRecoveryPatterns.cs` (~300 lines)
   - Define error patterns for common tools
   - Recovery suggestion logic
2. Extend FeedbackProvider with error handling (~100 lines)
3. Create error recovery cache (~50 lines)
4. Integrate with GenericPredictor (~60 lines)
5. Write tests for error patterns (~20 test cases)
6. Documentation updates

**Dependencies**: Requires PowerShell error output access

**Challenges**:
- Not all error output may be available via FeedbackProvider
- May need to explore alternative APIs

**Success Criteria**:
- ✅ Error patterns detected
- ✅ Recovery suggestions shown
- ✅ Suggestions expire appropriately
- ✅ All tests passing

---

### Phase 18.7: Multi-Tool Workflows (LOW PRIORITY)
**Estimated Effort**: 25-30 hours

**Goal**: Recognize workflows spanning multiple tools.

**Tasks**:
1. Create `WorkflowPatternRecognizer.cs` (~250 lines)
2. Define common multi-tool workflow patterns
3. Integrate with WorkflowLearner (~100 lines)
4. Add workflow category tracking
5. Write tests for pattern matching (~15 test cases)
6. Documentation updates

**Dependencies**: Phase 18.1 (workflow learning foundation)

**Success Criteria**:
- ✅ Multi-tool patterns recognized
- ✅ Cross-tool transitions suggested
- ✅ Workflow categories detected
- ✅ All tests passing

---

### Summary Timeline

| Phase | Priority | Effort | Dependencies |
|-------|----------|--------|--------------|
| 18.1: Dynamic Workflow Learning | HIGH | 40-50h | None |
| 18.2: Time-Based Detection | MEDIUM | 15-20h | 18.1 |
| 18.3: Workflow Chains | MEDIUM | 25-30h | 18.1 |
| 18.4: Project-Type Detection | MEDIUM-LOW | 20-25h | None |
| 18.5: Interruption Recovery | LOW | 15-20h | 18.1 |
| 18.6: Error-Driven Adjustment | LOW | 30-35h | None (challenges) |
| 18.7: Multi-Tool Workflows | LOW | 25-30h | 18.1 |

**Total Estimated Effort**: 170-210 hours

**Recommended Order**:
1. Start with **18.1** (foundation for most other features)
2. Follow with **18.2** and **18.3** (natural extensions)
3. Add **18.4** (independent, high user value)
4. Defer **18.5**, **18.6**, **18.7** (nice-to-have polish)

---

## Technical Architecture

### Component Integration

```
┌─────────────────────────────────────────────────────────────┐
│                    FeedbackProvider.cs                       │
│  • OnCommandExecuted: Record workflow transitions            │
│  • OnCommandFailed: Detect error patterns                    │
│  • Track timing between commands                             │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                   WorkflowLearner.cs (NEW)                   │
│  • Workflow graph (bigrams/trigrams)                         │
│  • RecordTransition(from, to, timeDelta)                     │
│  • GetNextCommandPredictions(current, history)               │
│  • Time-based scoring                                        │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│               PersistenceManager.cs (EXTEND)                 │
│  • SaveWorkflowTransitions()                                 │
│  • LoadWorkflowTransitions()                                 │
│  • workflow_transitions table                                │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│              ProjectTypeDetector.cs (NEW)                    │
│  • DetectProjectType(directory)                              │
│  • Cache detection results (5-minute TTL)                    │
│  • Walk directory tree for indicator files                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              GenericPredictor.cs (EXTEND)                    │
│  • Query WorkflowLearner for workflow predictions            │
│  • Apply project-type boosts                                 │
│  • Blend workflow + argument + ML suggestions                │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              CommandPredictor.cs                             │
│  • Surface workflow suggestions as inline predictions        │
│  • Show confidence/source in tooltips                        │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

```
1. User executes command:
   "git add ." <Enter>

2. FeedbackProvider receives OnCommandExecuted:
   - Extract command: "git add"
   - Get previous command: "git status"
   - Calculate time delta: 15 seconds

3. WorkflowLearner.RecordTransition():
   - Key: "git status" → "git add"
   - Increment frequency
   - Update average time delta
   - Update last seen timestamp

4. PersistenceManager.SaveWorkflowTransitions():
   - Async write to SQLite
   - Additive merge (frequency += 1)

5. User starts typing next command:
   "git c"

6. GenericPredictor.GetSuggestions():
   - Query WorkflowLearner.GetNextCommandPredictions("git add", [...])
   - Returns: [("git commit", 0.85), ("git status", 0.60)]
   - Query ProjectTypeDetector.DetectProjectType(currentDir)
   - Returns: [Git, Rust]
   - Apply project-type boost: "git" commands get 1.0× (already relevant)
   - Blend with argument suggestions

7. CommandPredictor.GetSuggestion():
   - Format as inline suggestion
   - Return to PowerShell

8. User sees:
   "git commit" (inline suggestion, 85% confidence)
```

---

## Testing Strategy

### Unit Tests

**WorkflowLearner.cs** (~25 tests):
- Transition recording (frequency, timing, last seen)
- Prediction queries (bigram, trigram, fallback)
- Time-based scoring (immediate, delayed, old)
- Cache behavior (hit, miss, expiration)
- Edge cases (empty history, unknown commands)

**ProjectTypeDetector.cs** (~15 tests):
- Project type detection (Rust, Node, .NET, Python, Go)
- Multi-type projects (e.g., Node + Docker)
- Directory tree walking
- Cache behavior (TTL, invalidation)
- Performance (cache hit <1ms)

**WorkflowSessionTracker.cs** (~12 tests):
- Session creation and tracking
- Interruption detection
- Session resumption
- Timeout and expiration
- LRU eviction

**ErrorRecoveryPatterns.cs** (~20 tests):
- Pattern matching (git, docker, cargo errors)
- Recovery suggestion generation
- Confidence scoring
- Unknown error handling

**WorkflowPatternRecognizer.cs** (~15 tests):
- Multi-tool pattern detection
- Workflow category recognition
- Pattern matching logic
- Next step prediction

### Integration Tests

**End-to-End Workflow Learning** (~10 tests):
1. Execute sequence of commands
2. Verify transitions recorded
3. Verify predictions improve
4. Verify cross-session persistence
5. Verify time-based scoring
6. Verify project-type influence

**Performance Tests** (~8 tests):
- Workflow graph lookup (<2ms)
- Project type detection (<10ms cache miss)
- Database operations (async, non-blocking)
- Memory usage (graph size limits)

### Manual Testing Scenarios

**Scenario 1: Git Workflow**
```powershell
# Execute multiple times to learn pattern
git add .
git commit -m "test"
git push

# Verify predictions improve over time
# After 5 iterations, "git push" should be top suggestion after "git commit"
```

**Scenario 2: Cross-Tool Workflow**
```powershell
# Execute multiple times
vim src/main.rs
cargo build
cargo test
git add .

# Verify PSCue suggests "git add" after "cargo test"
```

**Scenario 3: Project-Type Switching**
```powershell
cd ~/rust-project
cargo b<tab>  # Should strongly suggest "cargo build"

cd ~/node-project
cargo b<tab>  # Should weakly suggest cargo (or not at all)
npm b<tab>    # Should strongly suggest "npm run build"
```

---

## Configuration

### Environment Variables

```powershell
# Dynamic workflow learning
$env:PSCUE_WORKFLOW_LEARNING = "true"              # Enable/disable (default: true)
$env:PSCUE_WORKFLOW_MIN_FREQUENCY = "5"            # Min occurrences to suggest (default: 5)
$env:PSCUE_WORKFLOW_MAX_TIME_DELTA = "15"          # Max minutes between commands (default: 15)
$env:PSCUE_WORKFLOW_MIN_CONFIDENCE = "0.6"         # Min confidence to show suggestion (default: 0.6)

# N-gram configuration
$env:PSCUE_WORKFLOW_NGRAM_ORDER = "3"              # 2=bigrams, 3=trigrams (default: 3)
$env:PSCUE_WORKFLOW_PREFER_SPECIFIC = "true"       # Prefer trigrams over bigrams (default: true)

# Time-based scoring
$env:PSCUE_WORKFLOW_TIME_IMMEDIATE = "30"          # Seconds for "immediate" boost (default: 30)
$env:PSCUE_WORKFLOW_TIME_STRONG = "300"            # Seconds for "strong" boost (default: 300)
$env:PSCUE_WORKFLOW_TIME_MODERATE = "1800"         # Seconds for "moderate" boost (default: 1800)

# Project-type detection
$env:PSCUE_PROJECT_DETECTION = "true"              # Enable/disable (default: true)
$env:PSCUE_PROJECT_CACHE_TTL = "300"               # Cache TTL in seconds (default: 300)
$env:PSCUE_PROJECT_BOOST_FACTOR = "1.5"            # Boost for relevant tools (default: 1.5)

# Workflow sessions
$env:PSCUE_WORKFLOW_SESSION_TIMEOUT = "30"         # Minutes before session expires (default: 30)
$env:PSCUE_WORKFLOW_MAX_SESSIONS = "5"             # Max concurrent sessions (default: 5)

# Error recovery
$env:PSCUE_ERROR_RECOVERY = "true"                 # Enable/disable (default: true)
$env:PSCUE_ERROR_CACHE_TTL = "300"                 # Seconds to cache recovery suggestions (default: 300)

# Multi-tool workflows
$env:PSCUE_MULTITOOL_WORKFLOWS = "true"            # Enable/disable (default: true)
```

### PowerShell Functions

```powershell
# View learned workflows
Get-PSCueWorkflows [-Command <string>] [-AsJson]

# Example output:
Command: git add
Next Commands (by frequency):
  - git commit (87%, avg 45s after)
  - git status (12%, avg 10s after)
  - git reset  (1%, avg 120s after)

# Clear workflow data
Clear-PSCueWorkflows [-WhatIf] [-Confirm]

# Export workflows for backup
Export-PSCueWorkflows -Path ~/workflows.json

# Import workflows (merge with existing)
Import-PSCueWorkflows -Path ~/workflows.json [-Merge]

# Show workflow statistics
Get-PSCueWorkflowStats [-Detailed] [-AsJson]

# Example output:
Total Transitions: 1,247
Unique Command Pairs: 89
Most Common Workflow: git add → git commit (143 times)
Average Time Delta: 38 seconds
Database Size: 45 KB
```

---

## Success Metrics

### Phase 18.1: Dynamic Workflow Learning

**Functional**:
- ✅ Workflow transitions recorded automatically after each command
- ✅ Predictions shown based on learned workflows (min 5 occurrences)
- ✅ Cross-session persistence works (survives PowerShell restarts)
- ✅ Confidence scores calculated correctly (frequency-based)

**Performance**:
- ✅ Workflow graph lookup: <2ms (in-memory cache)
- ✅ Database persistence: Async, non-blocking
- ✅ Memory usage: <100KB for 1000 transitions
- ✅ No impact on module load time (<10ms overhead)

**Quality**:
- ✅ All unit tests passing (25+ tests)
- ✅ All integration tests passing (10+ tests)
- ✅ Manual scenarios work as expected
- ✅ Documentation complete and accurate

---

### Phase 18.2: Time-Based Detection

**Functional**:
- ✅ Time deltas recorded between commands
- ✅ Scoring adjusted based on time proximity
- ✅ Commands >2 hours old don't over-boost suggestions
- ✅ Average time delta stored per transition

**Performance**:
- ✅ Time calculation: <1ms (simple arithmetic)
- ✅ Database storage: 8 extra bytes per transition (negligible)

**Quality**:
- ✅ All unit tests passing (10+ tests)
- ✅ Integration tests verify time-based behavior
- ✅ Documentation updated

---

### Phase 18.3: Workflow Chains (3+ Commands)

**Functional**:
- ✅ Trigrams recorded and queried
- ✅ Higher confidence for longer matches (trigram > bigram)
- ✅ Graceful fallback to bigrams when trigram unavailable
- ✅ Configurable n-gram order (2 or 3)

**Performance**:
- ✅ Trigram lookup: <3ms (additional index)
- ✅ Memory: ~150 bytes per trigram

**Quality**:
- ✅ All unit tests passing (15+ tests)
- ✅ Integration tests verify n-gram behavior
- ✅ Documentation updated

---

### Phase 18.4: Project-Type Detection

**Functional**:
- ✅ Project types detected correctly (Rust, Node, .NET, Python, Go, Docker)
- ✅ Multi-type projects supported (e.g., Node + Docker)
- ✅ Relevant commands boosted (1.5× default)
- ✅ Irrelevant commands de-prioritized (0.5× default)

**Performance**:
- ✅ Cache hit: <1ms
- ✅ Cache miss (file system scan): <10ms
- ✅ Cache TTL: 5 minutes (configurable)

**Quality**:
- ✅ All unit tests passing (15+ tests)
- ✅ Integration tests verify project-type influence
- ✅ Documentation updated

---

## Open Questions

### Question 1: Workflow Graph Size Limits
**Issue**: Unlimited workflow graph could grow very large over time.

**Options**:
- **A**: Fixed limit (e.g., 1000 transitions), LRU eviction
- **B**: Adaptive limit based on available memory
- **C**: Prune low-frequency transitions periodically (< 3 occurrences)

**Recommendation**: Start with **Option A** (1000 transitions with LRU). Simple, predictable, sufficient for most users.

---

### Question 2: Cross-User Workflow Sharing
**Issue**: Users may want to share learned workflows (e.g., team onboarding).

**Options**:
- **A**: Export/Import via JSON (already supported)
- **B**: Cloud sync with opt-in (future enhancement)
- **C**: Curated workflow packs (community-maintained)

**Recommendation**: **Option A** sufficient for now. Revisit B/C if user demand exists.

---

### Question 3: Workflow Privacy
**Issue**: Workflow data may contain sensitive information (project names, etc.).

**Approach**:
- Reuse existing PSCUE_IGNORE_PATTERNS from Phase 17.2
- Apply same filtering to workflow transitions
- Ensure workflow exports are clearly labeled as containing user data

**Recommendation**: Extend existing privacy system to cover workflows.

---

### Question 4: Conflict Resolution
**Issue**: What if workflow suggestions conflict with other predictors?

**Scenario**:
```powershell
# WorkflowLearner suggests: "git push" (0.85 confidence)
# GenericPredictor suggests: "git pull" (0.90 confidence, higher frequency)
# Which wins?
```

**Options**:
- **A**: Highest confidence wins (simple)
- **B**: Weighted blend (workflow 0.4, generic 0.3, ML 0.3)
- **C**: Show multiple suggestions with source labels

**Recommendation**: **Option B** (weighted blend). Matches current architecture and prevents any single predictor from dominating.

---

### Question 5: Background Workflow Computation
**Issue**: Should workflow predictions be pre-computed in background thread?

**Context**:
- PowerShell ICommandPredictor has 20ms timeout (see ML-PREDICTIONS.md)
- Workflow lookup should be fast (<2ms), but complex scoring might exceed budget

**Options**:
- **A**: Synchronous lookup (simpler, likely fast enough)
- **B**: Background pre-computation (more complex, guarantees <1ms)

**Recommendation**: Start with **Option A** (synchronous). Only implement B if performance testing shows >20ms latency.

---

## References

- **Phase 17.1 ML Predictions**: `ML-PREDICTIONS.md` (n-gram infrastructure)
- **ContextAnalyzer**: `src/PSCue.Module/ContextAnalyzer.cs` (current static patterns)
- **SequencePredictor**: `src/PSCue.Module/SequencePredictor.cs` (n-gram implementation)
- **FeedbackProvider**: `src/PSCue.Module/FeedbackProvider.cs` (learning trigger point)
- **GenericPredictor**: `src/PSCue.Module/GenericPredictor.cs` (suggestion generation)
- **PowerShell ICommandPredictor**: https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-cmdlet-predictor

---

## Next Steps

1. **Review and approve this design document**
2. **Create Phase 18.1 implementation plan** (detailed task breakdown)
3. **Prototype WorkflowLearner.cs** (validate architecture)
4. **Implement Phase 18.1** (dynamic workflow learning)
5. **Gather user feedback** (does it improve suggestions?)
6. **Iterate and add Phase 18.2+** (based on feedback)

---

**Document Status**: DRAFT - Awaiting review and approval

**Author**: Claude (AI Assistant)

**Date**: 2025-11-07
