$env:PSCUE_DEBUG = "1"

# Clear the log first
$logPath = "$env:LOCALAPPDATA\PSCue\log.txt"
if (Test-Path $logPath) {
    Clear-Content $logPath
}

Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1

# Set environment variable so pscue-debug can find this session
$env:PSCUE_PID = $PID

# Wait for IPC server to start
Start-Sleep -Seconds 1

# Test scoop update completion
Write-Host "Testing: scoop update <tab>" -ForegroundColor Cyan
$result = TabExpansion2 'scoop update ' 13
if ($result.CompletionMatches) {
    Write-Host "Found $($result.CompletionMatches.Count) completions:" -ForegroundColor Green
    $result.CompletionMatches | Select-Object -First 10 CompletionText, ToolTip | Format-Table -AutoSize
} else {
    Write-Host "No completions found!" -ForegroundColor Red
}

# Show the log
Write-Host "`n`nLog output:" -ForegroundColor Cyan
if (Test-Path $logPath) {
    Get-Content $logPath | Select-Object -Last 50
} else {
    Write-Host "No log file found!" -ForegroundColor Yellow
}
