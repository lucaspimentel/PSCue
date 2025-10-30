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

# Test 9: Test other functions
Write-TestHeader "Other Module Functions"

try {
    $moduleInfo = Get-PSCueModuleInfo
    Write-TestSuccess "Get-PSCueModuleInfo works"
    if ($Verbose) {
        $moduleInfo | Format-List
    }
} catch {
    Write-TestFailure "Get-PSCueModuleInfo failed: $_"
}

# Test 10: Check if predictors are registered
Write-TestHeader "Registered Subsystems"

try {
    # Try to get registered predictors (this is internal PowerShell stuff)
    $predictors = [System.Management.Automation.Subsystem.SubsystemManager]::GetSubsystems(
        [System.Management.Automation.Subsystem.SubsystemKind]::CommandPredictor
    )

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
    $feedbackProviders = [System.Management.Automation.Subsystem.SubsystemManager]::GetSubsystems(
        [System.Management.Automation.Subsystem.SubsystemKind]::FeedbackProvider
    )

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
