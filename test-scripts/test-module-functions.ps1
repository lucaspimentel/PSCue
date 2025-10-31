#!/usr/bin/env pwsh
#Requires -Version 7.4

<#
.SYNOPSIS
    Test script to verify PSCue module functions are working correctly.

.DESCRIPTION
    This script tests the PSCue module installation, initialization, and Get- functions.
    It generates some test data by simulating command execution and then verifies
    that the learning system and cache are working properly.
#>

param(
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'

# ANSI colors
$Green = "`e[32m"
$Red = "`e[31m"
$Yellow = "`e[33m"
$Cyan = "`e[36m"
$Bold = "`e[1m"
$Reset = "`e[0m"

function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n${Cyan}${Bold}=== $Message ===${Reset}" -ForegroundColor Cyan
}

function Write-TestSuccess {
    param([string]$Message)
    Write-Host "${Green}✓${Reset} $Message" -ForegroundColor Green
}

function Write-TestFailure {
    param([string]$Message)
    Write-Host "${Red}✗${Reset} $Message" -ForegroundColor Red
}

function Write-TestInfo {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

# Test 1: Check PowerShell version and experimental features
Write-TestHeader "PowerShell Environment"

$psVersion = $PSVersionTable.PSVersion
Write-TestInfo "PowerShell Version: $psVersion"

if ($psVersion -lt [Version]"7.4.0") {
    Write-TestFailure "PowerShell 7.4+ required for IFeedbackProvider (learning system)"
    Write-TestInfo "Current version: $psVersion"
    Write-TestInfo "Learning features will not work, but completions will still function"
} else {
    Write-TestSuccess "PowerShell 7.4+ detected"
}

# Check if PSFeedbackProvider experimental feature is enabled
$feedbackFeature = Get-ExperimentalFeature -Name PSFeedbackProvider -ErrorAction SilentlyContinue
if ($feedbackFeature -and $feedbackFeature.Enabled) {
    Write-TestSuccess "PSFeedbackProvider experimental feature is enabled"
} else {
    Write-TestFailure "PSFeedbackProvider experimental feature is NOT enabled"
    Write-TestInfo "Enable with: Enable-ExperimentalFeature PSFeedbackProvider"
    Write-TestInfo "Then restart PowerShell"
}

# Test 2: Import module
Write-TestHeader "Module Import"

$modulePath = "~/.local/pwsh-modules/PSCue/PSCue.psd1"
$resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($modulePath)

if (-not (Test-Path $resolvedPath)) {
    Write-TestFailure "Module not found at: $resolvedPath"
    Write-TestInfo "Run: ./scripts/install-local.ps1 -Force"
    exit 1
}

try {
    Import-Module $resolvedPath -Force
    Write-TestSuccess "Module imported successfully"
} catch {
    Write-TestFailure "Failed to import module: $_"
    exit 1
}

# Test 3: Check module initialization
Write-TestHeader "Module Initialization"

$cacheInit = [PSCue.Module.PSCueModule]::Cache -ne $null
$graphInit = [PSCue.Module.PSCueModule]::KnowledgeGraph -ne $null
$historyInit = [PSCue.Module.PSCueModule]::CommandHistory -ne $null
$persistInit = [PSCue.Module.PSCueModule]::Persistence -ne $null

if ($cacheInit) {
    Write-TestSuccess "CompletionCache initialized"
} else {
    Write-TestFailure "CompletionCache NOT initialized"
}

if ($graphInit) {
    Write-TestSuccess "ArgumentGraph (KnowledgeGraph) initialized"
} else {
    Write-TestFailure "ArgumentGraph NOT initialized"
}

if ($historyInit) {
    Write-TestSuccess "CommandHistory initialized"
} else {
    Write-TestFailure "CommandHistory NOT initialized"
}

if ($persistInit) {
    Write-TestSuccess "PersistenceManager initialized"
    $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath
    Write-TestInfo "Database: $dbPath"
} else {
    Write-TestFailure "PersistenceManager NOT initialized"
}

# Test 4: Check if learning is disabled
Write-TestHeader "Learning Configuration"

$learningDisabled = $env:PSCUE_DISABLE_LEARNING -eq "true"
if ($learningDisabled) {
    Write-TestFailure "Learning is DISABLED (PSCUE_DISABLE_LEARNING=true)"
    Write-TestInfo "Remove env variable to enable learning"
} else {
    Write-TestSuccess "Learning is enabled"
}

# Test 5: Test Get-PSCueCacheStats
Write-TestHeader "Get-PSCueCacheStats"

try {
    $stats = Get-PSCueCacheStats
    Write-TestSuccess "Get-PSCueCacheStats works"
    $stats | Format-Table -AutoSize
} catch {
    Write-TestFailure "Get-PSCueCacheStats failed: $_"
}

# Test 6: Test Get-PSCueCache
Write-TestHeader "Get-PSCueCache"

try {
    $cacheEntries = @(Get-PSCueCache)
    Write-TestSuccess "Get-PSCueCache works"
    Write-TestInfo "Cache entries: $($cacheEntries.Count)"
    if ($cacheEntries.Count -gt 0) {
        $cacheEntries | Select-Object -First 3 | Format-Table -AutoSize
    } else {
        Write-TestInfo "Cache is empty (expected for fresh installation)"
    }
} catch {
    Write-TestFailure "Get-PSCueCache failed: $_"
}

# Test 7: Test Get-PSCueLearning
Write-TestHeader "Get-PSCueLearning"

try {
    $learningData = @(Get-PSCueLearning)
    Write-TestSuccess "Get-PSCueLearning works"
    Write-TestInfo "Learned commands: $($learningData.Count)"
    if ($learningData.Count -gt 0) {
        $learningData | Select-Object -First 5 | Format-Table -AutoSize
    } else {
        Write-TestInfo "No learned data yet (expected for fresh installation)"
        Write-TestInfo "Learning data is populated by executing commands in PowerShell"
    }
} catch {
    Write-TestFailure "Get-PSCueLearning failed: $_"
}

# Test 8: Simulate some commands to generate learning data
Write-TestHeader "Simulating Command Execution"

Write-TestInfo "The learning system learns from actual command execution via IFeedbackProvider"
Write-TestInfo "It cannot be directly tested in this script as it requires PowerShell's feedback system"
Write-TestInfo ""
Write-TestInfo "To generate learning data, run commands in your PowerShell session:"
Write-TestInfo "  - git status"
Write-TestInfo "  - git log"
Write-TestInfo "  - docker ps"
Write-TestInfo "  - kubectl get pods"
Write-TestInfo ""
Write-TestInfo "Then run: Get-PSCueLearning"

# Test 9: Test Get-PSCueModuleInfo
Write-TestHeader "Get-PSCueModuleInfo"

try {
    $moduleInfo = Get-PSCueModuleInfo
    Write-TestSuccess "Get-PSCueModuleInfo works"
    if ($Verbose) {
        $moduleInfo | Format-List
    }
} catch {
    Write-TestFailure "Get-PSCueModuleInfo failed: $_"
}

# Test 9a: Test -AsJson parameter for functions that support it
Write-TestHeader "Testing -AsJson Parameters"

try {
    $jsonStats = Get-PSCueCacheStats -AsJson
    $null = ConvertFrom-Json $jsonStats
    Write-TestSuccess "Get-PSCueCacheStats -AsJson produces valid JSON"
} catch {
    Write-TestFailure "Get-PSCueCacheStats -AsJson failed: $_"
}

try {
    $jsonCache = Get-PSCueCache -AsJson
    $null = ConvertFrom-Json $jsonCache
    Write-TestSuccess "Get-PSCueCache -AsJson produces valid JSON"
} catch {
    Write-TestFailure "Get-PSCueCache -AsJson failed: $_"
}

try {
    $jsonLearning = Get-PSCueLearning -AsJson
    $null = ConvertFrom-Json $jsonLearning
    Write-TestSuccess "Get-PSCueLearning -AsJson produces valid JSON"
} catch {
    Write-TestFailure "Get-PSCueLearning -AsJson failed: $_"
}

try {
    $jsonInfo = Get-PSCueModuleInfo -AsJson
    $null = ConvertFrom-Json $jsonInfo
    Write-TestSuccess "Get-PSCueModuleInfo -AsJson produces valid JSON"
} catch {
    Write-TestFailure "Get-PSCueModuleInfo -AsJson failed: $_"
}

# Test 9b: Test Get-PSCueCache with -Filter parameter
Write-TestHeader "Testing Get-PSCueCache -Filter"

try {
    $filtered = @(Get-PSCueCache -Filter "git")
    Write-TestSuccess "Get-PSCueCache -Filter works"
    Write-TestInfo "Filtered entries: $($filtered.Count)"
} catch {
    Write-TestFailure "Get-PSCueCache -Filter failed: $_"
}

# Test 9c: Test Get-PSCueLearning with -Command parameter
Write-TestHeader "Testing Get-PSCueLearning -Command"

try {
    $cmdLearning = @(Get-PSCueLearning -Command "git")
    Write-TestSuccess "Get-PSCueLearning -Command works"
    Write-TestInfo "Command-specific entries: $($cmdLearning.Count)"
} catch {
    Write-TestFailure "Get-PSCueLearning -Command failed: $_"
}

# Test 9d: Test Get-PSCueDatabaseStats
Write-TestHeader "Get-PSCueDatabaseStats"

try {
    $dbStats = Get-PSCueDatabaseStats
    Write-TestSuccess "Get-PSCueDatabaseStats works"
    if ($Verbose) {
        $dbStats | Format-List
    }
} catch {
    Write-TestFailure "Get-PSCueDatabaseStats failed: $_"
}

try {
    $dbStatsDetailed = Get-PSCueDatabaseStats -Detailed
    Write-TestSuccess "Get-PSCueDatabaseStats -Detailed works"
} catch {
    Write-TestFailure "Get-PSCueDatabaseStats -Detailed failed: $_"
}

try {
    $dbStatsJson = Get-PSCueDatabaseStats -AsJson
    $null = ConvertFrom-Json $dbStatsJson
    Write-TestSuccess "Get-PSCueDatabaseStats -AsJson produces valid JSON"
} catch {
    Write-TestFailure "Get-PSCueDatabaseStats -AsJson failed: $_"
}

# Test 9e: Test Get-PSCueDatabaseHistory
Write-TestHeader "Get-PSCueDatabaseHistory"

try {
    $dbHistory = @(Get-PSCueDatabaseHistory -Last 10)
    Write-TestSuccess "Get-PSCueDatabaseHistory -Last works"
    Write-TestInfo "History entries: $($dbHistory.Count)"
} catch {
    Write-TestFailure "Get-PSCueDatabaseHistory -Last failed: $_"
}

try {
    $dbHistoryCmd = @(Get-PSCueDatabaseHistory -Command "git")
    Write-TestSuccess "Get-PSCueDatabaseHistory -Command works"
    Write-TestInfo "Command-specific history: $($dbHistoryCmd.Count)"
} catch {
    Write-TestFailure "Get-PSCueDatabaseHistory -Command failed: $_"
}

try {
    $dbHistoryJson = Get-PSCueDatabaseHistory -Last 5 -AsJson
    $null = ConvertFrom-Json $dbHistoryJson
    Write-TestSuccess "Get-PSCueDatabaseHistory -AsJson produces valid JSON"
} catch {
    Write-TestFailure "Get-PSCueDatabaseHistory -AsJson failed: $_"
}

# Test 9f: Test Test-PSCueCompletion
Write-TestHeader "Test-PSCueCompletion"

try {
    $completions = Test-PSCueCompletion -InputString "git che"
    Write-TestSuccess "Test-PSCueCompletion works"
    Write-TestInfo "Completions found: $($completions.Count)"
    if ($completions.Count -gt 0) {
        $completions | Select-Object -First 3 | Format-Table CompletionText, ListItemText -AutoSize
    }
} catch {
    Write-TestFailure "Test-PSCueCompletion failed: $_"
}

# Test 9g: Test Export/Import-PSCueLearning
Write-TestHeader "Export/Import-PSCueLearning"

$exportPath = Join-Path $env:TEMP "pscue-test-export.json"

try {
    Export-PSCueLearning -Path $exportPath
    Write-TestSuccess "Export-PSCueLearning works"
    Write-TestInfo "Exported to: $exportPath"

    if (Test-Path $exportPath) {
        $exportContent = Get-Content $exportPath -Raw
        $null = ConvertFrom-Json $exportContent
        Write-TestSuccess "Export file contains valid JSON"

        # Test import (use -Confirm:$false to avoid prompting)
        Import-PSCueLearning -Path $exportPath -Confirm:$false
        Write-TestSuccess "Import-PSCueLearning works"

        # Test import with -Merge
        Import-PSCueLearning -Path $exportPath -Merge -Confirm:$false
        Write-TestSuccess "Import-PSCueLearning -Merge works"

        # Clean up
        Remove-Item $exportPath -ErrorAction SilentlyContinue
    }
} catch {
    Write-TestFailure "Export/Import-PSCueLearning failed: $_"
}

# Test 9h: Test error handling - invalid path
Write-TestHeader "Error Handling"

try {
    $null = Export-PSCueLearning -Path "Z:\invalid\path\file.json" -ErrorAction Stop
    Write-TestFailure "Export-PSCueLearning should fail with invalid path"
} catch {
    Write-TestSuccess "Export-PSCueLearning correctly handles invalid path"
}

try {
    $null = Import-PSCueLearning -Path "Z:\invalid\path\file.json" -ErrorAction Stop
    Write-TestFailure "Import-PSCueLearning should fail with invalid path"
} catch {
    Write-TestSuccess "Import-PSCueLearning correctly handles invalid path"
}

# Test 9i: Test WhatIf/Confirm for destructive operations
Write-TestHeader "WhatIf/Confirm Support"

try {
    # Test Clear-PSCueCache with -WhatIf
    Clear-PSCueCache -WhatIf
    Write-TestSuccess "Clear-PSCueCache supports -WhatIf"
} catch {
    Write-TestFailure "Clear-PSCueCache -WhatIf failed: $_"
}

try {
    # Test Clear-PSCueLearning with -WhatIf
    Clear-PSCueLearning -WhatIf
    Write-TestSuccess "Clear-PSCueLearning supports -WhatIf"
} catch {
    Write-TestFailure "Clear-PSCueLearning -WhatIf failed: $_"
}

# Test 9j: Test Save-PSCueLearning
Write-TestHeader "Save-PSCueLearning"

try {
    Save-PSCueLearning
    Write-TestSuccess "Save-PSCueLearning works"
} catch {
    Write-TestFailure "Save-PSCueLearning failed: $_"
}

# Test 10: Check if predictors are registered
Write-TestHeader "Registered Subsystems"

try {
    # Use Get-PSSubsystem cmdlet (available in PS 7.4+)
    $predictors = Get-PSSubsystem -Kind CommandPredictor -ErrorAction SilentlyContinue

    $pscuePredictor = $predictors | Where-Object { $_.Name -eq 'PSCue' }
    if ($pscuePredictor) {
        Write-TestSuccess "PSCue CommandPredictor is registered"
        Write-TestInfo "ID: $($pscuePredictor.Id)"
    } else {
        Write-TestFailure "PSCue CommandPredictor is NOT registered"
    }
} catch {
    Write-TestInfo "Could not check registered predictors: $_"
}

try {
    $feedbackProviders = Get-PSSubsystem -Kind FeedbackProvider -ErrorAction SilentlyContinue

    $pscueFeedback = $feedbackProviders | Where-Object { $_.Name -eq 'PSCue.FeedbackProvider' }
    if ($pscueFeedback) {
        Write-TestSuccess "PSCue FeedbackProvider is registered"
        Write-TestInfo "ID: $($pscueFeedback.Id)"
    } else {
        Write-TestFailure "PSCue FeedbackProvider is NOT registered"
        Write-TestInfo "Requires PowerShell 7.4+ with PSFeedbackProvider experimental feature"
    }
} catch {
    Write-TestInfo "Could not check registered feedback providers: $_"
}

# Summary
Write-TestHeader "Summary"

Write-Host ""
Write-Host "${Bold}Next Steps:${Reset}"
Write-Host "1. Run some commands in your PowerShell session to generate learning data"
Write-Host "2. Check learned data with: ${Cyan}Get-PSCueLearning${Reset}"
Write-Host "3. Try tab completion: ${Cyan}git che<TAB>${Reset}"
Write-Host "4. Try inline predictions: ${Cyan}git ${Reset}(wait for suggestion)"
Write-Host ""
Write-Host "For more information: ${Cyan}Get-Help Get-PSCueLearning${Reset}"
Write-Host ""
