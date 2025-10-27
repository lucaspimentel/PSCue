$logPath = "$env:LOCALAPPDATA\PSCue\log.txt"
if (Test-Path $logPath) {
    Write-Host "Log file contents:" -ForegroundColor Cyan
    Get-Content $logPath -Tail 100
} else {
    Write-Host "No log file found at: $logPath" -ForegroundColor Yellow
}
