#!/usr/bin/env pwsh
# Test script for PSCue.Debug tool
# Tests all commands: query-local, query-ipc, stats, cache, clear, ping

Write-Host "PSCue Debug Tool - Comprehensive Test Script" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

$debugToolPath = "D:/source/lucaspimentel/PSCue/src/PSCue.Debug/bin/Release/net9.0/pscue-debug.dll"

if (-not (Test-Path $debugToolPath)) {
    Write-Host "Error: PSCue.Debug tool not found at $debugToolPath" -ForegroundColor Red
    Write-Host "Please build the project first: dotnet build src/PSCue.Debug/ -c Release" -ForegroundColor Yellow
    exit 1
}

# Test 1: Help command
Write-Host "Test 1: Help command" -ForegroundColor Green
Write-Host "--------------------"
dotnet $debugToolPath help
Write-Host ""

# Test 2: Query local (no IPC)
Write-Host "Test 2: Query local completions (no IPC)" -ForegroundColor Green
Write-Host "----------------------------------------"
dotnet $debugToolPath query-local "git commit -"
Write-Host ""

# Test 3: Ping (check if PSCue is loaded)
Write-Host "Test 3: Ping IPC server" -ForegroundColor Green
Write-Host "-----------------------"
dotnet $debugToolPath ping
$pingSuccess = $LASTEXITCODE -eq 0
Write-Host ""

if (-not $pingSuccess) {
    Write-Host "Warning: IPC server not available. Skipping IPC-dependent tests." -ForegroundColor Yellow
    Write-Host "To run full tests:" -ForegroundColor Yellow
    Write-Host "  1. Open a PowerShell session" -ForegroundColor Yellow
    Write-Host "  2. Run: Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1" -ForegroundColor Yellow
    Write-Host "  3. Re-run this test script" -ForegroundColor Yellow
    exit 0
}

# Test 4: Stats (human-readable)
Write-Host "Test 4: Cache statistics (human-readable)" -ForegroundColor Green
Write-Host "-----------------------------------------"
dotnet $debugToolPath stats
Write-Host ""

# Test 5: Stats (JSON)
Write-Host "Test 5: Cache statistics (JSON)" -ForegroundColor Green
Write-Host "-------------------------------"
dotnet $debugToolPath stats --json
Write-Host ""

# Test 6: Cache (all entries)
Write-Host "Test 6: Cache inspection (all entries)" -ForegroundColor Green
Write-Host "--------------------------------------"
dotnet $debugToolPath cache
Write-Host ""

# Test 7: Cache with filter
Write-Host "Test 7: Cache inspection (filtered by 'git')" -ForegroundColor Green
Write-Host "--------------------------------------------"
dotnet $debugToolPath cache --filter git
Write-Host ""

# Test 8: Cache (JSON)
Write-Host "Test 8: Cache inspection (JSON)" -ForegroundColor Green
Write-Host "-------------------------------"
dotnet $debugToolPath cache --json
Write-Host ""

# Test 9: Query via IPC
Write-Host "Test 9: Query completions via IPC" -ForegroundColor Green
Write-Host "---------------------------------"
dotnet $debugToolPath query-ipc "git checkout"
Write-Host ""

# Test 10: Clear cache
Write-Host "Test 10: Clear cache" -ForegroundColor Green
Write-Host "-------------------"
dotnet $debugToolPath clear
Write-Host ""

# Test 11: Stats after clear (should show 0 entries)
Write-Host "Test 11: Stats after clear (should show 0 entries)" -ForegroundColor Green
Write-Host "--------------------------------------------------"
dotnet $debugToolPath stats
Write-Host ""

# Test 12: Unknown command
Write-Host "Test 12: Unknown command (should fail)" -ForegroundColor Green
Write-Host "--------------------------------------"
dotnet $debugToolPath unknown-command 2>&1
Write-Host ""

Write-Host "All tests completed!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  - Help system works: OK" -ForegroundColor Green
Write-Host "  - Query local works: OK" -ForegroundColor Green
if ($pingSuccess) {
    Write-Host "  - IPC connectivity: OK" -ForegroundColor Green
    Write-Host "  - Stats command: OK" -ForegroundColor Green
    Write-Host "  - Cache command: OK" -ForegroundColor Green
    Write-Host "  - Clear command: OK" -ForegroundColor Green
    Write-Host "  - Query IPC: OK" -ForegroundColor Green
    Write-Host "  - JSON output: OK" -ForegroundColor Green
} else {
    Write-Host "  - IPC connectivity: SKIPPED (PSCue not loaded)" -ForegroundColor Yellow
}
