using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;
using System.Management.Automation.Subsystem.Feedback;

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
    private readonly List<(SubsystemKind Kind, Guid Id)> _subsystems = [];
    private static ContextAnalyzer? _contextAnalyzer;
    private static GenericPredictor? _genericPredictor;
    private static System.Threading.Timer? _autoSaveTimer;

    /// <summary>
    /// Gets called when assembly is loaded.
    /// </summary>
    public void OnImport()
    {
        // Initialize generic learning system
        // Check if generic learning is enabled (default: true, can be disabled via env var)
        var enableGenericLearning = Environment.GetEnvironmentVariable("PSCUE_DISABLE_LEARNING")?.Equals("true", StringComparison.OrdinalIgnoreCase) != true;

        if (enableGenericLearning)
        {
            try
            {
                // Read configuration from environment variables (with defaults)
                var historySize = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_HISTORY_SIZE"), out var hs) ? hs : 100;
                var maxCommands = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_MAX_COMMANDS"), out var mc) ? mc : 500;
                var maxArgs = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_MAX_ARGS_PER_CMD"), out var ma) ? ma : 100;
                var decayDays = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_DECAY_DAYS"), out var dd) ? dd : 30;

                // ML configuration (Phase 17.1: N-gram predictor)
                var mlEnabled = Environment.GetEnvironmentVariable("PSCUE_ML_ENABLED")?.Equals("false", StringComparison.OrdinalIgnoreCase) != true; // Default: true
                var ngramOrder = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_ML_NGRAM_ORDER"), out var no) ? no : 2; // Default: bigrams
                var ngramMinFreq = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_ML_NGRAM_MIN_FREQ"), out var mf) ? mf : 3; // Default: 3 occurrences

                // Workflow learning configuration (Phase 18.1: Dynamic workflow learning)
                var workflowEnabled = Environment.GetEnvironmentVariable("PSCUE_WORKFLOW_LEARNING")?.Equals("false", StringComparison.OrdinalIgnoreCase) != true; // Default: true
                var workflowMinFreq = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_WORKFLOW_MIN_FREQUENCY"), out var wf) ? wf : 5; // Default: 5 occurrences
                var workflowMaxTime = int.TryParse(Environment.GetEnvironmentVariable("PSCUE_WORKFLOW_MAX_TIME_DELTA"), out var wt) ? wt : 15; // Default: 15 minutes
                var workflowMinConf = double.TryParse(Environment.GetEnvironmentVariable("PSCUE_WORKFLOW_MIN_CONFIDENCE"), out var wc) ? wc : 0.6; // Default: 0.6

                // Initialize persistence manager and load learned data
                PSCueModule.Persistence = new PersistenceManager();
                PSCueModule.KnowledgeGraph = PSCueModule.Persistence.LoadArgumentGraph(maxCommands, maxArgs, decayDays);
                PSCueModule.CommandHistory = PSCueModule.Persistence.LoadCommandHistory(historySize);

                // Initialize ML sequence predictor if enabled
                if (mlEnabled)
                {
                    PSCueModule.SequencePredictor = new SequencePredictor(ngramOrder, ngramMinFreq);
                    var sequences = PSCueModule.Persistence.LoadCommandSequences();
                    PSCueModule.SequencePredictor.Initialize(sequences);
                }

                // Initialize workflow learner if enabled
                if (workflowEnabled)
                {
                    PSCueModule.WorkflowLearner = new WorkflowLearner(workflowMinFreq, workflowMaxTime, workflowMinConf, decayDays);
                    var workflows = PSCueModule.Persistence.LoadWorkflowTransitions();
                    PSCueModule.WorkflowLearner.Initialize(workflows);
                }

                // Initialize command parser
                PSCueModule.CommandParser = new CommandParser();

                // Register known parameters from KnownCompletions
                RegisterKnownParameters(PSCueModule.CommandParser);

                _contextAnalyzer = new ContextAnalyzer();
                _genericPredictor = new GenericPredictor(PSCueModule.CommandHistory, PSCueModule.KnowledgeGraph, _contextAnalyzer, PSCueModule.SequencePredictor);

                // Set up auto-save timer (every 5 minutes)
                var autoSaveInterval = TimeSpan.FromMinutes(5);
                _autoSaveTimer = new System.Threading.Timer(AutoSave, null, autoSaveInterval, autoSaveInterval);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Warning: Failed to initialize generic learning: {ex.Message}";
                Console.Error.WriteLine(errorMessage);

                // Log detailed exception information (always logged, regardless of PSCUE_DEBUG)
                PSCue.Shared.Logger.WriteError($"Generic learning initialization failed: {ex.GetType().Name}: {ex.Message}");
                PSCue.Shared.Logger.WriteError($"Stack trace: {ex.StackTrace}");

                // Log all inner exceptions recursively
                var innerEx = ex.InnerException;
                var depth = 1;
                while (innerEx != null)
                {
                    PSCue.Shared.Logger.WriteError($"Inner exception (level {depth}): {innerEx.GetType().Name}: {innerEx.Message}");
                    PSCue.Shared.Logger.WriteError($"Inner stack trace (level {depth}): {innerEx.StackTrace}");
                    innerEx = innerEx.InnerException;
                    depth++;
                }

                enableGenericLearning = false;
            }
        }

        // Register command predictor with generic learning support
        RegisterCommandPredictor(new CommandPredictor(_genericPredictor, enableGenericLearning));
        //RegisterCommandPredictor(new SamplePredictor());

        // Register feedback provider (requires PowerShell 7.4+ with PSFeedbackProvider experimental feature)
        // This will fail gracefully on older PowerShell versions
        // Note: FeedbackProvider gets instances dynamically from PSCueModule to handle module reloads
        RegisterFeedbackProvider(new FeedbackProvider());
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
        // Save learned data before unloading
        try
        {
            if (PSCueModule.Persistence != null && PSCueModule.KnowledgeGraph != null && PSCueModule.CommandHistory != null)
            {
                PSCueModule.Persistence.SaveArgumentGraph(PSCueModule.KnowledgeGraph);
                PSCueModule.Persistence.SaveCommandHistory(PSCueModule.CommandHistory);

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
        PSCueModule.KnowledgeGraph = null;
        PSCueModule.CommandHistory = null;

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

        // Cleanup AI resources
        // AiPredictor.Cleanup();
    }

    /// <summary>
    /// Get the generic predictor instance for testing or debugging.
    /// </summary>
    internal static GenericPredictor? GetGenericPredictor() => _genericPredictor;

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
