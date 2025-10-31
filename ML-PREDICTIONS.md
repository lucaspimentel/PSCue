# ML-Based Predictions for PSCue

**Last Updated**: 2025-10-31

This document outlines the design and implementation strategy for ML-based prediction features in PSCue.

## Current State vs. ML-Based Approach

**Current System (Frequency + Recency Scoring)**:
- Simple heuristic: `score = (frequency * 0.6) + (recency * 0.4)`
- Works well for basic patterns
- Fast, deterministic, explainable
- No training required

**ML-Based Predictions - Key Benefits**:
1. **Context-aware**: Learn complex patterns (e.g., "after `git add`, suggest `git commit`")
2. **Personalized**: Adapt to individual workflow patterns
3. **Sequence understanding**: Predict based on command chains
4. **Semantic understanding**: Recognize file paths, URLs, branch names as concepts

## Potential Approaches

### 1. **Lightweight: N-gram Language Model**
**Pros**:
- Fast, simple to implement
- Works offline, no external dependencies
- Can run in-process with minimal overhead
- Good for predicting next command in sequence

**Implementation**:
```csharp
// Store bigrams/trigrams of command sequences
// "git status" → "git add" (70% of time)
// "git add" → "git commit" (80% of time)
```

**Best for**: Command sequence prediction (what command comes next)

### 2. **Medium: TF-IDF + Cosine Similarity**
**Pros**:
- Good for argument similarity
- No training phase needed
- Works well with small data
- Can find similar command patterns

**Use case**: "This argument was useful in similar contexts"

### 3. **Advanced: Small Transformer Model (Local)**
**Options**:
- ONNX Runtime for local inference
- Small model (<100MB) for command prediction
- Train on user's command history

**Pros**:
- Best accuracy
- Understands complex patterns
- Can be personalized

**Cons**:
- Requires training phase
- Higher resource usage
- More complex deployment

### 4. **Hybrid Approach (Recommended Starting Point)**
Combine existing heuristics with lightweight ML:

```
Final Score = (0.4 × Frequency/Recency) +
              (0.3 × N-gram Sequence Score) +
              (0.3 × Context Similarity)
```

## Critical Performance Constraint: 20ms Timeout ⚠️

**CRITICAL**: PowerShell's `ICommandPredictor` interface has a **hardcoded 20ms timeout**. Any predictor not responding within 20ms is **silently ignored** and won't display suggestions.

### Source
- **File**: `PowerShell/src/System.Management.Automation/engine/Subsystem/PredictionSubsystem/CommandPrediction.cs`
- **Implementation**: Uses `Task.WhenAny()` + `Task.Delay(20)` to enforce timeout
- **Not configurable**: As of PowerShell 7.5 (2025), this cannot be changed without recompiling PowerShell
- **Feature request**: [PSReadLine #4029](https://github.com/PowerShell/PSReadLine/issues/4029) exists but not implemented

### Implications for ML Implementation

**Direct ML inference in `GetSuggestion()` is NOT viable**:
- ❌ N-gram lookup: 5ms (possible, but tight)
- ❌ ONNX embedding inference: 50ms+ (**exceeds 20ms limit - will be ignored!**)
- ❌ Any synchronous ML computation: Too risky

**Required Architecture: Background Pre-computation**

All ML predictions MUST be pre-computed in background threads and cached:

```csharp
class CommandPredictor : ICommandPredictor
{
    private ConcurrentDictionary<string, PredictionResult> _predictionCache;
    private CancellationTokenSource _backgroundWorker;

    // Background thread runs continuously
    private async Task BackgroundPredictionLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var context = GetCurrentContext();

            // ML inference can take 50ms+ here - that's OK, we're in background
            var predictions = await ComputeMLPredictions(context);

            // Cache results for instant lookup
            _predictionCache[context.Input] = predictions;

            await Task.Delay(100, ct); // Update every 100ms
        }
    }

    // Called by PowerShell - MUST return <20ms
    public SuggestionPackage GetSuggestion(PredictionContext context)
    {
        // Just lookup pre-computed result (<1ms)
        if (_predictionCache.TryGetValue(context.Input, out var cached))
            return cached.ToSuggestionPackage();

        // Fallback to fast heuristics if cache miss
        return GetFrequencyRecencyFallback(context); // <5ms
    }
}
```

### Alternative: Tab Completion Only

**Option**: Skip ML for inline predictions entirely, use only in Tab completion
- Tab completion (`Register-ArgumentCompleter`) has **no hard timeout**
- Users expect slight delay on Tab press (50-100ms acceptable)
- Can afford synchronous ML inference
- Simpler architecture, no background threads needed

**Trade-off**: Lose inline prediction benefits, but gain implementation simplicity

## Key Questions to Consider

1. **Performance Budget**: What's acceptable latency?
   - **Tab completion**: <50ms (user expectation, no hard limit)
   - **Inline predictions**: **<20ms HARD LIMIT** (enforced by PowerShell)

2. **Model Size**: What's acceptable?
   - <10MB: Can ship with module
   - <100MB: Download on first use
   - >100MB: Optional advanced feature

3. **Training Strategy**:
   - Train locally on user data? (privacy-friendly)
   - Pre-trained model + fine-tuning?
   - No training, pure inference?

4. **Fallback Behavior**:
   - What if ML prediction fails/slow?
   - Always have frequency+recency as fallback (<5ms)

5. **Architecture Choice** (NEW):
   - **Option A**: Background pre-computation + caching (complex, supports inline predictions)
   - **Option B**: Tab completion only (simple, no 20ms constraint)

## Practical Implementation Path

Here's the recommended phased approach:

### Phase 17.1: N-gram Sequence Predictor
**Goal**: Predict next command based on recent command history

**Components**:
- `SequencePredictor.cs`: Build/query n-gram model
- Store in SQLite alongside ArgumentGraph
- Blend with existing GenericPredictor scores

**Example**:
```powershell
# User types: git add .
# After Enter, ML learns: "git add" → "git commit" pattern

# Next time, when typing "git c":
# Regular score: "checkout" (0.7), "commit" (0.6)
# Sequence boost: "commit" gets +0.3 (appeared after "git add" frequently)
# Final: "commit" (0.9), "checkout" (0.7)
```

**Implementation Details**:
- Track command sequences in `CommandHistory`
- Build bigram/trigram frequency tables
- Store in SQLite for cross-session persistence
- Query during prediction to boost relevant suggestions
- Configurable via `$env:PSCUE_SEQUENCE_LEARNING` (enable/disable)

**Performance Targets**:
- N-gram lookup: <5ms (for inline predictions, must fit in 20ms budget)
- Background pre-computation: Can take 50-100ms (runs asynchronously)
- Model size: <1MB in database
- No impact on Tab completion (<50ms maintained)

**Architecture Decision**:
- **Recommended**: Background pre-computation approach
  - Pre-compute n-gram predictions every 100ms in background thread
  - Cache results in `ConcurrentDictionary` for instant (<1ms) lookup
  - `GetSuggestion()` always returns <5ms (cached lookup + fallback)
  - Supports both Tab completion and inline predictions
- **Alternative**: Tab completion only (if background threading proves complex)

**Database Schema**:
```sql
CREATE TABLE CommandSequences (
    PrevCommand TEXT NOT NULL,
    NextCommand TEXT NOT NULL,
    Frequency INTEGER NOT NULL,
    LastSeen TEXT NOT NULL,
    PRIMARY KEY (PrevCommand, NextCommand)
);
```

### Phase 17.2: Context Embeddings (Optional Advanced)
**Goal**: Understand semantic similarity between commands

**Options**:
- Use pre-trained sentence embeddings (e.g., all-MiniLM-L6-v2)
- ONNX Runtime for local inference
- Cache embeddings for performance

**Challenges**:
- ONNX Runtime compatibility with NativeAOT (ArgumentCompleter)
- Model size (~80MB for MiniLM)
- Inference latency: 50ms+ (**exceeds 20ms inline prediction limit**)
- **REQUIRES background pre-computation architecture** for inline predictions

**Research Needed**:
- ONNX Runtime + NativeAOT compatibility
- Performance benchmarks on target hardware
- Embedding cache strategy

### Phase 17.3: Personalized Model (Future)
**Goal**: Train small model on user's history

**Approach**:
- Export training data from SQLite
- Train small model offline (Python scripts in `ai/` directory)
- Convert to ONNX for C# inference
- Optional download/training step

**Workflow**:
1. User runs `Export-PSCueTrainingData -Path ~/training-data.json`
2. Run Python training script: `python ai/train-model.py ~/training-data.json`
3. Model saved to `~/.local/share/PSCue/model.onnx`
4. Module automatically loads and uses model if present

**Privacy Considerations**:
- All training happens locally
- No data sent to external services
- User controls when/if to train
- Model can be deleted anytime

## Integration with Existing System

### GenericPredictor Enhancement

Current flow:
```
User input → GenericPredictor → Frequency/Recency scoring → Suggestions
```

Enhanced flow:
```
User input → GenericPredictor → Multiple scoring systems → Weighted blend → Suggestions
                                  ↓
                                  ├─ Frequency/Recency (0.4)
                                  ├─ N-gram Sequence (0.3)
                                  └─ Context Similarity (0.3)
```

### Scoring System Integration

```csharp
// src/PSCue.Module/GenericPredictor.cs
private double ComputeMLScore(string command, string[] arguments, CommandHistory history)
{
    // Base score from frequency/recency
    double baseScore = ComputeFrequencyRecencyScore(command, arguments);

    // Sequence boost from n-gram model
    double sequenceBoost = _sequencePredictor.GetSequenceScore(
        history.GetRecentCommands(3),
        command
    );

    // Context similarity (if embeddings available)
    double contextScore = _contextPredictor?.GetContextScore(command, arguments) ?? 0;

    // Weighted blend
    return (baseScore * 0.4) + (sequenceBoost * 0.3) + (contextScore * 0.3);
}
```

## Configuration

New environment variables:

```powershell
# Enable/disable ML features
$env:PSCUE_ML_ENABLED = "true"

# N-gram configuration
$env:PSCUE_NGRAM_ORDER = "2"              # Bigrams (can be 2 or 3)
$env:PSCUE_NGRAM_MIN_FREQUENCY = "3"     # Min occurrences to suggest

# Score blending weights
$env:PSCUE_SCORE_FREQUENCY = "0.4"
$env:PSCUE_SCORE_SEQUENCE = "0.3"
$env:PSCUE_SCORE_CONTEXT = "0.3"

# Performance limits
$env:PSCUE_ML_TIMEOUT_MS = "50"          # Max time for ML inference
```

## Testing Strategy

### Unit Tests
- N-gram model construction and queries
- Score blending logic
- Database persistence for sequences
- Performance benchmarks

### Integration Tests
- End-to-end prediction flow
- Fallback behavior when ML fails
- Cross-session sequence learning
- Privacy filter integration

### Performance Tests
- N-gram lookup latency (<5ms target)
- Total prediction time with ML (<100ms target)
- Memory usage with ML models loaded
- Database size growth over time

## Success Metrics

**Phase 17.1 (N-gram)**:
- ✅ Command sequences stored and retrieved correctly
- ✅ Sequence predictions improve suggestion relevance
- ✅ Performance targets met (<5ms for n-gram lookup)
- ✅ All tests passing
- ✅ Documentation updated

**Phase 17.2 (Embeddings)**:
- ✅ ONNX Runtime integrated successfully
- ✅ Background pre-computation working (<20ms constraint satisfied)
- ✅ Semantic similarity improves suggestions
- ✅ Model size acceptable (<100MB)
- ✅ NativeAOT compatibility verified (if used in ArgumentCompleter)

**Phase 17.3 (Personalized)**:
- ✅ Training pipeline works offline
- ✅ User can train custom model
- ✅ Custom model improves accuracy
- ✅ Privacy-preserving (all local)

## Next Steps

1. **Prototype N-gram predictor** (Phase 17.1)
   - Implement `SequencePredictor.cs`
   - Add database schema for sequences
   - Integrate with GenericPredictor
   - Write comprehensive tests

2. **Research ONNX Runtime** (Phase 17.2 prep)
   - Verify NativeAOT compatibility
   - Benchmark inference performance
   - Test model size impact on module load time

3. **Document ML architecture** (This file)
   - Design decisions
   - Implementation phases
   - Configuration options
   - Testing strategy

## References

- **ML.NET**: https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet
- **ONNX Runtime**: https://onnxruntime.ai/
- **N-gram Models**: https://en.wikipedia.org/wiki/N-gram
- **Sentence Transformers**: https://www.sbert.net/
- **TF-IDF**: https://en.wikipedia.org/wiki/Tf%E2%80%93idf

## Open Questions

1. Should n-gram model be shared across all commands or per-command?
   - Shared: Learn general workflows (e.g., "commit after add")
   - Per-command: More specific but requires more data

2. How to handle rare sequences?
   - Min frequency threshold?
   - Decay rare sequences over time?

3. Should ML be opt-in or opt-out?
   - Default on with `$env:PSCUE_ML_ENABLED`?
   - Require explicit opt-in for privacy?

4. How to evaluate prediction quality?
   - Accuracy: % of times ML prediction was accepted
   - Latency: Time to generate predictions
   - User satisfaction: Subjective feedback

---

**Status**: Design phase - awaiting implementation decision
