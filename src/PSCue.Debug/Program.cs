using System.Diagnostics;
using System.IO.Pipes;
using System.Management.Automation.Subsystem.Prediction;
using System.Text;
using System.Text.Json;
using PSCue.Module;
using PSCue.Shared;

namespace PSCue.Debug;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "query" => await HandleQueryCommand(args),
                "stats" => await HandleStatsCommand(),
                "cache" => await HandleCacheCommand(args),
                "ping" => await HandlePingCommand(),
                "help" or "--help" or "-h" => ShowUsage(),
                _ => HandleQueryCommand(args).GetAwaiter().GetResult()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static int ShowUsage()
    {
        Console.WriteLine("PSCue Debug Tool - Test and inspect PSCue completion cache");
        Console.WriteLine();
        Console.WriteLine("Usage: pscue-debug <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  query <input>       Get completion suggestions for input (tests GetSuggestion)");
        Console.WriteLine("  stats               Show cache statistics");
        Console.WriteLine("  cache [--filter]    Inspect cached completions (optionally filtered)");
        Console.WriteLine("  ping                Test IPC server connectivity");
        Console.WriteLine("  help                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  pscue-debug query \"git checkout ma\"");
        Console.WriteLine("  pscue-debug stats");
        Console.WriteLine("  pscue-debug cache --filter git");
        Console.WriteLine("  pscue-debug ping");
        return 0;
    }

    /// <summary>
    /// Handle 'query' command - tests GetSuggestion like the old behavior
    /// </summary>
    static async Task<int> HandleQueryCommand(string[] args)
    {
        string input;
        if (args[0].ToLowerInvariant() == "query")
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Error: 'query' command requires an input string");
                return 1;
            }
            input = args[1];
        }
        else
        {
            // Backward compatibility: if first arg isn't a command, treat it as input
            input = args[0];
        }

        var predictionContext = PredictionContext.Create(input);
        var predictionClient = new PredictionClient("PSCue.Debug", PredictionClientKind.Terminal);

        var predictor = new CommandPredictor();
        var suggestionPackage = predictor.GetSuggestion(predictionClient, predictionContext, CancellationToken.None);

        if (suggestionPackage.SuggestionEntries != null && suggestionPackage.SuggestionEntries.Count > 0)
        {
            Console.WriteLine($"Found {suggestionPackage.SuggestionEntries.Count} suggestions:");
            foreach (var suggestion in suggestionPackage.SuggestionEntries)
            {
                Console.WriteLine($"  {suggestion.SuggestionText}");
            }
            return 0;
        }
        else
        {
            Console.WriteLine("(no suggestions)");
            return 0;
        }
    }

    /// <summary>
    /// Handle 'stats' command - show cache statistics via IPC
    /// </summary>
    static async Task<int> HandleStatsCommand()
    {
        var response = await SendDebugRequest(new IpcDebugRequest { RequestType = "stats" });

        if (response == null)
        {
            Console.Error.WriteLine("Failed to connect to IPC server. Is PSCue loaded?");
            return 1;
        }

        if (!response.Success)
        {
            Console.Error.WriteLine($"Error: {response.Message}");
            return 1;
        }

        if (response.Stats == null)
        {
            Console.Error.WriteLine("Error: No stats returned");
            return 1;
        }

        Console.WriteLine("Cache Statistics:");
        Console.WriteLine($"  Entry Count:       {response.Stats.EntryCount}");
        Console.WriteLine($"  Total Hits:        {response.Stats.TotalHits}");
        Console.WriteLine($"  Oldest Entry Age:  {response.Stats.OldestEntryAge}");
        return 0;
    }

    /// <summary>
    /// Handle 'cache' command - inspect cached completions
    /// </summary>
    static async Task<int> HandleCacheCommand(string[] args)
    {
        string? filter = null;

        // Look for --filter argument
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--filter" && i + 1 < args.Length)
            {
                filter = args[i + 1];
                break;
            }
        }

        var response = await SendDebugRequest(new IpcDebugRequest
        {
            RequestType = "cache",
            Filter = filter
        });

        if (response == null)
        {
            Console.Error.WriteLine("Failed to connect to IPC server. Is PSCue loaded?");
            return 1;
        }

        if (!response.Success)
        {
            Console.Error.WriteLine($"Error: {response.Message}");
            return 1;
        }

        if (response.CacheEntries == null || response.CacheEntries.Length == 0)
        {
            Console.WriteLine("No cache entries found.");
            return 0;
        }

        Console.WriteLine($"Cache Entries ({response.CacheEntries.Length}):");
        Console.WriteLine();

        foreach (var entry in response.CacheEntries)
        {
            Console.WriteLine($"Key: {entry.Key}");
            Console.WriteLine($"  Completions: {entry.CompletionCount}");
            Console.WriteLine($"  Hit Count:   {entry.HitCount}");
            Console.WriteLine($"  Age:         {entry.Age}");

            if (entry.TopCompletions.Length > 0)
            {
                Console.WriteLine("  Top Completions:");
                foreach (var completion in entry.TopCompletions)
                {
                    Console.WriteLine($"    - {completion.Text} (score: {completion.Score:F2})");
                }
            }
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// Handle 'ping' command - test IPC connectivity
    /// </summary>
    static async Task<int> HandlePingCommand()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await SendDebugRequest(new IpcDebugRequest { RequestType = "ping" });
        stopwatch.Stop();

        if (response == null)
        {
            Console.WriteLine("FAIL: Could not connect to IPC server");
            Console.WriteLine("Is PSCue loaded in your PowerShell session?");
            return 1;
        }

        if (!response.Success)
        {
            Console.WriteLine($"FAIL: {response.Message}");
            return 1;
        }

        Console.WriteLine($"OK: {response.Message} (round-trip: {stopwatch.ElapsedMilliseconds}ms)");
        return 0;
    }

    /// <summary>
    /// Send a debug request to the IPC server
    /// </summary>
    static async Task<IpcDebugResponse?> SendDebugRequest(IpcDebugRequest request)
    {
        try
        {
            // Find the PowerShell process that has the IPC server
            // Try current process first, then parent
            var processIds = new[] { Environment.ProcessId, GetParentProcessId() };

            foreach (var pid in processIds)
            {
                if (pid == 0) continue;

                var pipeName = IpcProtocol.GetPipeName(pid);

                try
                {
                    var response = await SendDebugRequestToPipe(pipeName, request);
                    if (response != null)
                    {
                        return response;
                    }
                }
                catch (TimeoutException)
                {
                    // Try next process
                    continue;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"IPC error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Send a debug request to a specific named pipe
    /// </summary>
    static async Task<IpcDebugResponse?> SendDebugRequestToPipe(string pipeName, IpcDebugRequest request)
    {
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        // Try to connect with a short timeout
        var connectTask = client.ConnectAsync(CancellationToken.None);
        var timeoutTask = Task.Delay(100);

        if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
        {
            throw new TimeoutException();
        }

        // Send debug request with 'D' marker
        var json = JsonSerializer.Serialize(request, IpcJsonContext.Default.IpcDebugRequest);
        var buffer = Encoding.UTF8.GetBytes(json);

        // Write 'D' marker
        await client.WriteAsync(new byte[] { (byte)'D' }.AsMemory(0, 1));

        // Write length prefix
        var lengthBuffer = BitConverter.GetBytes(buffer.Length);
        await client.WriteAsync(lengthBuffer.AsMemory(0, 4));

        // Write JSON payload
        await client.WriteAsync(buffer.AsMemory(0, buffer.Length));
        await client.FlushAsync();

        // Read response (with 'D' marker)
        var markerBuffer = new byte[1];
        var bytesRead = await client.ReadAsync(markerBuffer.AsMemory(0, 1));
        if (bytesRead != 1 || markerBuffer[0] != (byte)'D')
        {
            return null;
        }

        // Read length prefix
        var responseLengthBuffer = new byte[4];
        bytesRead = await client.ReadAsync(responseLengthBuffer.AsMemory(0, 4));
        if (bytesRead != 4)
        {
            return null;
        }

        var responseLength = BitConverter.ToInt32(responseLengthBuffer, 0);
        if (responseLength is <= 0 or > 1024 * 1024)
        {
            return null;
        }

        // Read JSON payload
        var responseBuffer = new byte[responseLength];
        bytesRead = await client.ReadAsync(responseBuffer.AsMemory(0, responseLength));
        if (bytesRead != responseLength)
        {
            return null;
        }

        var responseJson = Encoding.UTF8.GetString(responseBuffer);
        return JsonSerializer.Deserialize(responseJson, IpcJsonContext.Default.IpcDebugResponse);
    }

    /// <summary>
    /// Get the parent process ID (PowerShell session)
    /// </summary>
    static int GetParentProcessId()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            // On Windows, we can try to get the parent process
            // This is a simple heuristic - in production you might want WMI or native calls
            return 0; // For now, just return 0 (will be skipped)
        }
        catch
        {
            return 0;
        }
    }
}
