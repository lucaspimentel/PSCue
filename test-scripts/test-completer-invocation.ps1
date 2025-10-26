# Test if our completer scriptblock is actually being invoked
# This adds debug output to the scriptblock to see if it's called

Write-Host "Testing if PSCue ArgumentCompleter is invoked" -ForegroundColor Cyan
Write-Host ""

# Load module
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID

# Create a test log file to track invocations
$testLog = Join-Path $env:TEMP "pscue-test-invocation.log"
if (Test-Path $testLog) { Remove-Item $testLog }

Write-Host "1. Manually registering a completer with logging..." -ForegroundColor Cyan

# Manually register with debug logging
$completerExe = Join-Path ~/.local/pwsh-modules/PSCue "pscue-completer.exe"
$debugScriptBlock = {
    param($wordToComplete, $commandAst, $cursorPosition)

    # Log that we were invoked
    $logFile = Join-Path $env:TEMP "pscue-test-invocation.log"
    "INVOKED at $(Get-Date -Format 'HH:mm:ss.fff'): wordToComplete='$wordToComplete', commandAst='$commandAst', cursorPosition=$cursorPosition" |
        Add-Content -Path $logFile

    # Extract the command line
    $line = $commandAst.ToString()

    # Call the pscue-completer executable
    $completerExe = Join-Path ~/.local/pwsh-modules/PSCue "pscue-completer.exe"
    $completions = & $completerExe $wordToComplete $line $cursorPosition

    # Log results
    "  -> Returned $($completions.Count) completions" | Add-Content -Path $logFile

    # Return completion results
    foreach ($completion in $completions) {
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
}.GetNewClosure()

# Register for git with -Native flag
Register-ArgumentCompleter -Native -CommandName 'git' -ScriptBlock $debugScriptBlock

Write-Host "2. Testing TabExpansion2..." -ForegroundColor Cyan
$result = TabExpansion2 'git st' 6
Write-Host "   Found $($result.CompletionMatches.Count) completions" -ForegroundColor Gray

# Check the log
Write-Host ""
Write-Host "3. Checking invocation log..." -ForegroundColor Cyan
if (Test-Path $testLog) {
    $logContents = Get-Content $testLog
    if ($logContents) {
        Write-Host "   ✓ Completer WAS invoked!" -ForegroundColor Green
        foreach ($line in $logContents) {
            Write-Host "   $line" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ✗ Log file exists but is empty" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Completer was NOT invoked (log file not created)" -ForegroundColor Red
    Write-Host "   This means PowerShell is using built-in completions" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Green
