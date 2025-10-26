# Check the completer log file
$logPath = Join-Path $env:LOCALAPPDATA 'pwsh-argument-completer/log.txt'

Write-Host "Completer log location: $logPath" -ForegroundColor Cyan

if (Test-Path $logPath) {
    Write-Host "✓ Log file exists" -ForegroundColor Green
    Write-Host ""
    Write-Host "Last 30 lines:" -ForegroundColor Cyan
    Get-Content $logPath -Tail 30 | ForEach-Object {
        if ($_ -match "IPC") {
            Write-Host $_ -ForegroundColor Yellow
        } elseif ($_ -match "local") {
            Write-Host $_ -ForegroundColor Red
        } else {
            Write-Host $_ -ForegroundColor Gray
        }
    }
} else {
    Write-Host "✗ Log file not found" -ForegroundColor Red
    Write-Host "  Debug mode might not be enabled" -ForegroundColor Yellow
}
