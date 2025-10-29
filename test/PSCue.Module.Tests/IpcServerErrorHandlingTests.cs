#pragma warning disable CA2252 // Opt into preview features for testing

using PSCue.Shared;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests for IPC server error handling scenarios.
/// Verifies graceful degradation when things go wrong.
/// </summary>
public class IpcServerErrorHandlingTests : IDisposable
{
    private readonly IpcServer _server;
    private readonly string _pipeName;

    public IpcServerErrorHandlingTests()
    {
        _pipeName = $"PSCue-Test-{Guid.NewGuid():N}";
        _server = new IpcServer(_pipeName);
        Thread.Sleep(100); // Give server time to start
    }

    public void Dispose()
    {
        _server?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task IpcServer_MalformedRequest_DoesNotCrashServer()
    {
        // Test that sending garbage data doesn't crash the server
        // Arrange & Act - Send malformed data (invalid length prefix)
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(1000);

            var garbageData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00 };
            await client.WriteAsync(garbageData);
            await client.FlushAsync();

            // Give server time to reject and close
            await Task.Delay(100);
        }
        catch
        {
            // Expected: server may close connection
        }

        // Assert - Server should still be running and accept new valid requests
        var validRequest = new IpcRequest
        {
            Command = "git",
            CommandLine = "git ",
            WordToComplete = ""
        };

        using var client2 = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var response = await SendIpcRequestAsync(client2, validRequest);
        Assert.NotNull(response);
        Assert.NotEmpty(response.Completions);
    }

    [Fact]
    public async Task IpcServer_ExcessivelyLargePayload_IsRejected()
    {
        // Test that payloads larger than 1MB are rejected
        // Arrange & Act - Send length prefix indicating 2MB payload
        bool connectionClosed = false;
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(1000);

            var lengthBytes = BitConverter.GetBytes(2 * 1024 * 1024); // 2MB
            await client.WriteAsync(lengthBytes);
            await client.FlushAsync();

            // Give server time to reject
            await Task.Delay(100);

            // Try to read response (should fail because server rejected)
            var buffer = new byte[4];
            var bytesRead = await client.ReadAsync(buffer.AsMemory(0, 4));
            if (bytesRead == 0)
            {
                connectionClosed = true; // Server closed connection
            }
        }
        catch (IOException)
        {
            connectionClosed = true; // Connection was closed
        }

        // Assert - Server should reject (close connection or refuse to respond)
        Assert.True(connectionClosed, "Server should close connection for oversized payload");

        // Verify server is still running
        using var client2 = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var validRequest = new IpcRequest
        {
            Command = "git",
            CommandLine = "git ",
            WordToComplete = ""
        };
        var response = await SendIpcRequestAsync(client2, validRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task IpcServer_NegativeLengthPrefix_IsRejected()
    {
        // Test that negative length prefixes are rejected
        // Arrange
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);

        // Act - Send negative length prefix
        var lengthBytes = BitConverter.GetBytes(-100);
        await client.WriteAsync(lengthBytes);
        await client.FlushAsync();

        // Give server time to reject
        await Task.Delay(100);

        // Assert - Server should reject and remain functional
        using var client2 = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var validRequest = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop ",
            WordToComplete = ""
        };
        var response = await SendIpcRequestAsync(client2, validRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task IpcServer_InvalidJson_DoesNotCrashServer()
    {
        // Test that invalid JSON doesn't crash the server
        // Arrange
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);

        // Act - Send valid length but invalid JSON
        var invalidJson = "{ this is not valid json !!!";
        var jsonBytes = Encoding.UTF8.GetBytes(invalidJson);
        var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

        await client.WriteAsync(lengthBytes);
        await client.WriteAsync(jsonBytes);
        await client.FlushAsync();

        // Give server time to process
        await Task.Delay(100);

        // Assert - Server should still work
        using var client2 = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var validRequest = new IpcRequest
        {
            Command = "gh",
            CommandLine = "gh ",
            WordToComplete = ""
        };
        var response = await SendIpcRequestAsync(client2, validRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task IpcServer_MissingRequiredFields_DoesNotCrashServer()
    {
        // Test that JSON with missing fields doesn't crash the server
        // Arrange
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);

        // Act - Send JSON with missing required fields
        var invalidRequest = "{ \"Command\": \"git\" }"; // Missing CommandLine, WordToComplete
        var jsonBytes = Encoding.UTF8.GetBytes(invalidRequest);
        var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

        await client.WriteAsync(lengthBytes);
        await client.WriteAsync(jsonBytes);
        await client.FlushAsync();

        // Give server time to process
        await Task.Delay(100);

        // Assert - Server should still work
        using var client2 = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var validRequest = new IpcRequest
        {
            Command = "az",
            CommandLine = "az ",
            WordToComplete = ""
        };
        var response = await SendIpcRequestAsync(client2, validRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task IpcServer_ClientDisconnectsEarly_DoesNotCrashServer()
    {
        // Test that client disconnecting mid-request doesn't crash server
        // Arrange
        var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);

        // Act - Start sending request but disconnect early
        var lengthBytes = BitConverter.GetBytes(100);
        await client.WriteAsync(lengthBytes);
        // Disconnect without sending the payload
        client.Dispose();

        // Give server time to handle disconnect
        await Task.Delay(100);

        // Assert - Server should still work
        using var client2 = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var validRequest = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop ",
            WordToComplete = ""
        };
        var response = await SendIpcRequestAsync(client2, validRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task IpcServer_EmptyPayload_DoesNotCrashServer()
    {
        // Test that empty payload is handled gracefully
        // Arrange
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);

        // Act - Send zero-length payload
        var lengthBytes = BitConverter.GetBytes(0);
        await client.WriteAsync(lengthBytes);
        await client.FlushAsync();

        // Give server time to process
        await Task.Delay(100);

        // Assert - Server should still work
        using var client2 = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var validRequest = new IpcRequest
        {
            Command = "git",
            CommandLine = "git ",
            WordToComplete = ""
        };
        var response = await SendIpcRequestAsync(client2, validRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task IpcServer_DebugRequest_UnknownType_ReturnsError()
    {
        // Test that unknown debug request types return error (not crash)
        // Arrange
        var debugRequest = new IpcDebugRequest
        {
            RequestType = "unknown-invalid-type"
        };

        // Act
        var response = await SendDebugRequestAsync(debugRequest);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Contains("unknown", response.Message?.ToLower() ?? "");
    }

    [Fact]
    public async Task IpcServer_VeryLongCommandLine_IsHandledCorrectly()
    {
        // Test that very long command lines (but under 1MB limit) are handled
        // Arrange
        var longCommand = "git " + string.Join(' ', Enumerable.Repeat("--some-flag", 1000));
        var request = new IpcRequest
        {
            Command = "git",
            CommandLine = longCommand,
            WordToComplete = ""
        };

        // Act
        var response = await SendIpcRequestAsync(request);

        // Assert - Should handle it (even if completions are empty)
        Assert.NotNull(response);
    }

    [Fact]
    public async Task IpcServer_SpecialCharactersInRequest_AreHandledCorrectly()
    {
        // Test that special characters (Unicode, emoji, etc.) don't break parsing
        // Arrange
        var request = new IpcRequest
        {
            Command = "echo",
            CommandLine = "echo 你好世界 🚀",
            WordToComplete = "🚀"
        };

        // Act
        var response = await SendIpcRequestAsync(request);

        // Assert - Should not crash (completions may be empty for unknown command)
        Assert.NotNull(response);
    }

    /// <summary>
    /// Helper to send IPC request and get response.
    /// </summary>
    private async Task<IpcResponse> SendIpcRequestAsync(IpcRequest request)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        return await SendIpcRequestAsync(client, request);
    }

    /// <summary>
    /// Helper to send IPC request using existing client connection.
    /// </summary>
    private async Task<IpcResponse> SendIpcRequestAsync(NamedPipeClientStream client, IpcRequest request)
    {
        if (!client.IsConnected)
        {
            await client.ConnectAsync(1000);
        }

        // Send request
        var requestJson = JsonSerializer.Serialize(request, IpcJsonContext.Default.IpcRequest);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        var lengthBytes = BitConverter.GetBytes(requestBytes.Length);

        await client.WriteAsync(lengthBytes);
        await client.WriteAsync(requestBytes);
        await client.FlushAsync();

        // Read response
        var responseLengthBytes = new byte[4];
        await client.ReadExactlyAsync(responseLengthBytes.AsMemory(0, 4));
        var responseLength = BitConverter.ToInt32(responseLengthBytes);

        var responseBytes = new byte[responseLength];
        await client.ReadExactlyAsync(responseBytes.AsMemory(0, responseLength));

        var responseJson = Encoding.UTF8.GetString(responseBytes);
        var response = JsonSerializer.Deserialize(responseJson, IpcJsonContext.Default.IpcResponse);

        return response ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    /// <summary>
    /// Helper to send debug request and get response.
    /// </summary>
    private async Task<IpcDebugResponse> SendDebugRequestAsync(IpcDebugRequest request)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);

        // Send request with 'D' marker
        var requestJson = JsonSerializer.Serialize(request, IpcJsonContext.Default.IpcDebugRequest);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        var lengthBytes = BitConverter.GetBytes(requestBytes.Length);

        await client.WriteAsync(new byte[] { (byte)'D' }); // Debug marker
        await client.WriteAsync(lengthBytes);
        await client.WriteAsync(requestBytes);
        await client.FlushAsync();

        // Read response (with 'D' marker)
        var marker = new byte[1];
        await client.ReadExactlyAsync(marker.AsMemory(0, 1));
        Assert.Equal((byte)'D', marker[0]);

        var responseLengthBytes = new byte[4];
        await client.ReadExactlyAsync(responseLengthBytes.AsMemory(0, 4));
        var responseLength = BitConverter.ToInt32(responseLengthBytes);

        var responseBytes = new byte[responseLength];
        await client.ReadExactlyAsync(responseBytes.AsMemory(0, responseLength));

        var responseJson = Encoding.UTF8.GetString(responseBytes);
        var response = JsonSerializer.Deserialize(responseJson, IpcJsonContext.Default.IpcDebugResponse);

        return response ?? throw new InvalidOperationException("Failed to deserialize debug response");
    }
}
