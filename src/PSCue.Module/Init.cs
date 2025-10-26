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
/// Register the predictor on module loading and unregister it on module un-loading.
/// </summary>
public class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    private readonly List<Guid> _identifiers = [];
    private static IpcServer? _ipcServer;

    /// <summary>
    /// Gets called when assembly is loaded.
    /// </summary>
    public void OnImport()
    {
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

        RegisterCommandPredictor(new CommandCompleterPredictor());
        //RegisterCommandPredictor(new SamplePredictor());

        // Register feedback provider (requires PowerShell 7.4+ with PSFeedbackProvider experimental feature)
        // This will fail gracefully on older PowerShell versions
        RegisterFeedbackProvider(new CommandCompleterFeedbackProvider(_ipcServer));
    }

    private void RegisterCommandPredictor(ICommandPredictor commandPredictor)
    {
        _identifiers.Add(commandPredictor.Id);
        SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, commandPredictor);
    }

    private void RegisterFeedbackProvider(IFeedbackProvider feedbackProvider)
    {
        try
        {
            _identifiers.Add(feedbackProvider.Id);
            SubsystemManager.RegisterSubsystem(SubsystemKind.FeedbackProvider, feedbackProvider);
        }
        catch (Exception ex)
        {
            // Feedback providers require PowerShell 7.4+ with PSFeedbackProvider experimental feature
            // Fail gracefully on older versions or when experimental feature is not enabled
            Console.Error.WriteLine($"Note: Feedback provider not registered (requires PowerShell 7.4+): {ex.Message}");
        }
    }

    /// <summary>
    /// Gets called when the binary module is unloaded.
    /// </summary>
    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        // Unregister all subsystems (predictors and feedback providers)
        foreach (var id in _identifiers)
        {
            try
            {
                // Try CommandPredictor first
                SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, id);
            }
            catch
            {
                try
                {
                    // Try FeedbackProvider if CommandPredictor fails
                    SubsystemManager.UnregisterSubsystem(SubsystemKind.FeedbackProvider, id);
                }
                catch
                {
                    // Ignore unregistration errors
                }
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
    public static IpcServer? GetIpcServer() => _ipcServer;
}
