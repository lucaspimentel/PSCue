#!/usr/bin/env pwsh
# Test script to reproduce the "scoop h<tab> then scoop <tab>" issue

Write-Host "Setting up test environment..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID
$env:PSCUE_DEBUG = "1"
Start-Sleep -Milliseconds 500

Write-Host ""
Write-Host "=== Test 1: scoop h<tab> ===" -ForegroundColor Magenta
$result1 = TabExpansion2 'scoop h' 7
$completions1 = $result1.CompletionMatches | Select-Object -ExpandProperty CompletionText | Sort-Object
Write-Host "Found: $($completions1 -join ', ')" -ForegroundColor Yellow
Write-Host "Count: $($completions1.Count)" -ForegroundColor Cyan

Write-Host ""
Write-Host "=== Test 2: scoop <tab> (empty prefix) ===" -ForegroundColor Magenta
$result2 = TabExpansion2 'scoop ' 6
$completions2 = $result2.CompletionMatches | Select-Object -ExpandProperty CompletionText | Sort-Object
Write-Host "Found: $($completions2 -join ', ')" -ForegroundColor Yellow
Write-Host "Count: $($completions2.Count)" -ForegroundColor Cyan

Write-Host ""
if ($completions2.Count -ge 25) {
    Write-Host "✓ PASS - Got all scoop subcommands" -ForegroundColor Green
} else {
    Write-Host "✗ FAIL - Expected ~28 subcommands, got $($completions2.Count)" -ForegroundColor Red
    Write-Host "This suggests the cache is incorrectly filtered" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Check the log file for details:" -ForegroundColor Cyan
Write-Host "  $env:LOCALAPPDATA\PSCue\log.txt" -ForegroundColor Yellow
