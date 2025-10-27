#pragma warning disable CA2252 // Opt into preview features for testing

using PSCue.Shared;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PSCue.Module.Tests;

/// <summary>
/// Integration tests for the IPC server that would have caught the filtering bugs.
/// These tests verify end-to-end behavior of caching and filtering.
/// </summary>
public class IpcServerIntegrationTests : IDisposable
{
    private readonly IpcServer _server;

    public IpcServerIntegrationTests()
    {
        _server = new IpcServer();
        // Give server time to start
        Thread.Sleep(100);
    }

    public void Dispose()
    {
        _server?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task IpcServer_FirstRequest_PopulatesCacheWithUnfilteredCompletions()
    {
        // This test would have caught Bug #2:
        // Cache was storing filtered completions instead of all completions

        // Arrange
        var request = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop h",
            WordToComplete = "h",
            IncludeDynamicArguments = false
        };

        // Act
        var response = await SendIpcRequestAsync(request);

        // Assert - Response should be filtered
        Assert.NotNull(response);
        Assert.NotEmpty(response.Completions);
        Assert.All(response.Completions, c =>
            Assert.StartsWith("h", c.Text, StringComparison.OrdinalIgnoreCase));

        // THE KEY ASSERTION: Cache should have ALL completions, not just filtered ones
        var cache = _server.GetCache();
        var cached = cache.TryGetCompletions("scoop");
        Assert.NotNull(cached);
        Assert.True(cached.Length >= 25,
            $"Cache should have all ~28 scoop commands, but had {cached.Length}");

        // Verify cache has more than just "h" commands
        var nonHCommands = cached.Where(c => !c.Text.StartsWith("h", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.NotEmpty(nonHCommands);
    }

    [Fact]
    public async Task IpcServer_CachedCompletions_AreFilteredByWordToComplete()
    {
        // This test would have caught Bug #1:
        // Cached completions weren't being filtered before returning

        // Arrange - First request populates cache
        var request1 = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop h",
            WordToComplete = "h",
            IncludeDynamicArguments = false
        };
        await SendIpcRequestAsync(request1);

        // Act - Second request with different prefix (from same cache)
        var request2 = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop u",
            WordToComplete = "u",
            IncludeDynamicArguments = false
        };
        var response2 = await SendIpcRequestAsync(request2);

        // Assert - Should get "u" completions, not "h" completions
        Assert.NotNull(response2);
        Assert.NotEmpty(response2.Completions);
        Assert.All(response2.Completions, c =>
            Assert.StartsWith("u", c.Text, StringComparison.OrdinalIgnoreCase));

        // Should be from cache
        Assert.True(response2.Cached);

        // Verify we get the expected "u" commands
        var texts = response2.Completions.Select(c => c.Text).ToArray();
        Assert.Contains("unhold", texts);
        Assert.Contains("uninstall", texts);
        Assert.Contains("update", texts);
    }

    [Fact]
    public async Task IpcServer_EmptyPrefix_ReturnsAllCachedCompletions()
    {
        // This is the exact bug scenario reported:
        // After "scoop h<tab>", then "scoop <tab>" was returning only "h" completions

        // Arrange - First request with "h" prefix
        var request1 = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop h",
            WordToComplete = "h",
            IncludeDynamicArguments = false
        };
        var response1 = await SendIpcRequestAsync(request1);

        Assert.Equal(3, response1.Completions.Length); // help, hold, home

        // Act - Second request with empty prefix
        var request2 = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop ",
            WordToComplete = "",
            IncludeDynamicArguments = false
        };
        var response2 = await SendIpcRequestAsync(request2);

        // Assert - Should return ALL completions, not just the 3 "h" ones
        Assert.NotNull(response2);
        Assert.True(response2.Completions.Length >= 25,
            $"Expected at least 25 scoop commands, got {response2.Completions.Length}");
        Assert.True(response2.Cached);

        // Verify we get completions from different prefixes
        var texts = response2.Completions.Select(c => c.Text).ToArray();
        Assert.Contains("help", texts);      // "h" command
        Assert.Contains("install", texts);   // "i" command
        Assert.Contains("search", texts);    // "s" command
        Assert.Contains("update", texts);    // "u" command
    }

    [Fact]
    public async Task IpcServer_DifferentCommands_UseDifferentCacheKeys()
    {
        // Arrange & Act
        var scoopRequest = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop h",
            WordToComplete = "h",
            IncludeDynamicArguments = false
        };
        await SendIpcRequestAsync(scoopRequest);

        var gitRequest = new IpcRequest
        {
            Command = "git",
            CommandLine = "git ch",
            WordToComplete = "ch",
            IncludeDynamicArguments = false
        };
        await SendIpcRequestAsync(gitRequest);

        // Assert - Should have separate cache entries
        var cache = _server.GetCache();
        var stats = cache.GetStatistics();
        Assert.Equal(2, stats.EntryCount);

        var scoopCached = cache.TryGetCompletions("scoop");
        var gitCached = cache.TryGetCompletions("git");

        Assert.NotNull(scoopCached);
        Assert.NotNull(gitCached);
        Assert.NotEqual(scoopCached.Length, gitCached.Length);
    }

    [Fact]
    public async Task IpcServer_ScoopUpdateWithTrailingSpace_ReturnsUpdateArguments()
    {
        // This test verifies the fix for the "scoop update <tab>" bug
        // When command line ends with a space, we should navigate into the subcommand
        // and return its arguments (parameters + dynamic arguments), not the parent's subcommands

        // Arrange
        var request = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop update ",  // Trailing space = completing next argument
            WordToComplete = "",
            IncludeDynamicArguments = false  // Don't actually call scoop list (slow)
        };

        // Act
        var response = await SendIpcRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Completions);

        // Should return the "*" parameter from the update command
        Assert.Contains(response.Completions, c => c.Text == "*");

        // Should NOT return scoop subcommands like "alias", "bucket", etc.
        Assert.DoesNotContain(response.Completions, c => c.Text == "alias");
        Assert.DoesNotContain(response.Completions, c => c.Text == "bucket");
        Assert.DoesNotContain(response.Completions, c => c.Text == "cache");
    }

    [Fact]
    public async Task IpcServer_ScoopUpdatePartialWord_ReturnsFilteredSubcommands()
    {
        // When there's NO trailing space, we're still completing the subcommand itself
        // "scoop upd" should return "update" (filtered subcommands)

        // Arrange
        var request = new IpcRequest
        {
            Command = "scoop",
            CommandLine = "scoop upd",  // No trailing space = completing this word
            WordToComplete = "upd",
            IncludeDynamicArguments = false
        };

        // Act
        var response = await SendIpcRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Completions);

        // Should return "update" (subcommand starting with "upd")
        var completionTexts = response.Completions.Select(c => c.Text).ToList();
        Assert.Contains("update", completionTexts);

        // Should NOT return subcommands that don't start with "upd"
        Assert.DoesNotContain("alias", completionTexts);
        Assert.DoesNotContain("bucket", completionTexts);
        Assert.DoesNotContain("unhold", completionTexts);  // starts with "un", not "upd"
    }

    [Fact]
    public async Task IpcServer_GitCheckoutWithTrailingSpace_ReturnsCheckoutArguments()
    {
        // Similar test for git - verify the fix works for other commands too

        // Arrange
        var request = new IpcRequest
        {
            Command = "git",
            CommandLine = "git checkout ",  // Trailing space
            WordToComplete = "",
            IncludeDynamicArguments = false  // Don't actually call git (slow)
        };

        // Act
        var response = await SendIpcRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Completions);

        // Should return parameters like "-b", "--track", etc.
        Assert.Contains(response.Completions, c => c.Text == "-b");

        // Should NOT return git subcommands like "add", "commit", etc.
        Assert.DoesNotContain(response.Completions, c => c.Text == "add");
        Assert.DoesNotContain(response.Completions, c => c.Text == "commit");
        Assert.DoesNotContain(response.Completions, c => c.Text == "push");
    }

    /// <summary>
    /// Helper to send IPC request and get response.
    /// This simulates what ArgumentCompleter does.
    /// </summary>
    private async Task<IpcResponse> SendIpcRequestAsync(IpcRequest request)
    {
        var pipeName = IpcProtocol.GetCurrentPipeName();

        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
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
        await client.ReadAsync(responseLengthBytes.AsMemory(0, 4));
        var responseLength = BitConverter.ToInt32(responseLengthBytes);

        var responseBytes = new byte[responseLength];
        await client.ReadAsync(responseBytes.AsMemory(0, responseLength));

        var responseJson = Encoding.UTF8.GetString(responseBytes);
        var response = JsonSerializer.Deserialize(responseJson, IpcJsonContext.Default.IpcResponse);

        return response ?? throw new InvalidOperationException("Failed to deserialize response");
    }
}
