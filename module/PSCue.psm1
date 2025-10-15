# PSCue Module - PowerShell Completion and Prediction
# Author: Lucas Pimentel
# Repository: https://github.com/lucaspimentel/PSCue

#Requires -Version 7.2

# Get the module directory
$ModuleRoot = $PSScriptRoot

# Find the pscue-completer executable
$CompleterExe = if ($IsWindows) {
    Join-Path $ModuleRoot "pscue-completer.exe"
} else {
    Join-Path $ModuleRoot "pscue-completer"
}

# Verify the completer executable exists
if (-not (Test-Path $CompleterExe)) {
    Write-Warning "PSCue: pscue-completer executable not found at: $CompleterExe"
    Write-Warning "PSCue: Tab completion will not be available. Please ensure the module is properly installed."
    $CompleterExe = $null
}

# Define the script block for argument completion
$CompleterScriptBlock = if ($CompleterExe) {
    {
        param($wordToComplete, $commandAst, $cursorPosition)

        # Extract the command line up to the cursor position
        $line = $commandAst.ToString()

        # Call the pscue-completer executable
        $completions = & $CompleterExe $line

        # Return completion results
        foreach ($completion in $completions) {
            # The completer returns JSON, parse it
            $completionObj = $completion | ConvertFrom-Json
            [System.Management.Automation.CompletionResult]::new(
                $completionObj.CompletionText,
                $completionObj.ListItemText,
                $completionObj.ResultType,
                $completionObj.ToolTip
            )
        }
    }
} else {
    # Fallback: no completions if executable not found
    { param($wordToComplete, $commandAst, $cursorPosition) }
}

# Register argument completers for supported commands
$SupportedCommands = @(
    # Version Control
    'git'
    'gh'

    # Azure
    'az'
    'azd'
    'func'

    # Package Managers
    'scoop'
    'winget'

    # Tools
    'code'
    'chezmoi'
    'tre'
    'lsd'
    'dust'
)

foreach ($command in $SupportedCommands) {
    Register-ArgumentCompleter -CommandName $command -ScriptBlock $CompleterScriptBlock
}

# Load the CommandPredictor DLL (ICommandPredictor auto-registers via IModuleAssemblyInitializer)
$PredictorDll = Join-Path $ModuleRoot "PSCue.CommandPredictor.dll"

if (Test-Path $PredictorDll) {
    try {
        # Import the predictor assembly
        Import-Module $PredictorDll -ErrorAction Stop

        Write-Verbose "PSCue: Loaded CommandPredictor from: $PredictorDll"

        # Suggest enabling predictions if not already enabled
        $predictionSource = (Get-PSReadLineOption).PredictionSource
        if ($predictionSource -eq 'None' -or $predictionSource -eq 'History') {
            Write-Host "PSCue: To enable inline predictions, run:" -ForegroundColor Cyan
            Write-Host "  Set-PSReadLineOption -PredictionSource HistoryAndPlugin" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Warning "PSCue: Failed to load CommandPredictor: $_"
    }
} else {
    Write-Warning "PSCue: CommandPredictor DLL not found at: $PredictorDll"
    Write-Warning "PSCue: Inline predictions will not be available."
}

# Export nothing (we only register completers and load the predictor)
Export-ModuleMember -Function @()
