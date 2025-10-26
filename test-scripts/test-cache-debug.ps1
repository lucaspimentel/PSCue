# Test script with debug logging to diagnose cache issue
# This script enables debug logging to see what's happening

# 1. Load PSCue module
Write-Host "1. Loading PSCue module..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force

# 2. Set environment variables for debugging
Write-Host "2. Setting environment variables..." -ForegroundColor Cyan
$env:PSCUE_PID = $PID
$env:PSCUE_DEBUG = "1"  # Enable debug logging
Write-Host "   PSCUE_PID = $env:PSCUE_PID" -ForegroundColor Gray
Write-Host "   PSCUE_DEBUG = $env:PSCUE_DEBUG" -ForegroundColor Gray

# 3. Test IPC connectivity
Write-Host ""
Write-Host "3. Testing IPC connectivity..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- ping

# 4. Check initial cache
Write-Host ""
Write-Host "4. Initial cache state..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- stats

# 5. Trigger completion via TabExpansion2
Write-Host ""
Write-Host "5. Triggering completion via TabExpansion2..." -ForegroundColor Cyan
Write-Host "   Command: 'git status'" -ForegroundColor Gray
$result = TabExpansion2 'git st' 6
Write-Host "   Found $($result.CompletionMatches.Count) completions" -ForegroundColor Gray
if ($result.CompletionMatches.Count -gt 0) {
    Write-Host "   First completion: $($result.CompletionMatches[0].CompletionText)" -ForegroundColor Gray
}

# 6. Check cache after TabExpansion2
Write-Host ""
Write-Host "6. Cache state after TabExpansion2..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- cache

# 7. Test query-ipc directly
Write-Host ""
Write-Host "7. Testing query-ipc directly..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- query-ipc "git st"

# 8. Check cache after query-ipc
Write-Host ""
Write-Host "8. Cache state after query-ipc..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- cache

# 9. Check debug log if it exists
Write-Host ""
Write-Host "9. Checking for debug logs..." -ForegroundColor Cyan
$debugLogPath = [System.IO.Path]::Combine($env:TEMP, "pscue-completer-debug.log")
if (Test-Path $debugLogPath) {
    Write-Host "   Debug log found at: $debugLogPath" -ForegroundColor Gray
    Write-Host "   Last 10 lines:" -ForegroundColor Gray
    Get-Content $debugLogPath -Tail 10 | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkGray }
} else {
    Write-Host "   No debug log found at: $debugLogPath" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Debug test complete!" -ForegroundColor Green
