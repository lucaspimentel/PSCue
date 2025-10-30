#!/usr/bin/env pwsh
#Requires -Version 7.4

<#
.SYNOPSIS
    Test the new database query functions.

.DESCRIPTION
    Demonstrates Get-PSCueDatabaseStats and Get-PSCueDatabaseHistory
    which read directly from the SQLite database file.
#>

$ErrorActionPreference = 'Stop'

Write-Host "=== Testing Database Functions ===" -ForegroundColor Cyan
Write-Host ""

# Import module
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force 2>&1 | Out-Null

# Get database path
$dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath
Write-Host "Database Location:" -ForegroundColor Yellow
Write-Host "  $dbPath" -ForegroundColor Gray

if (Test-Path $dbPath) {
    $size = (Get-Item $dbPath).Length
    Write-Host "  Size: $([math]::Round($size / 1KB, 2)) KB" -ForegroundColor Gray
} else {
    Write-Host "  Database does not exist yet" -ForegroundColor Gray
}

Write-Host ""

# Test Get-PSCueDatabaseStats
Write-Host "=== Get-PSCueDatabaseStats ===" -ForegroundColor Cyan

if (Test-Path $dbPath) {
    $stats = Get-PSCueDatabaseStats

    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Yellow
    $stats | Select-Object TotalCommands, TotalArguments, TotalHistoryEntries, DatabaseSizeKB | Format-Table -AutoSize

    Write-Host "Top Commands:" -ForegroundColor Yellow
    if ($stats.TopCommands.Count -gt 0) {
        $stats.TopCommands | Format-Table Command, TotalUsage, LastUsed -AutoSize
    } else {
        Write-Host "  (none)" -ForegroundColor Gray
    }
} else {
    Write-Host "  Database does not exist yet" -ForegroundColor Gray
}

Write-Host ""

# Test Get-PSCueDatabaseHistory
Write-Host "=== Get-PSCueDatabaseHistory (last 10) ===" -ForegroundColor Cyan

if (Test-Path $dbPath) {
    $history = Get-PSCueDatabaseHistory -Last 10

    if ($history.Count -gt 0) {
        Write-Host ""
        $history | Format-Table Id, Command, Success, Timestamp -AutoSize
    } else {
        Write-Host "  (no history entries)" -ForegroundColor Gray
    }
} else {
    Write-Host "  Database does not exist yet" -ForegroundColor Gray
}

Write-Host ""

# Test detailed stats
Write-Host "=== Get-PSCueDatabaseStats -Detailed ===" -ForegroundColor Cyan

if (Test-Path $dbPath) {
    $detailed = Get-PSCueDatabaseStats -Detailed

    if ($detailed.DetailedCommands.Count -gt 0) {
        Write-Host ""
        foreach ($cmd in $detailed.DetailedCommands | Select-Object -First 3) {
            Write-Host "Command: $($cmd.Command)" -ForegroundColor Yellow
            Write-Host "  Total Usage: $($cmd.TotalUsage)"
            Write-Host "  Last Used: $($cmd.LastUsed)"
            Write-Host "  Top Arguments:"
            if ($cmd.TopArguments.Count -gt 0) {
                $cmd.TopArguments | Select-Object -First 5 | Format-Table Argument, UsageCount, IsFlag -AutoSize | Out-String | ForEach-Object { "    $_" }
            } else {
                Write-Host "    (none)"
            }
        }
    } else {
        Write-Host "  (no commands)" -ForegroundColor Gray
    }
} else {
    Write-Host "  Database does not exist yet" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Comparison: In-Memory vs Database ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "In-Memory (Get-PSCueLearning):" -ForegroundColor Yellow
$inMemory = @(Get-PSCueLearning)
Write-Host "  Commands in memory: $($inMemory.Count)" -ForegroundColor Gray

if (Test-Path $dbPath) {
    Write-Host ""
    Write-Host "Database (Get-PSCueDatabaseStats):" -ForegroundColor Yellow
    $dbStats = Get-PSCueDatabaseStats
    Write-Host "  Commands in database: $($dbStats.TotalCommands)" -ForegroundColor Gray

    if ($inMemory.Count -ne $dbStats.TotalCommands) {
        Write-Host ""
        Write-Host "⚠️  Mismatch detected!" -ForegroundColor Yellow
        Write-Host "  This can happen if:" -ForegroundColor Gray
        Write-Host "  - The module was just loaded (data not synced yet)" -ForegroundColor Gray
        Write-Host "  - Auto-save hasn't run yet (runs every 5 minutes)" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  Try running: Save-PSCueLearning" -ForegroundColor Cyan
    } else {
        Write-Host ""
        Write-Host "✓ In-memory and database are in sync" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "To generate data, run commands in PowerShell:" -ForegroundColor Cyan
Write-Host "  git status"
Write-Host "  docker ps"
Write-Host "  Then check with: Get-PSCueDatabaseStats"
Write-Host ""
