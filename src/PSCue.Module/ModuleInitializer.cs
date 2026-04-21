using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;
using System.Management.Automation.Subsystem.Feedback;
using System.Threading;

namespace PSCue.Module;

// https://adamtheautomator.com/psreadline/
// https://learn.microsoft.com/en-us/powershell/scripting/learn/shell/using-predictors
// https://learn.microsoft.com/en-us/powershell/scripting/dev-cross-plat/create-cmdline-predictor
// https://learn.microsoft.com/en-us/powershell/scripting/dev-cross-plat/create-feedback-provider

/// <summary>
/// Handles module lifecycle: initialization and cleanup.
/// Registers predictors and feedback providers with PowerShell.
/// </summary>
public class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    // Captured when the type is first touched (just before OnImport runs) — upper bound on JIT + assembly load.
    public static readonly DateTime AssemblyLoadedUtc = DateTime.UtcNow;

    private readonly List<(SubsystemKind Kind, Guid Id)> _subsystems = [];
    private static System.Threading.Timer? _autoSaveTimer;
    private static CancellationTokenSource? _initCts;
    private static Task? _initTask;

    /// <summary>
    /// Gets called when assembly is loaded.
    /// Registers subsystems synchronously (required by PowerShell), then loads
    /// learned data in the background so the module returns to the prompt instantly.
    /// </summary>
    public void OnImport()
    {
        var total = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();

        PSCue.Shared.Logger.Write($"IMPORT [marker] assembly_ctor_utc={AssemblyLoadedUtc:HH:mm:ss.ffff}");

        RegisterCommandPredictor(new CommandPredictor());
        PSCue.Shared.Logger.Write($"IMPORT [phase] RegisterCommandPredictor={sw.ElapsedMilliseconds}ms");
        sw.Restart();

        RegisterFeedbackProvider(new FeedbackProvider());
        PSCue.Shared.Logger.Write($"IMPORT [phase] RegisterFeedbackProvider={sw.ElapsedMilliseconds}ms");
        sw.Restart();

        var enableGenericLearning = Environment.GetEnvironmentVariable("PSCUE_DISABLE_LEARNING")?.Equals("true", StringComparison.OrdinalIgnoreCase) != true;

        if (enableGenericLearning)
        {
            var config = ReadConfiguration();
            PSCue.Shared.Logger.Write($"IMPORT [phase] ReadConfiguration={sw.ElapsedMilliseconds}ms");
            sw.Restart();

            _initCts = new CancellationTokenSource();
            _initTask = Task.Run(() => InitializeInBackground(config, _initCts.Token));
            PSCue.Shared.Logger.Write($"IMPORT [phase] BackgroundTaskSpawn={sw.ElapsedMilliseconds}ms");
        }

        PSCue.Shared.Logger.Write($"IMPORT [total-sync] OnImport={total.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Reads all configuration from environment variables. Fast (no I/O), runs on the main thread
    /// so env vars are captured at import time.
    /// </summary>
    private static InitConfiguration ReadConfiguration()
    {
        return new InitConfiguration
        {
            HistorySize = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_HISTORY_SIZE"), out var hs) ? hs : 100,
            MaxCommands = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_MAX_COMMANDS"), out var mc) ? mc : 500,
            MaxArgs = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_MAX_ARGS_PER_CMD"), out var ma) ? ma : 100,
            DecayDays = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_DECAY_DAYS"), out var dd) ? dd : 30,
            MlEnabled = Environment.GetEnvironmentVariable("PSCUE_ML_ENABLED")?.Equals("false", StringComparison.OrdinalIgnoreCase) != true,
            NgramOrder = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_ML_NGRAM_ORDER"), out var no) ? no : 2,
            NgramMinFreq = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_ML_NGRAM_MIN_FREQ"), out var mf) ? mf : 3,
            WorkflowEnabled = Environment.GetEnvironmentVariable("PSCUE_WORKFLOW_LEARNING")?.Equals("false", StringComparison.OrdinalIgnoreCase) != true,
            WorkflowMinFreq = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_WORKFLOW_MIN_FREQUENCY"), out var wf) ? wf : 5,
            WorkflowMaxTime = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_WORKFLOW_MAX_TIME_DELTA"), out var wt) ? wt : 15,
            WorkflowMinConf = double.TryParse(Environment.GetEnvironmentVariable("PSCUE_WORKFLOW_MIN_CONFIDENCE"), out var wc) ? wc : 0.6,
            CustomDataDir = Environment.GetEnvironmentVariable("PSCUE_DATA_DIR"),
        };
    }

    /// <summary>
    /// Loads all learned data from the database and wires up the prediction components.
    /// Runs on a background thread so the module returns to the prompt immediately.
    /// All PSCueModule statics are null-checked by consumers, so they gracefully return
    /// empty results until this method completes.
    /// </summary>
    private static void InitializeInBackground(InitConfiguration config, CancellationToken cancellationToken)
    {
        var debug = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PSCUE_DEBUG"));

        try
        {
            if (debug)
            {
                PSCue.Shared.Logger.Write("Background initialization starting...");
            }

            var sw = Stopwatch.StartNew();

            cancellationToken.ThrowIfCancellationRequested();

            // Initialize persistence manager and load learned data
            var dbPath = !string.IsNullOrWhiteSpace(config.CustomDataDir)
                ? Path.Combine(config.CustomDataDir, "learned-data.db")
                : null;
            var persistence = new PersistenceManager(dbPath);
            PSCueModule.Persistence = persistence;

            cancellationToken.ThrowIfCancellationRequested();

            // One connection shared across all Load operations avoids 5 redundant
            // open + PRAGMA busy_timeout cycles on the init critical path.
            using var sharedConnection = persistence.CreateSharedConnection();

            PSCueModule.KnowledgeGraph = persistence.LoadArgumentGraph(sharedConnection, config.MaxCommands, config.MaxArgs, config.DecayDays);
            PSCueModule.CommandHistory = persistence.LoadCommandHistory(sharedConnection, config.HistorySize);

            cancellationToken.ThrowIfCancellationRequested();

            // Initialize bookmarks
            var bookmarks = new BookmarkManager(persistence);
            bookmarks.Initialize(persistence.LoadBookmarks(sharedConnection));
            PSCueModule.Bookmarks = bookmarks;

            // Initialize ML sequence predictor if enabled
            if (config.MlEnabled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sequencePredictor = new SequencePredictor(config.NgramOrder, config.NgramMinFreq);
                sequencePredictor.Initialize(persistence.LoadCommandSequences(sharedConnection));
                PSCueModule.SequencePredictor = sequencePredictor;
            }

            // Initialize workflow learner if enabled
            if (config.WorkflowEnabled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workflowLearner = new WorkflowLearner(config.WorkflowMinFreq, config.WorkflowMaxTime, config.WorkflowMinConf, config.DecayDays);
                workflowLearner.Initialize(persistence.LoadWorkflowTransitions(sharedConnection));
                PSCueModule.WorkflowLearner = workflowLearner;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Initialize command parser
            var commandParser = new CommandParser();
            RegisterKnownParameters(commandParser);
            PSCueModule.CommandParser = commandParser;

            // Build and publish the generic predictor (CommandPredictor reads this dynamically)
            var contextAnalyzer = new ContextAnalyzer();
            PSCueModule.GenericPredictor = new GenericPredictor(
                PSCueModule.CommandHistory, PSCueModule.KnowledgeGraph, contextAnalyzer, PSCueModule.SequencePredictor);

            // Start auto-save timer now that all data is loaded
            var autoSaveInterval = TimeSpan.FromMinutes(5);
            _autoSaveTimer = new System.Threading.Timer(AutoSave, null, autoSaveInterval, autoSaveInterval);

            sw.Stop();

            if (debug)
            {
                PSCue.Shared.Logger.Write($"Background initialization completed in {sw.ElapsedMilliseconds}ms");
            }
        }
        catch (OperationCanceledException)
        {
            // Module was unloaded before init completed -- expected, not an error
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to initialize generic learning: {ex.Message}");

            PSCue.Shared.Logger.WriteError($"Generic learning initialization failed: {ex.GetType().Name}: {ex.Message}");
            PSCue.Shared.Logger.WriteError($"Stack trace: {ex.StackTrace}");

            var innerEx = ex.InnerException;
            var depth = 1;
            while (innerEx != null)
            {
                PSCue.Shared.Logger.WriteError($"Inner exception (level {depth}): {innerEx.GetType().Name}: {innerEx.Message}");
                PSCue.Shared.Logger.WriteError($"Inner stack trace (level {depth}): {innerEx.StackTrace}");
                innerEx = innerEx.InnerException;
                depth++;
            }
        }
    }

    private void RegisterCommandPredictor(ICommandPredictor commandPredictor)
    {
        try
        {
            SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, commandPredictor);
            _subsystems.Add((SubsystemKind.CommandPredictor, commandPredictor.Id));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("was already registered"))
        {
            // Already registered - this can happen if OnImport() is called multiple times
            // This is expected behavior due to PowerShell's module loading mechanism
            // Silently ignore duplicate registration
        }
        catch (Exception ex)
        {
            // Command predictors should work on PowerShell 7.2+, but fail gracefully if there are issues
            Console.Error.WriteLine($"Note: Command predictor not registered: {ex.Message}");
        }
    }

    private void RegisterFeedbackProvider(IFeedbackProvider feedbackProvider)
    {
        try
        {
            SubsystemManager.RegisterSubsystem(SubsystemKind.FeedbackProvider, feedbackProvider);
            _subsystems.Add((SubsystemKind.FeedbackProvider, feedbackProvider.Id));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("was already registered"))
        {
            // Already registered - this can happen if OnImport() is called multiple times
            // This is expected behavior due to PowerShell's module loading mechanism
            // Silently ignore duplicate registration
        }
        catch (Exception ex)
        {
            // Feedback providers require PowerShell 7.4+ with PSFeedbackProvider experimental feature
            // Fail gracefully on older versions or when experimental feature is not enabled
            Console.Error.WriteLine($"Note: Feedback provider not registered (requires PowerShell 7.4+): {ex.Message}");
        }
    }

    /// <summary>
    /// Auto-save callback - saves learned data periodically.
    /// </summary>
    private static void AutoSave(object? state)
    {
        try
        {
            if (PSCueModule.Persistence != null && PSCueModule.KnowledgeGraph != null && PSCueModule.CommandHistory != null)
            {
                PSCueModule.Persistence.SaveArgumentGraph(PSCueModule.KnowledgeGraph);
                PSCueModule.Persistence.SaveCommandHistory(PSCueModule.CommandHistory);

                // Prune stale directory entries after save
                PSCueModule.Persistence.PruneStaleDirectoryEntries(PSCueModule.KnowledgeGraph);

                // Save ML sequence data if enabled
                if (PSCueModule.SequencePredictor != null)
                {
                    var delta = PSCueModule.SequencePredictor.GetDelta();
                    if (delta.Count > 0)
                    {
                        PSCueModule.Persistence.SaveCommandSequences(delta);
                        PSCueModule.SequencePredictor.ClearDelta();
                    }
                }

                // Save workflow learning data if enabled
                if (PSCueModule.WorkflowLearner != null)
                {
                    var workflowDelta = PSCueModule.WorkflowLearner.GetDelta();
                    if (workflowDelta.Count > 0)
                    {
                        PSCueModule.Persistence.SaveWorkflowTransitions(workflowDelta);
                        PSCueModule.WorkflowLearner.ClearDelta();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't crash - saving is best-effort
            Console.Error.WriteLine($"Warning: Auto-save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets called when the binary module is unloaded.
    /// </summary>
    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        // Cancel background init if still running, then wait for it to finish
        // so we don't race with statics being assigned after we null them
        try
        {
            _initCts?.Cancel();
            _initTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore cancellation/timeout exceptions during shutdown
        }
        finally
        {
            _initCts?.Dispose();
            _initCts = null;
            _initTask = null;
        }

        // Save learned data before unloading
        try
        {
            if (PSCueModule.Persistence != null && PSCueModule.KnowledgeGraph != null && PSCueModule.CommandHistory != null)
            {
                PSCueModule.Persistence.SaveArgumentGraph(PSCueModule.KnowledgeGraph);
                PSCueModule.Persistence.SaveCommandHistory(PSCueModule.CommandHistory);

                // Prune stale directory entries after save
                PSCueModule.Persistence.PruneStaleDirectoryEntries(PSCueModule.KnowledgeGraph);

                // Save ML sequence data if enabled
                if (PSCueModule.SequencePredictor != null)
                {
                    var delta = PSCueModule.SequencePredictor.GetDelta();
                    if (delta.Count > 0)
                    {
                        PSCueModule.Persistence.SaveCommandSequences(delta);
                    }
                }

                // Save workflow learning data if enabled
                if (PSCueModule.WorkflowLearner != null)
                {
                    var workflowDelta = PSCueModule.WorkflowLearner.GetDelta();
                    if (workflowDelta.Count > 0)
                    {
                        PSCueModule.Persistence.SaveWorkflowTransitions(workflowDelta);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to save learned data: {ex.Message}");
        }

        // Stop auto-save timer
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;

        // Cleanup SequencePredictor
        PSCueModule.SequencePredictor?.Dispose();
        PSCueModule.SequencePredictor = null;

        // Cleanup WorkflowLearner
        PSCueModule.WorkflowLearner?.Dispose();
        PSCueModule.WorkflowLearner = null;

        // Cleanup persistence manager
        PSCueModule.Persistence?.Dispose();
        PSCueModule.Persistence = null;

        // Clear module state
        PSCueModule.GenericPredictor = null;
        PSCueModule.KnowledgeGraph = null;
        PSCueModule.CommandHistory = null;
        PSCueModule.Bookmarks = null;

        // Unregister all subsystems (predictors and feedback providers)
        foreach (var (kind, id) in _subsystems)
        {
            try
            {
                SubsystemManager.UnregisterSubsystem(kind, id);
            }
            catch
            {
                // Ignore unregistration errors
            }
        }
    }

    /// <summary>
    /// Get the generic predictor instance for testing or debugging.
    /// </summary>
    internal static GenericPredictor? GetGenericPredictor() => PSCueModule.GenericPredictor;

    /// <summary>
    /// Registers known parameters from KnownCompletions with the CommandParser.
    /// </summary>
    private static void RegisterKnownParameters(CommandParser parser)
    {
        // Register git parameters
        var git = PSCue.Shared.KnownCompletions.GitCommand.Create();
        RegisterCommandParameters(parser, "git", git);

        // Can add more commands here as needed
        // var dotnet = PSCue.Shared.KnownCompletions.DotnetCommand.Create();
        // RegisterCommandParameters(parser, "dotnet", dotnet);
    }

    /// <summary>
    /// Recursively registers parameters from a command and its subcommands.
    /// </summary>
    private static void RegisterCommandParameters(CommandParser parser, string commandName, PSCue.Shared.Completions.ICompletion completion)
    {
        if (completion is PSCue.Shared.Completions.Command command)
        {
            // Register parameters from this command
            foreach (var param in command.Parameters)
            {
                if (param.RequiresValue)
                {
                    parser.RegisterParameterRequiringValue(param.CompletionText);
                    if (!string.IsNullOrEmpty(param.Alias))
                    {
                        parser.RegisterParameterRequiringValue(param.Alias);
                    }
                }
                else
                {
                    parser.RegisterFlag(param.CompletionText);
                    if (!string.IsNullOrEmpty(param.Alias))
                    {
                        parser.RegisterFlag(param.Alias);
                    }
                }
            }

            // Recursively register subcommands
            foreach (var subcommand in command.SubCommands)
            {
                RegisterCommandParameters(parser, commandName, subcommand);
            }
        }
    }
}

/// <summary>
/// Captures all environment-variable configuration at import time
/// so it can be passed to the background initialization thread.
/// </summary>
internal sealed record InitConfiguration
{
    public int HistorySize { get; init; }
    public int MaxCommands { get; init; }
    public int MaxArgs { get; init; }
    public int DecayDays { get; init; }
    public bool MlEnabled { get; init; }
    public int NgramOrder { get; init; }
    public int NgramMinFreq { get; init; }
    public bool WorkflowEnabled { get; init; }
    public int WorkflowMinFreq { get; init; }
    public int WorkflowMaxTime { get; init; }
    public double WorkflowMinConf { get; init; }
    public string? CustomDataDir { get; init; }
}
