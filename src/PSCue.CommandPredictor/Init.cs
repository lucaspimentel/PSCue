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

    /// <summary>
    /// Gets called when assembly is loaded.
    /// </summary>
    public void OnImport()
    {
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

        // Cleanup AI resources
        // AiPredictor.Cleanup();
    }
}
