using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;

namespace PSCue.CommandPredictor;

// https://adamtheautomator.com/psreadline/
// https://learn.microsoft.com/en-us/powershell/scripting/learn/shell/using-predictors
// https://learn.microsoft.com/en-us/powershell/scripting/dev-cross-plat/create-cmdline-predictor

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

        RegisterSubsystem(new CommandCompleterPredictor());
        // RegisterSubsystem(new KnownCommandsPredictor());
        // RegisterSubsystem(new SamplePredictor());
        // RegisterSubsystem(new AiPredictor());
    }

    private void RegisterSubsystem(ICommandPredictor commandPredictor)
    {
        _identifiers.Add(commandPredictor.Id);
        SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, commandPredictor);
    }

    /// <summary>
    /// Gets called when the binary module is unloaded.
    /// </summary>
    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        foreach (var id in _identifiers)
        {
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, id);
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
