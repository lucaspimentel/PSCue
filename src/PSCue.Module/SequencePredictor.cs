using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PSCue.Module
{
    /// <summary>
    /// Predicts the next command based on command sequence history (n-grams).
    /// Uses background cache refresh to meet <20ms inline prediction constraint.
    /// </summary>
    public class SequencePredictor : IDisposable
    {
        // Thread-safe cache for fast lookups (<1ms target)
        private readonly ConcurrentDictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>> _cache;

        // Delta tracking for concurrent session support (additive merging)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, (int frequency, DateTime lastSeen)>> _delta;

        private readonly int _ngramOrder; // 2 = bigrams, 3 = trigrams
        private readonly int _minFrequency; // Minimum occurrences to suggest
        private readonly CancellationTokenSource _cancellationToken;
        private Task? _backgroundRefreshTask;
        private bool _disposed;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new SequencePredictor.
        /// </summary>
        /// <param name="ngramOrder">N-gram order: 2 for bigrams (prev -> next), 3 for trigrams.</param>
        /// <param name="minFrequency">Minimum frequency threshold for predictions.</param>
        public SequencePredictor(int ngramOrder = 2, int minFrequency = 3)
        {
            if (ngramOrder < 2 || ngramOrder > 3)
                throw new ArgumentOutOfRangeException(nameof(ngramOrder), "N-gram order must be 2 (bigrams) or 3 (trigrams)");

            if (minFrequency < 1)
                throw new ArgumentOutOfRangeException(nameof(minFrequency), "Minimum frequency must be at least 1");

            _ngramOrder = ngramOrder;
            _minFrequency = minFrequency;
            _cache = new ConcurrentDictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>(StringComparer.OrdinalIgnoreCase);
            _delta = new ConcurrentDictionary<string, ConcurrentDictionary<string, (int frequency, DateTime lastSeen)>>(StringComparer.OrdinalIgnoreCase);
            _cancellationToken = new CancellationTokenSource();
        }

        /// <summary>
        /// Initializes the cache from persisted data.
        /// </summary>
        public void Initialize(Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>> sequences)
        {
            if (sequences == null)
                throw new ArgumentNullException(nameof(sequences));

            lock (_lock)
            {
                _cache.Clear();
                foreach (var (prevCommand, nextCommands) in sequences)
                {
                    _cache[prevCommand] = new Dictionary<string, (int frequency, DateTime lastSeen)>(nextCommands, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>
        /// Starts the background cache refresh task (optional for async updating).
        /// </summary>
        public void StartBackgroundRefresh()
        {
            if (_backgroundRefreshTask != null)
                return;

            _backgroundRefreshTask = Task.Run(async () =>
            {
                while (!_cancellationToken.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Refresh cache every 100ms from delta
                        await Task.Delay(100, _cancellationToken.Token);

                        RefreshCacheFromDelta();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Swallow exceptions in background thread
                    }
                }
            }, _cancellationToken.Token);
        }

        /// <summary>
        /// Records a command sequence (called by FeedbackProvider).
        /// Updates delta for additive merging with database.
        /// </summary>
        public void RecordSequence(string[] recentCommands)
        {
            if (recentCommands == null || recentCommands.Length < 2)
                return;

            // For bigrams: record (prev, current) pairs
            // For trigrams: record (prev_prev + prev, current) as key
            for (int i = 1; i < recentCommands.Length; i++)
            {
                string prevKey;
                if (_ngramOrder == 2)
                {
                    // Bigram: just the previous command
                    prevKey = recentCommands[i - 1];
                }
                else
                {
                    // Trigram: two previous commands joined
                    if (i < 2)
                        continue;
                    prevKey = $"{recentCommands[i - 2]} && {recentCommands[i - 1]}";
                }

                var nextCommand = recentCommands[i];
                var timestamp = DateTime.UtcNow;

                // Update delta (for database persistence)
                var nextCmds = _delta.GetOrAdd(prevKey, _ => new ConcurrentDictionary<string, (int frequency, DateTime lastSeen)>(StringComparer.OrdinalIgnoreCase));

                nextCmds.AddOrUpdate(
                    nextCommand,
                    (1, timestamp),
                    (_, existing) => (existing.frequency + 1, timestamp > existing.lastSeen ? timestamp : existing.lastSeen)
                );

                // Also update cache immediately for inline predictions (no waiting for background refresh)
                var cacheNextCmds = _cache.GetOrAdd(prevKey, _ => new Dictionary<string, (int frequency, DateTime lastSeen)>(StringComparer.OrdinalIgnoreCase));

                lock (_lock)
                {
                    if (cacheNextCmds.TryGetValue(nextCommand, out var existing))
                    {
                        cacheNextCmds[nextCommand] = (existing.frequency + 1, timestamp > existing.lastSeen ? timestamp : existing.lastSeen);
                    }
                    else
                    {
                        cacheNextCmds[nextCommand] = (1, timestamp);
                    }
                }
            }
        }

        /// <summary>
        /// Gets predictions for the next command based on recent history.
        /// MUST complete in <1ms to fit within 20ms inline prediction budget.
        /// </summary>
        /// <param name="recentCommands">Recent command history (most recent last).</param>
        /// <returns>List of (nextCommand, score) sorted by score descending.</returns>
        public List<(string nextCommand, double score)> GetPredictions(string[] recentCommands)
        {
            if (recentCommands == null || recentCommands.Length == 0)
                return new List<(string nextCommand, double score)>();

            string prevKey;
            if (_ngramOrder == 2)
            {
                // Bigram: use last command
                prevKey = recentCommands[^1];
            }
            else
            {
                // Trigram: use last two commands
                if (recentCommands.Length < 2)
                    return new List<(string nextCommand, double score)>();
                prevKey = $"{recentCommands[^2]} && {recentCommands[^1]}";
            }

            // Fast cache lookup (target <1ms)
            if (!_cache.TryGetValue(prevKey, out var nextCommands))
                return new List<(string nextCommand, double score)>();

            // Calculate probabilities and filter by minFrequency
            var totalFrequency = nextCommands.Values.Sum(v => v.frequency);
            if (totalFrequency == 0)
                return new List<(string nextCommand, double score)>();

            var predictions = nextCommands
                .Where(kv => kv.Value.frequency >= _minFrequency)
                .Select(kv =>
                {
                    var probability = (double)kv.Value.frequency / totalFrequency;

                    // Apply recency boost (commands used recently get higher score)
                    var ageDays = (DateTime.UtcNow - kv.Value.lastSeen).TotalDays;
                    var recencyFactor = Math.Exp(-ageDays / 30.0); // 30-day decay

                    // Combined score: 70% probability + 30% recency
                    var score = (0.7 * probability) + (0.3 * recencyFactor);

                    return (nextCommand: kv.Key, score);
                })
                .OrderByDescending(p => p.score)
                .ToList();

            return predictions;
        }

        /// <summary>
        /// Gets the delta (new sequences since last save) for persistence.
        /// </summary>
        public Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>> GetDelta()
        {
            var result = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (prevCommand, nextCommands) in _delta)
            {
                result[prevCommand] = new Dictionary<string, (int frequency, DateTime lastSeen)>(StringComparer.OrdinalIgnoreCase);
                foreach (var (nextCommand, data) in nextCommands)
                {
                    result[prevCommand][nextCommand] = data;
                }
            }

            return result;
        }

        /// <summary>
        /// Clears the delta after successful persistence.
        /// </summary>
        public void ClearDelta()
        {
            _delta.Clear();
        }

        /// <summary>
        /// Refreshes the cache from delta (called by background task).
        /// </summary>
        private void RefreshCacheFromDelta()
        {
            if (_delta.IsEmpty)
                return;

            lock (_lock)
            {
                foreach (var (prevCommand, nextCommands) in _delta)
                {
                    var cacheNextCmds = _cache.GetOrAdd(prevCommand, _ => new Dictionary<string, (int frequency, DateTime lastSeen)>(StringComparer.OrdinalIgnoreCase));

                    foreach (var (nextCommand, data) in nextCommands)
                    {
                        if (cacheNextCmds.TryGetValue(nextCommand, out var existing))
                        {
                            cacheNextCmds[nextCommand] = (
                                existing.frequency + data.frequency,
                                data.lastSeen > existing.lastSeen ? data.lastSeen : existing.lastSeen
                            );
                        }
                        else
                        {
                            cacheNextCmds[nextCommand] = data;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets diagnostic information about the predictor state.
        /// </summary>
        public (int cacheEntries, int deltaEntries, int ngramOrder, int minFrequency) GetDiagnostics()
        {
            var cacheCount = _cache.Sum(kv => kv.Value.Count);
            var deltaCount = _delta.Sum(kv => kv.Value.Count);
            return (cacheCount, deltaCount, _ngramOrder, _minFrequency);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _cancellationToken?.Cancel();
            _backgroundRefreshTask?.Wait(TimeSpan.FromSeconds(1));
            _cancellationToken?.Dispose();
            _disposed = true;
        }
    }
}
