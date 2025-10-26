using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PSCue.Shared;

namespace PSCue.Module;

/// <summary>
/// Named Pipe server that provides completion suggestions to ArgumentCompleter clients.
/// Runs in the CommandPredictor (long-lived) and serves requests from ArgumentCompleter (short-lived).
/// </summary>
public class IpcServer : IDisposable
{
    private readonly CompletionCache _cache;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _serverTask;
    private readonly string _pipeName;
    private bool _disposed;

    public IpcServer()
    {
        _cache = new CompletionCache();
        _cancellationTokenSource = new CancellationTokenSource();
        _pipeName = IpcProtocol.GetCurrentPipeName();

        // Start the server loop in a background task
        _serverTask = Task.Run(ServerLoopAsync);
    }

    /// <summary>
    /// Main server loop that listens for client connections.
    /// </summary>
    private async Task ServerLoopAsync()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Create a new pipe server for each connection
                await using var pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                // Wait for a client to connect
                await pipeServer.WaitForConnectionAsync(_cancellationTokenSource.Token);

                // Handle the request (fire and forget - we'll create a new server for the next connection)
                _ = Task.Run(() => HandleClientAsync(pipeServer), _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            // Log error but don't crash the server
            await Console.Error.WriteLineAsync($"IpcServer error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle a single client request.
    /// </summary>
    private async Task HandleClientAsync(NamedPipeServerStream pipeServer)
    {
        try
        {
            // Peek at the first byte to determine request type
            var peekBuffer = new byte[1];
            var bytesRead = await pipeServer.ReadAsync(peekBuffer.AsMemory(0, 1));
            if (bytesRead != 1)
            {
                return;
            }

            // Check if this is a debug request (starts with 'D') or completion request (starts with length prefix)
            if (peekBuffer[0] == (byte)'D')
            {
                await HandleDebugRequestAsync(pipeServer);
            }
            else
            {
                // Put the byte back conceptually by reading the rest of the length prefix
                var lengthBuffer = new byte[4];
                lengthBuffer[0] = peekBuffer[0];
                bytesRead = await pipeServer.ReadAsync(lengthBuffer.AsMemory(1, 3));
                if (bytesRead != 3)
                {
                    return;
                }

                var length = BitConverter.ToInt32(lengthBuffer, 0);
                if (length is <= 0 or > 1024 * 1024) // Max 1MB
                {
                    return;
                }

                // Read the JSON payload
                var buffer = new byte[length];
                bytesRead = await pipeServer.ReadAsync(buffer.AsMemory(0, length));
                if (bytesRead != length)
                {
                    return;
                }

                var json = Encoding.UTF8.GetString(buffer);
                var request = JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcRequest);
                if (request == null)
                {
                    return;
                }

                await HandleCompletionRequestAsync(pipeServer, request);
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error handling client request: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle a completion request.
    /// </summary>
    private async Task HandleCompletionRequestAsync(NamedPipeServerStream pipeServer, IpcRequest request)
    {
        // Generate cache key
        var cacheKey = CompletionCache.GetCacheKey(request.Command, request.CommandLine);

        // Try to get from cache first
        var cachedCompletions = _cache.TryGetCompletions(cacheKey);
        CompletionItem[] completions;
        bool fromCache;

        if (cachedCompletions != null)
        {
            completions = cachedCompletions;
            fromCache = true;
        }
        else
        {
            // Generate fresh completions
            completions = GenerateCompletions(request);
            _cache.SetCompletions(cacheKey, completions);
            fromCache = false;
        }

        // Create response
        var response = new IpcResponse
        {
            Completions = completions,
            Cached = fromCache,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Send response
        await WriteResponseAsync(pipeServer, response);
    }

    /// <summary>
    /// Handle a debug request (stats, cache inspection, ping).
    /// </summary>
    private async Task HandleDebugRequestAsync(NamedPipeServerStream pipeServer)
    {
        try
        {
            // Read the 'D' marker that was already consumed
            // Now read the length prefix (4 bytes)
            var lengthBuffer = new byte[4];
            var bytesRead = await pipeServer.ReadAsync(lengthBuffer.AsMemory(0, 4));
            if (bytesRead != 4)
            {
                return;
            }

            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length is <= 0 or > 1024 * 1024)
            {
                return;
            }

            // Read JSON payload
            var buffer = new byte[length];
            bytesRead = await pipeServer.ReadAsync(buffer.AsMemory(0, length));
            if (bytesRead != length)
            {
                return;
            }

            var json = Encoding.UTF8.GetString(buffer);
            var request = JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcDebugRequest);
            if (request == null)
            {
                return;
            }

            // Handle different debug request types
            IpcDebugResponse response = request.RequestType switch
            {
                "ping" => new IpcDebugResponse { Success = true, Message = "pong" },
                "stats" => GetStatsResponse(),
                "cache" => GetCacheResponse(request.Filter),
                _ => new IpcDebugResponse { Success = false, Message = $"Unknown request type: {request.RequestType}" }
            };

            // Send response (with 'D' marker)
            await WriteDebugResponseAsync(pipeServer, response);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error handling debug request: {ex.Message}");
        }
    }

    /// <summary>
    /// Get cache statistics response.
    /// </summary>
    private IpcDebugResponse GetStatsResponse()
    {
        var stats = _cache.GetStatistics();
        var age = DateTime.UtcNow - stats.OldestEntry;

        return new IpcDebugResponse
        {
            Success = true,
            Stats = new CacheStats
            {
                EntryCount = stats.EntryCount,
                TotalHits = stats.TotalHits,
                OldestEntryAge = age.TotalSeconds < 60
                    ? $"{age.TotalSeconds:F1}s"
                    : $"{age.TotalMinutes:F1}m"
            }
        };
    }

    /// <summary>
    /// Get cache entries response.
    /// </summary>
    private IpcDebugResponse GetCacheResponse(string? filter)
    {
        var entries = _cache.GetCacheEntries(filter);
        var cacheEntries = entries.Select(e => new CacheEntryInfo
        {
            Key = e.Key,
            CompletionCount = e.Completions.Length,
            HitCount = e.HitCount,
            Age = e.Age.TotalSeconds < 60
                ? $"{e.Age.TotalSeconds:F1}s"
                : $"{e.Age.TotalMinutes:F1}m",
            TopCompletions = e.Completions.Take(5).ToArray()
        }).ToArray();

        return new IpcDebugResponse
        {
            Success = true,
            CacheEntries = cacheEntries
        };
    }

    /// <summary>
    /// Write an IPC response to the pipe stream.
    /// </summary>
    private static async Task WriteResponseAsync(PipeStream pipe, IpcResponse response)
    {
        try
        {
            var json = JsonSerializer.Serialize(response, IpcJsonContext.Default.IpcResponse);
            var buffer = Encoding.UTF8.GetBytes(json);

            // Write length prefix (4 bytes)
            var lengthBuffer = BitConverter.GetBytes(buffer.Length);
            await pipe.WriteAsync(lengthBuffer.AsMemory(0, 4));

            // Write JSON payload
            await pipe.WriteAsync(buffer.AsMemory(0, buffer.Length));
            await pipe.FlushAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error writing response: {ex.Message}");
        }
    }

    /// <summary>
    /// Write a debug response to the pipe stream (with 'D' marker).
    /// </summary>
    private static async Task WriteDebugResponseAsync(PipeStream pipe, IpcDebugResponse response)
    {
        try
        {
            var json = JsonSerializer.Serialize(response, IpcJsonContext.Default.IpcDebugResponse);
            var buffer = Encoding.UTF8.GetBytes(json);

            // Write 'D' marker (1 byte)
            await pipe.WriteAsync(new byte[] { (byte)'D' }.AsMemory(0, 1));

            // Write length prefix (4 bytes)
            var lengthBuffer = BitConverter.GetBytes(buffer.Length);
            await pipe.WriteAsync(lengthBuffer.AsMemory(0, 4));

            // Write JSON payload
            await pipe.WriteAsync(buffer.AsMemory(0, buffer.Length));
            await pipe.FlushAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error writing debug response: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate completion suggestions using the shared completion logic.
    /// </summary>
    private static CompletionItem[] GenerateCompletions(IpcRequest request)
    {
        try
        {
            // Use the existing CommandCompleter logic from PSCue.Shared
            // Pass includeDynamicArguments from the request (ArgumentCompleter wants all, Predictor wants fast)
            var completions = CommandCompleter.GetCompletions(
                request.CommandLine.AsSpan(),
                request.WordToComplete.AsSpan(),
                request.IncludeDynamicArguments);

            return completions
                .Select(c => new CompletionItem
                {
                    Text = c.CompletionText,
                    Description = c.Tooltip,
                    Score = 0.0 // Initial score, will be updated by usage tracking
                })
                .ToArray();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating completions: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Get the completion cache (for testing and feedback provider).
    /// </summary>
    public CompletionCache GetCache() => _cache;

    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource.Cancel();
            try
            {
                _serverTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions during shutdown
            }

            _cancellationTokenSource.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
