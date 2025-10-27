#!/usr/bin/env pwsh
# Test script to verify completion filtering works correctly
# Usage: ./test-completion-filtering.ps1 [scoop|git|all]

param(
    [Parameter(Position = 0)]
    [ValidateSet('scoop', 'git', 'all')]
    [string]$TestSuite = 'all'
)

Write-Host "Testing completion filtering..." -ForegroundColor Cyan
Write-Host ""

# Import the module
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
Write-Host "Module loaded. PID: $PID" -ForegroundColor Green

# Set PSCUE_PID so the ArgumentCompleter can find the IPC server
$env:PSCUE_PID = $PID

Write-Host ""
Write-Host "Waiting for IPC server to start..." -ForegroundColor Yellow
Start-Sleep -Milliseconds 500

$totalTests = 0
$passedTests = 0

function Test-Completion {
    param(
        [string]$Description,
        [string]$CommandLine,
        [int]$CursorPosition,
        [string[]]$ExpectedCompletions
    )

    $script:totalTests++

    Write-Host ""
    Write-Host "Test $script:totalTests`: $Description" -ForegroundColor Cyan

    $result = TabExpansion2 $CommandLine $CursorPosition
    $actualCompletions = $result.CompletionMatches | Select-Object -ExpandProperty CompletionText | Sort-Object

    Write-Host "Found: $($actualCompletions -join ', ')" -ForegroundColor Yellow

    $expectedSorted = $ExpectedCompletions | Sort-Object
    $allMatch = $actualCompletions.Count -eq $expectedSorted.Count

    if ($allMatch) {
        for ($i = 0; $i -lt $actualCompletions.Count; $i++) {
            if ($actualCompletions[$i] -ne $expectedSorted[$i]) {
                $allMatch = $false
                break
            }
        }
    }

    if ($allMatch) {
        Write-Host "✓ PASS" -ForegroundColor Green
        $script:passedTests++
    } else {
        Write-Host "✗ FAIL - Expected: $($expectedSorted -join ', ')" -ForegroundColor Red
    }
}

function Test-CompletionPattern {
    param(
        [string]$Description,
        [string]$CommandLine,
        [int]$CursorPosition,
        [scriptblock]$ValidationScript
    )

    $script:totalTests++

    Write-Host ""
    Write-Host "Test $script:totalTests`: $Description" -ForegroundColor Cyan

    $result = TabExpansion2 $CommandLine $CursorPosition
    $actualCompletions = $result.CompletionMatches | Select-Object -ExpandProperty CompletionText | Sort-Object

    Write-Host "Found $($actualCompletions.Count) completions" -ForegroundColor Yellow

    if (& $ValidationScript $actualCompletions) {
        Write-Host "✓ PASS" -ForegroundColor Green
        $script:passedTests++
    } else {
        Write-Host "✗ FAIL" -ForegroundColor Red
    }
}

# Scoop tests
if ($TestSuite -eq 'scoop' -or $TestSuite -eq 'all') {
    Write-Host ""
    Write-Host "=== Scoop Completion Tests ===" -ForegroundColor Magenta

    Test-Completion `
        -Description "'scoop h' should return only: help, hold, home" `
        -CommandLine 'scoop h' `
        -CursorPosition 7 `
        -ExpectedCompletions @('help', 'hold', 'home')

    Test-Completion `
        -Description "'scoop ho' should return only: hold, home" `
        -CommandLine 'scoop ho' `
        -CursorPosition 8 `
        -ExpectedCompletions @('hold', 'home')

    Test-Completion `
        -Description "'scoop hom' should return only: home" `
        -CommandLine 'scoop hom' `
        -CursorPosition 9 `
        -ExpectedCompletions @('home')

    Test-Completion `
        -Description "'scoop u' should return only: unhold, uninstall, update" `
        -CommandLine 'scoop u' `
        -CursorPosition 7 `
        -ExpectedCompletions @('unhold', 'uninstall', 'update')

    Test-Completion `
        -Description "'scoop c' should return only: cache, cat, checkup, cleanup, config, create" `
        -CommandLine 'scoop c' `
        -CursorPosition 7 `
        -ExpectedCompletions @('cache', 'cat', 'checkup', 'cleanup', 'config', 'create')
}

# Git tests
if ($TestSuite -eq 'git' -or $TestSuite -eq 'all') {
    Write-Host ""
    Write-Host "=== Git Completion Tests ===" -ForegroundColor Magenta

    Test-Completion `
        -Description "'git ch' should return only: checkout, cherry-pick" `
        -CommandLine 'git ch' `
        -CursorPosition 6 `
        -ExpectedCompletions @('checkout', 'cherry-pick')

    Test-Completion `
        -Description "'git che' should return only: checkout, cherry-pick" `
        -CommandLine 'git che' `
        -CursorPosition 7 `
        -ExpectedCompletions @('checkout', 'cherry-pick')

    Test-Completion `
        -Description "'git chec' should return only: checkout" `
        -CommandLine 'git chec' `
        -CursorPosition 8 `
        -ExpectedCompletions @('checkout')

    Test-CompletionPattern `
        -Description "'git commit -' should return flags starting with -" `
        -CommandLine 'git commit -' `
        -CursorPosition 12 `
        -ValidationScript {
            param($completions)
            $completions.Count -ge 5 -and ($completions | Where-Object { $_ -notmatch '^--' }).Count -eq 0
        }

    Test-CompletionPattern `
        -Description "'git commit --a' should return only flags starting with --a" `
        -CommandLine 'git commit --a' `
        -CursorPosition 14 `
        -ValidationScript {
            param($completions)
            $completions.Count -ge 2 -and ($completions | Where-Object { $_ -match '^--a' }).Count -eq $completions.Count
        }
}

# Summary
Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Magenta
Write-Host "Total: $totalTests" -ForegroundColor Cyan
Write-Host "Passed: $passedTests" -ForegroundColor Green
Write-Host "Failed: $($totalTests - $passedTests)" -ForegroundColor $(if ($passedTests -eq $totalTests) { 'Green' } else { 'Red' })

if ($passedTests -eq $totalTests) {
    Write-Host ""
    Write-Host "All tests passed! ✓" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "Some tests failed! ✗" -ForegroundColor Red
    exit 1
}
