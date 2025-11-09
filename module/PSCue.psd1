@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'PSCue.psm1'

    # Version number of this module.
    ModuleVersion = '0.3.0'

    # Supported PSEditions
    CompatiblePSEditions = @('Core')

    # ID used to uniquely identify this module
    GUID = '8a3c7f2e-4b1d-4c9a-8f5e-1a2b3c4d5e6f'

    # Author of this module
    Author = 'Lucas Pimentel'

    # Company or vendor of this module
    CompanyName = 'lucaspimentel'

    # Copyright statement for this module
    Copyright = '(c) 2024 Lucas Pimentel. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'PSCue - PowerShell Completion and Prediction Module. Provides intelligent command-line completion via Register-ArgumentCompleter and inline predictions via ICommandPredictor.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '7.2'

    # Name of the PowerShell host required by this module
    # PowerShellHostName = ''

    # Minimum version of the PowerShell host required by this module
    # PowerShellHostVersion = ''

    # Minimum version of Microsoft .NET Framework required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
    # DotNetFrameworkVersion = ''

    # Minimum version of the common language runtime (CLR) required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
    # ClrVersion = ''

    # Processor architecture (None, X86, Amd64) required by this module
    # ProcessorArchitecture = ''

    # Modules that must be imported into the global environment prior to importing this module
    # RequiredModules = @()

    # Assemblies that must be loaded prior to importing this module
    # RequiredAssemblies = @()

    # Script files (.ps1) that are run in the caller's environment prior to importing this module.
    # ScriptsToProcess = @()

    # Type files (.ps1xml) to be loaded when importing this module
    # TypesToProcess = @()

    # Format files (.ps1xml) to be loaded when importing this module
    # FormatsToProcess = @()

    # Modules to import as nested modules of the module specified in RootModule/ModuleToProcess
    NestedModules = @('PSCue.Module.dll')

    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport = @(
        # Cache Management
        'Get-PSCueCache',
        'Clear-PSCueCache',
        'Get-PSCueCacheStats',
        # Learning Management
        'Get-PSCueLearning',
        'Clear-PSCueLearning',
        'Export-PSCueLearning',
        'Import-PSCueLearning',
        'Save-PSCueLearning',
        # Database Management
        'Get-PSCueDatabaseStats',
        'Get-PSCueDatabaseHistory',
        # Workflow Management
        'Get-PSCueWorkflows',
        'Get-PSCueWorkflowStats',
        'Clear-PSCueWorkflows',
        'Export-PSCueWorkflows',
        'Import-PSCueWorkflows',
        # Navigation
        'Invoke-PCD',
        # Debugging
        'Test-PSCueCompletion',
        'Get-PSCueModuleInfo'
    )

    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport = @()

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
    AliasesToExport = @('pcd')

    # DSC resources to export from this module
    # DscResourcesToExport = @()

    # List of all modules packaged with this module
    # ModuleList = @()

    # List of all files packaged with this module
    # FileList = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData = @{
        PSData = @{
            # Tags applied to this module. These help with module discovery in online galleries.
            Tags = @('PowerShell', 'Completion', 'Prediction', 'ArgumentCompleter', 'CommandPredictor', 'Tab', 'PSReadLine')

            # A URL to the license for this module.
            LicenseUri = 'https://github.com/lucaspimentel/PSCue/blob/main/LICENSE'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/lucaspimentel/PSCue'

            # A URL to an icon representing this module.
            # IconUri = ''

            # ReleaseNotes of this module
            ReleaseNotes = @'
v0.3.0 - Major Feature Release

New Features:
- Multi-word prediction suggestions (Phase 17.4): Shows common argument combinations like "git checkout master"
- Dynamic workflow learning (Phase 18.1): Learns command sequences and predicts next command based on usage patterns
- Smart directory navigation with pcd command (Phases 17.5-17.7):
  * Fuzzy matching with Levenshtein distance
  * Frecency scoring (frequency + recency + distance)
  * Inline predictions with relative path display
  * Best-match navigation without tab completion
- PowerShell module functions for workflow management: Get/Clear/Export/Import-PSCueWorkflows

Improvements:
- Enhanced path handling with cross-drive validation
- Better directory existence filtering
- Improved path normalization to prevent duplicates
- Performance optimizations for directory navigation (<10ms)

Database:
- Extended SQLite schema to 8 tables (added workflow_transitions, argument_sequences)
- Cross-session persistence for all learned data
'@

            # Prerelease string of this module
            # Prerelease = ''

            # Flag to indicate whether the module requires explicit user acceptance for install/update/save
            # RequireLicenseAcceptance = $false

            # External dependent modules of this module
            # ExternalModuleDependencies = @()
        }
    }

    # HelpInfo URI of this module
    # HelpInfoURI = ''

    # Default prefix for commands exported from this module. Override the default prefix using Import-Module -Prefix.
    # DefaultCommandPrefix = ''
}
