using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PSCue.Shared;

namespace PSCue.ArgumentCompleter;

/// <summary>
/// Named Pipe client that requests completion suggestions from the CommandPredictor server.
/// Used by ArgumentCompleter (short-lived) to communicate with CommandPredictor (long-lived).
/// </summary>
public static class IpcClient
{
    /// <summary>
    /// Try to get completions from the IPC server.
    /// Returns null if the server is unavailable or times out.
    /// </summary>
    public static async Task<IpcResponse?> TryGetCompletionsAsync(string command, string commandLine, string wordToComplete, int cursorPosition)
    {
        try
        {
            var pipeName = IpcProtocol.GetCurrentPipeName();

            await using var pipeClient = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            // Try to connect with a short timeout
            using var cts = new CancellationTokenSource(IpcProtocol.ConnectionTimeoutMs);
            try
            {
                await pipeClient.ConnectAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout - server not available
                return null;
            }

            // Create request
            var request = new IpcRequest
            {
                Command = command,
                CommandLine = commandLine,
                WordToComplete = wordToComplete,
                CursorPosition = cursorPosition,
                Args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            };

            // Send request
            await WriteRequestAsync(pipeClient, request);

            // Read response with timeout
            using var responseCts = new CancellationTokenSource(IpcProtocol.ResponseTimeoutMs);
            try
            {
                return await ReadResponseAsync(pipeClient, responseCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout waiting for response
                return null;
            }
        }
        catch (Exception ex)
        {
            // IPC not available - fall back to local logic
            Logger.Write($"IPC error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Write an IPC request to the pipe stream.
    /// </summary>
    private static async Task WriteRequestAsync(PipeStream pipe, IpcRequest request)
    {
        var json = JsonSerializer.Serialize(request, IpcJsonContext.Default.IpcRequest);
        var buffer = Encoding.UTF8.GetBytes(json);

        // Write length prefix (4 bytes)
        var lengthBuffer = BitConverter.GetBytes(buffer.Length);
        await pipe.WriteAsync(lengthBuffer, 0, 4);

        // Write JSON payload
        await pipe.WriteAsync(buffer, 0, buffer.Length);
        await pipe.FlushAsync();
    }

    /// <summary>
    /// Read an IPC response from the pipe stream.
    /// </summary>
    private static async Task<IpcResponse?> ReadResponseAsync(PipeStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            // Read length prefix (4 bytes)
            var lengthBuffer = new byte[4];
            var bytesRead = await pipe.ReadAsync(lengthBuffer, 0, 4, cancellationToken);
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
            bytesRead = await pipe.ReadAsync(buffer, 0, length, cancellationToken);
            if (bytesRead != length)
            {
                return null;
            }

            var json = Encoding.UTF8.GetString(buffer);
            return JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcResponse);
        }
        catch (Exception ex)
        {
            Logger.Write($"Error reading response: {ex.Message}");
            return null;
        }
    }
}
