#!/usr/bin/env pwsh
# Test with cache clearing between requests

Write-Host "Setting up test environment..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID
Start-Sleep -Milliseconds 500

Write-Host ""
Write-Host "Step 1: Clear cache" -ForegroundColor Magenta
& "D:\source\lucaspimentel\PSCue\src\PSCue.Debug\bin\Release\net9.0\pscue-debug.exe" clear

Write-Host ""
Write-Host "Step 2: Test 'scoop h<tab>'" -ForegroundColor Magenta
$result1 = TabExpansion2 'scoop h' 7
Write-Host "Got $($result1.CompletionMatches.Count) completions" -ForegroundColor Yellow

Write-Host ""
Write-Host "Step 3: Check cache contents" -ForegroundColor Magenta
& "D:\source\lucaspimentel\PSCue\src\PSCue.Debug\bin\Release\net9.0\pscue-debug.exe" cache --filter scoop

Write-Host ""
Write-Host "Expected: Cache should have ~28 completions, not just 3" -ForegroundColor Cyan
