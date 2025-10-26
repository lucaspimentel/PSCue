# Test with PSCUE_DEBUG=1 to see IPC path
$env:PSCUE_DEBUG = "1"  # Enable completer debug logging

# Clear old log
$logPath = Join-Path $env:LOCALAPPDATA 'pwsh-argument-completer/log.txt'
if (Test-Path $logPath) {
    Clear-Content $logPath
    Write-Host "Cleared old log" -ForegroundColor Gray
}

# Load module
Write-Host "Loading PSCue..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID

# Test IPC
Write-Host "Testing IPC connectivity..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- ping

# Trigger completion
Write-Host ""
Write-Host "Triggering TabExpansion2 for 'git st'..." -ForegroundColor Cyan
$result = TabExpansion2 'git st' 6
Write-Host "Found $($result.CompletionMatches.Count) completions" -ForegroundColor Gray

# Check log
Write-Host ""
Write-Host "Completer log output:" -ForegroundColor Cyan
if (Test-Path $logPath) {
    Get-Content $logPath | ForEach-Object {
        if ($_ -match "IPC") {
            Write-Host "  $_" -ForegroundColor Yellow
        } elseif ($_ -match "local") {
            Write-Host "  $_" -ForegroundColor Red
        } else {
            Write-Host "  $_" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "  No log entries" -ForegroundColor Red
}

# Check cache
Write-Host ""
Write-Host "Cache state:" -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- cache
