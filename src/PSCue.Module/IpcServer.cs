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
                using var pipeServer = new NamedPipeServerStream(
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
            Console.Error.WriteLine($"IpcServer error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle a single client request.
    /// </summary>
    private async Task HandleClientAsync(NamedPipeServerStream pipeServer)
    {
        try
        {
            // Read the request
            var request = await ReadRequestAsync(pipeServer);
            if (request == null)
            {
                return;
            }

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
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error handling client request: {ex.Message}");
        }
    }

    /// <summary>
    /// Read an IPC request from the pipe stream.
    /// </summary>
    private static async Task<IpcRequest?> ReadRequestAsync(PipeStream pipe)
    {
        try
        {
            // Read length prefix (4 bytes)
            var lengthBuffer = new byte[4];
            var bytesRead = await pipe.ReadAsync(lengthBuffer.AsMemory(0, 4));
            if (bytesRead != 4)
            {
                return null;
            }

            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > 1024 * 1024) // Max 1MB
            {
                return null;
            }

            // Read JSON payload
            var buffer = new byte[length];
            bytesRead = await pipe.ReadAsync(buffer.AsMemory(0, length));
            if (bytesRead != length)
            {
                return null;
            }

            var json = Encoding.UTF8.GetString(buffer);
            return JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcRequest);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading request: {ex.Message}");
            return null;
        }
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
            Console.Error.WriteLine($"Error writing response: {ex.Message}");
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
            var completions = CommandCompleter.GetCompletions(
                request.CommandLine.AsSpan(),
                request.WordToComplete.AsSpan());

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
            return Array.Empty<CompletionItem>();
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
        }
    }
}
