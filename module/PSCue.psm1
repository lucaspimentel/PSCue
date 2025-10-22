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

        # Extract the command line
        $line = $commandAst.ToString()

        # Call the pscue-completer executable with the 3 required arguments
        $completions = & $CompleterExe $wordToComplete $line $cursorPosition

        # Return completion results
        foreach ($completion in $completions) {
            # The completer returns "completionText|tooltip" format
            $parts = $completion -split '\|', 2
            $completionText = $parts[0]
            $tooltip = if ($parts.Length -gt 1) { $parts[1] } else { $completionText }

            [System.Management.Automation.CompletionResult]::new(
                $completionText,
                $completionText,
                [System.Management.Automation.CompletionResultType]::ParameterValue,
                $tooltip
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

# CommandPredictor is loaded as a nested module from the PSD1
# It auto-registers via IModuleAssemblyInitializer.OnImport()

# Suggest enabling predictions if not already enabled
$predictionSource = (Get-PSReadLineOption).PredictionSource
if ($predictionSource -eq 'None' -or $predictionSource -eq 'History') {
    Write-Host "PSCue: To enable inline predictions, run:" -ForegroundColor Cyan
    Write-Host "  Set-PSReadLineOption -PredictionSource HistoryAndPlugin" -ForegroundColor Yellow
}

# Export nothing (we only register completers and load the predictor)
Export-ModuleMember -Function @()
