# Test script to demonstrate cache learning
# This script shows how the cache populates and learns from usage

# 1. Load PSCue module
Write-Host "1. Loading PSCue module..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force

# 2. Set environment variable so pscue-debug can find this session
Write-Host "2. Setting PSCUE_PID environment variable..." -ForegroundColor Cyan
$env:PSCUE_PID = $PID
Write-Host "   PSCUE_PID = $env:PSCUE_PID" -ForegroundColor Gray

# 3. Check initial cache state
Write-Host ""
Write-Host "3. Checking initial cache state..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- cache

# 4. Trigger some Tab completions to populate the cache
Write-Host ""
Write-Host "4. Triggering completions to populate cache..." -ForegroundColor Cyan
Write-Host "   (Simulating Tab completion for 'git checkout ma')" -ForegroundColor Gray

# Use TabExpansion2 to trigger completion
$result = TabExpansion2 'git checkout ma' 15
Write-Host "   Found $($result.CompletionMatches.Count) completions" -ForegroundColor Gray

# 5. Check cache after completions
Write-Host ""
Write-Host "5. Checking cache after completions..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- cache --filter git

# 6. Execute a command to trigger learning
Write-Host ""
Write-Host "6. Executing 'git status' to trigger learning..." -ForegroundColor Cyan
git status | Out-Null

# 7. Check cache after execution
Write-Host ""
Write-Host "7. Checking cache after command execution..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- cache --filter git

# 8. Show cache statistics
Write-Host ""
Write-Host "8. Cache statistics:" -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- stats

Write-Host ""
Write-Host "Done! The cache now contains learned data." -ForegroundColor Green
Write-Host "Try executing more git commands to see scores increase." -ForegroundColor Gray
