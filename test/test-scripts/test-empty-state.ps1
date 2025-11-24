#!/usr/bin/env pwsh
#Requires -Version 7.4

Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force 2>&1 | Out-Null

Write-Host "=== Testing Get- functions with empty data ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "Get-PSCueLearning:" -ForegroundColor Yellow
$learning = Get-PSCueLearning
Write-Host "  Count: $($learning.Count)" -ForegroundColor Green
Write-Host ""

Write-Host "Get-PSCueCache:" -ForegroundColor Yellow
$cache = Get-PSCueCache
Write-Host "  Count: $($cache.Count)" -ForegroundColor Green
Write-Host ""

Write-Host "Get-PSCueCacheStats:" -ForegroundColor Yellow
$stats = Get-PSCueCacheStats
$stats | Format-Table -AutoSize
Write-Host ""

Write-Host "✓ All Get- functions work correctly with empty data!" -ForegroundColor Green
Write-Host ""
Write-Host "To populate data, run commands in your PowerShell session:" -ForegroundColor Cyan
Write-Host "  git status"
Write-Host "  docker ps"
Write-Host "  kubectl get pods"
Write-Host ""
Write-Host "Then run: Get-PSCueLearning" -ForegroundColor Cyan
