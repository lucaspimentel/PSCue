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
    private static IpcServer? _ipcServer;
    private static ContextAnalyzer? _contextAnalyzer;
    private static GenericPredictor? _genericPredictor;
    private static System.Threading.Timer? _autoSaveTimer;

    /// <summary>
    /// Gets called when assembly is loaded.
    /// </summary>
    public void OnImport()
    {
        // Phase 16: Initialize completion cache (used by both IPC and PowerShell functions)
        PSCueModule.Cache = new CompletionCache();

        // Phase 11: Initialize generic learning system
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

                // Phase 12: Initialize persistence manager and load learned data
                PSCueModule.Persistence = new PersistenceManager();
                PSCueModule.KnowledgeGraph = PSCueModule.Persistence.LoadArgumentGraph(maxCommands, maxArgs, decayDays);
                PSCueModule.CommandHistory = PSCueModule.Persistence.LoadCommandHistory(historySize);

                _contextAnalyzer = new ContextAnalyzer();
                _genericPredictor = new GenericPredictor(PSCueModule.CommandHistory, PSCueModule.KnowledgeGraph, _contextAnalyzer);

                // Set up auto-save timer (every 5 minutes)
                var autoSaveInterval = TimeSpan.FromMinutes(5);
                _autoSaveTimer = new System.Threading.Timer(AutoSave, null, autoSaveInterval, autoSaveInterval);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to initialize generic learning: {ex.Message}");
                enableGenericLearning = false;
            }
        }

        // Start IPC server for ArgumentCompleter communication
        try
        {
            _ipcServer = new IpcServer();
        }
        catch (Exception ex)
        {
            // Log but don't fail module loading if IPC server fails to start
            Console.Error.WriteLine($"Failed to start IPC server: {ex.Message}");
        }

        // Register command predictor with generic learning support
        RegisterCommandPredictor(new CommandPredictor(_genericPredictor, enableGenericLearning));
        //RegisterCommandPredictor(new SamplePredictor());

        // Register feedback provider (requires PowerShell 7.4+ with PSFeedbackProvider experimental feature)
        // This will fail gracefully on older PowerShell versions
        RegisterFeedbackProvider(new FeedbackProvider(_ipcServer, PSCueModule.CommandHistory, PSCueModule.KnowledgeGraph));
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
        // Phase 12: Save learned data before unloading
        try
        {
            if (PSCueModule.Persistence != null && PSCueModule.KnowledgeGraph != null && PSCueModule.CommandHistory != null)
            {
                PSCueModule.Persistence.SaveArgumentGraph(PSCueModule.KnowledgeGraph);
                PSCueModule.Persistence.SaveCommandHistory(PSCueModule.CommandHistory);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to save learned data: {ex.Message}");
        }

        // Stop auto-save timer
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;

        // Cleanup persistence manager
        PSCueModule.Persistence?.Dispose();
        PSCueModule.Persistence = null;

        // Clear module state
        PSCueModule.Cache = null;
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

        // Cleanup IPC server
        _ipcServer?.Dispose();
        _ipcServer = null;

        // Cleanup AI resources
        // AiPredictor.Cleanup();
    }

    /// <summary>
    /// Get the IPC server instance for testing or feedback provider access.
    /// </summary>
    internal static IpcServer? GetIpcServer() => _ipcServer;

    /// <summary>
    /// Get the generic predictor instance for testing or debugging.
    /// </summary>
    internal static GenericPredictor? GetGenericPredictor() => _genericPredictor;
}
