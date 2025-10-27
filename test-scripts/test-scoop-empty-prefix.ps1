#!/usr/bin/env pwsh
# Test script to check "scoop <tab>" completions (empty prefix)

Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID
Start-Sleep -Milliseconds 500

Write-Host "Testing 'scoop <tab>' completions (empty prefix)..." -ForegroundColor Cyan
$result = TabExpansion2 'scoop ' 6
$completions = $result.CompletionMatches | Select-Object -ExpandProperty CompletionText | Sort-Object

Write-Host ""
Write-Host "Found $($completions.Count) completions:" -ForegroundColor Yellow
$completions | ForEach-Object { Write-Host "  $_" }

Write-Host ""
if ($completions.Count -ge 25) {
    Write-Host "✓ PASS - Found expected number of scoop subcommands" -ForegroundColor Green
} else {
    Write-Host "✗ FAIL - Expected ~28 scoop subcommands, got $($completions.Count)" -ForegroundColor Red
}
