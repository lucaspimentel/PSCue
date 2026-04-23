# PSCue Module - PowerShell Completion and Prediction
# Author: Lucas Pimentel
# Repository: https://github.com/lucaspimentel/PSCue

#Requires -Version 7.2

$psm1Start = [DateTime]::UtcNow
$debug = $env:PSCUE_DEBUG -eq '1'

if ($debug) {
    $gap = ($psm1Start - [PSCue.Module.ModuleInitializer]::AssemblyLoadedUtc).TotalMilliseconds
    [PSCue.Shared.Logger]::Write("IMPORT [gap] assembly_ctor_to_psm1_start=$([int]$gap)ms")
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()

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

if ($debug) { [PSCue.Shared.Logger]::Write("IMPORT [phase] CompleterExeResolution=$($sw.ElapsedMilliseconds)ms") }

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
    'git-wt'
    'gh'
    'gt'

    # Azure
    'az'
    'azd'
    'func'

    # Package Managers
    'scoop'
    'winget'

    # Tools
    'code'
    'claude'
    'chezmoi'
    'tre'
    'lsd'
    'dust'
    'chafa'
    'rg'
    'fd'
    'wt'
)

$sw.Restart()
foreach ($command in $SupportedCommands) {
    Register-ArgumentCompleter -Native -CommandName $command -ScriptBlock $CompleterScriptBlock
}
if ($debug) { [PSCue.Shared.Logger]::Write("IMPORT [phase] Register-ArgumentCompleter-loop=$($sw.ElapsedMilliseconds)ms ($($SupportedCommands.Count) commands)") }

# CommandPredictor is loaded as a nested module from the PSD1
# It auto-registers via IModuleAssemblyInitializer.OnImport()

# Load PowerShell functions for learning, database, workflow, navigation, and debugging
$sw.Restart(); . $PSScriptRoot/Functions.ps1
if ($debug) { [PSCue.Shared.Logger]::Write("IMPORT [phase] Dotsource-Functions=$($sw.ElapsedMilliseconds)ms") }

$sw.Restart()
$dataDir = if ($env:PSCUE_DATA_DIR) {
    $env:PSCUE_DATA_DIR
} elseif ($IsWindows) {
    Join-Path $env:LOCALAPPDATA 'PSCue'
} else {
    Join-Path $HOME '.local/share/PSCue'
}
$marker = Join-Path $dataDir 'prediction-source-ok'
$checkDays = if ($env:PSCUE_PREDICTION_SOURCE_CHECK_DAYS) {
    [int]$env:PSCUE_PREDICTION_SOURCE_CHECK_DAYS
} else { 7 }
$markerInfo = [System.IO.FileInfo]::new($marker)
$markerFresh = $markerInfo.Exists -and
    $markerInfo.LastWriteTimeUtc -gt [DateTime]::UtcNow.AddDays(-$checkDays)

if (-not $markerFresh) {
    $predictionSource = (Get-PSReadLineOption).PredictionSource
    if ($predictionSource -eq 'None' -or $predictionSource -eq 'History') {
        Write-Host "PSCue: To enable inline predictions, run:" -ForegroundColor Cyan
        Write-Host "  Set-PSReadLineOption -PredictionSource HistoryAndPlugin" -ForegroundColor Yellow
    } else {
        try {
            if (-not (Test-Path -LiteralPath $dataDir)) {
                $null = New-Item -ItemType Directory -Path $dataDir -Force
            }
            [System.IO.File]::WriteAllText($marker, '')
        } catch {
            # best-effort: if we can't write the marker we just re-check next session
        }
    }
}
if ($debug) { [PSCue.Shared.Logger]::Write("IMPORT [phase] PSReadLine-source-check=$($sw.ElapsedMilliseconds)ms (marker_fresh=$markerFresh)") }

if ($debug) {
    $psm1Total = ([DateTime]::UtcNow - $psm1Start).TotalMilliseconds
    [PSCue.Shared.Logger]::Write("IMPORT [total-sync] psm1=$([int]$psm1Total)ms")
}
