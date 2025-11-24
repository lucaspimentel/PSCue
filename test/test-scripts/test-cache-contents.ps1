#!/usr/bin/env pwsh
# Test script to inspect cache contents after scoop h completion

Write-Host "Setting up test environment..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID
Start-Sleep -Milliseconds 500

Write-Host ""
Write-Host "=== Triggering 'scoop h' completion ===" -ForegroundColor Magenta
$result1 = TabExpansion2 'scoop h' 7
Write-Host "Got $($result1.CompletionMatches.Count) completions" -ForegroundColor Yellow

Write-Host ""
Write-Host "=== Inspecting cache contents ===" -ForegroundColor Magenta
& "D:\source\lucaspimentel\PSCue\src\PSCue.Debug\bin\Release\net9.0\pscue-debug.exe" cache --filter scoop

Write-Host ""
Write-Host "Note: If the cache entry shows only 3 completions (help, hold, home), the bug is still present." -ForegroundColor Yellow
Write-Host "If it shows ~28 completions, the fix is working." -ForegroundColor Cyan
