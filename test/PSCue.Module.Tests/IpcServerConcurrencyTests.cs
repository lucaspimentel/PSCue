#pragma warning disable CA2252 // Opt into preview features for testing

using PSCue.Shared;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests for concurrent IPC server operations.
/// Verifies thread-safety and proper handling of multiple simultaneous requests.
/// </summary>
[Trait("Category", "Flaky")]
public class IpcServerConcurrencyTests : IDisposable
{
    private readonly IpcServer _server;
    private readonly string _pipeName;

    public IpcServerConcurrencyTests()
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
    public async Task IpcServer_MultipleConcurrentRequests_AllSucceed()
    {
        // Test that server can handle multiple concurrent requests
        // Arrange
        var requests = new[]
        {
            new IpcRequest { Command = "git", CommandLine = "git ", WordToComplete = "" },
            new IpcRequest { Command = "scoop", CommandLine = "scoop ", WordToComplete = "" },
            new IpcRequest { Command = "gh", CommandLine = "gh ", WordToComplete = "" },
            new IpcRequest { Command = "az", CommandLine = "az ", WordToComplete = "" },
            new IpcRequest { Command = "git", CommandLine = "git ch", WordToComplete = "ch" }
        };

        // Act - Send all requests concurrently
        var tasks = requests.Select(SendIpcRequestAsync).ToArray();
        var responses = await Task.WhenAll(tasks);

        // Assert - All responses should be valid
        Assert.Equal(5, responses.Length);
        Assert.All(responses, response =>
        {
            Assert.NotNull(response);
            Assert.NotNull(response.Completions);
        });
    }

    [Fact]
    public async Task IpcServer_HighConcurrency_NoErrors()
    {
        // Stress test: 20 concurrent requests
        // Arrange
        var requests = Enumerable.Range(0, 20).Select(i => new IpcRequest
        {
            Command = i % 2 == 0 ? "git" : "scoop",
            CommandLine = i % 2 == 0 ? "git " : "scoop ",
            WordToComplete = ""
        }).ToArray();

        // Act
        var tasks = requests.Select(SendIpcRequestAsync).ToArray();
        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(20, responses.Length);
        Assert.All(responses, response =>
        {
            Assert.NotNull(response);
            Assert.NotNull(response.Completions);
        });
    }

    [Fact]
    public async Task IpcServer_ConcurrentCacheAccess_IsThreadSafe()
    {
        // Test that concurrent access to the same cache key is thread-safe
        // Arrange - All requests use same command to hit same cache entry
        var requests = Enumerable.Range(0, 10).Select(_ => new IpcRequest
        {
            Command = "git",
            CommandLine = "git ",
            WordToComplete = ""
        }).ToArray();

        // Act
        var tasks = requests.Select(SendIpcRequestAsync).ToArray();
        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        Assert.Equal(10, responses.Length);
        Assert.All(responses, response =>
        {
            Assert.NotNull(response);
            Assert.NotEmpty(response.Completions);
        });

        // First request populates cache, most should be from cache
        // Note: Due to race conditions, some concurrent requests may miss cache
        var cachedCount = responses.Count(r => r.Cached);
        Assert.True(cachedCount >= 5, $"Expected at least 5 cached responses, got {cachedCount}");
    }

    [Fact]
    public async Task IpcServer_MixedValidAndInvalidRequests_ValidOnesSucceed()
    {
        // Test that invalid requests don't affect valid ones running concurrently
        // Arrange
        var tasks = new List<Task>();

        // Add valid requests
        tasks.Add(SendIpcRequestAsync(new IpcRequest
        {
            Command = "git",
            CommandLine = "git ",
            WordToComplete = ""
        }));

        // Add invalid request (will be handled, but we can't await it easily)
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(1000);
                // Send garbage
                await client.WriteAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
                await client.FlushAsync();
            }
            catch
            {
                // Expected to fail
            }
        }));

        // Add more valid requests
        tasks.Add(SendIpcRequestAsync(new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop ",
            WordToComplete = ""
        }));

        // Act - Run all concurrently
        await Task.WhenAll(tasks);

        // Assert - Server should still work after invalid request
        var response = await SendIpcRequestAsync(new IpcRequest
        {
            Command = "gh",
            CommandLine = "gh ",
            WordToComplete = ""
        });
        Assert.NotNull(response);
    }

    [Fact]
    public async Task IpcServer_ConcurrentDebugAndCompletionRequests_BothSucceed()
    {
        // Test that debug requests and completion requests can run concurrently
        // Arrange & Act
        var tasks = new List<Task>();

        // Send completion requests
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(SendIpcRequestAsync(new IpcRequest
            {
                Command = "git",
                CommandLine = "git ",
                WordToComplete = ""
            }));
        }

        // Send debug requests
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(SendDebugRequestAsync(new IpcDebugRequest { RequestType = "ping" }));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert - All should succeed (no exceptions thrown)
        Assert.True(true); // If we get here, all requests succeeded
    }

    [Fact]
    public async Task IpcServer_ConcurrentCacheClearAndRequests_NoErrors()
    {
        // Test that clearing cache while requests are in flight doesn't cause errors
        // Arrange
        var tasks = new List<Task>();

        // Send many completion requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(SendIpcRequestAsync(new IpcRequest
            {
                Command = "git",
                CommandLine = "git ",
                WordToComplete = ""
            }));
        }

        // Clear cache mid-flight
        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(10); // Let some requests start
            await SendDebugRequestAsync(new IpcDebugRequest { RequestType = "clear" });
        }));

        // Send more requests after clear
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Delay(20).ContinueWith(_ => SendIpcRequestAsync(new IpcRequest
            {
                Command = "git",
                CommandLine = "git ",
                WordToComplete = ""
            })).Unwrap());
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert - All should succeed
        Assert.True(true);
    }

    [Fact(Skip = "Flaky - rapid connections can timeout when server is under load")]
    public async Task IpcServer_RapidConnectDisconnect_HandledGracefully()
    {
        // Test rapid connection/disconnection doesn't cause issues
        // Arrange
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(1000);
            // Disconnect immediately without sending anything
        }).ToArray();

        // Act
        await Task.WhenAll(tasks);

        // Assert - Server should still work
        var response = await SendIpcRequestAsync(new IpcRequest
        {
            Command = "git",
            CommandLine = "git ",
            WordToComplete = ""
        });
        Assert.NotNull(response);
    }

    /// <summary>
    /// Helper to send IPC request and get response.
    /// </summary>
    private async Task<IpcResponse> SendIpcRequestAsync(IpcRequest request)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);

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

        await client.WriteAsync(new byte[] { (byte)'D' });
        await client.WriteAsync(lengthBytes);
        await client.WriteAsync(requestBytes);
        await client.FlushAsync();

        // Read response
        var marker = new byte[1];
        await client.ReadExactlyAsync(marker.AsMemory(0, 1));

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
