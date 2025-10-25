#!/usr/bin/env pwsh
#Requires -Version 7.4

<#
.SYNOPSIS
    Test script for IFeedbackProvider implementation.

.DESCRIPTION
    This script tests the feedback provider by:
    1. Loading the PSCue module
    2. Enabling the PSFeedbackProvider experimental feature (PowerShell 7.4+)
    3. Executing commands to trigger feedback events
    4. Checking if cache scores are being updated
#>

param(
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'

Write-Host "=== PSCue Feedback Provider Test ===" -ForegroundColor Cyan
Write-Host ""

# Check PowerShell version
$psVersion = $PSVersionTable.PSVersion
Write-Host "PowerShell version: $psVersion" -ForegroundColor Green

if ($psVersion.Major -lt 7 -or ($psVersion.Major -eq 7 -and $psVersion.Minor -lt 4)) {
    Write-Host "ERROR: PowerShell 7.4+ is required for IFeedbackProvider" -ForegroundColor Red
    Write-Host "Current version: $psVersion" -ForegroundColor Yellow
    exit 1
}

# Enable experimental feature if not already enabled
Write-Host "Checking PSFeedbackProvider experimental feature..." -ForegroundColor Yellow
$feature = Get-ExperimentalFeature -Name PSFeedbackProvider -ErrorAction SilentlyContinue

if ($null -eq $feature) {
    Write-Host "WARNING: PSFeedbackProvider feature not found" -ForegroundColor Red
    Write-Host "This may indicate PowerShell 7.4 RC or beta version" -ForegroundColor Yellow
}
elseif (-not $feature.Enabled) {
    Write-Host "Enabling PSFeedbackProvider experimental feature..." -ForegroundColor Yellow
    Enable-ExperimentalFeature -Name PSFeedbackProvider -Scope CurrentUser
    Write-Host "Feature enabled. You may need to restart PowerShell." -ForegroundColor Yellow
    Write-Host "Run this script again after restart." -ForegroundColor Yellow
    exit 0
}
else {
    Write-Host "PSFeedbackProvider is already enabled ✓" -ForegroundColor Green
}

Write-Host ""

# Load the module
Write-Host "Loading PSCue module..." -ForegroundColor Yellow
$modulePath = Join-Path $PSScriptRoot "../module/PSCue.psd1"

if (-not (Test-Path $modulePath)) {
    Write-Host "ERROR: Module not found at $modulePath" -ForegroundColor Red
    Write-Host "Run install-local.ps1 first" -ForegroundColor Yellow
    exit 1
}

# Remove module if already loaded to get fresh instance
if (Get-Module PSCue) {
    Remove-Module PSCue -Force
}

Import-Module $modulePath -Force -Verbose:$Verbose

if (Get-Module PSCue) {
    Write-Host "Module loaded successfully ✓" -ForegroundColor Green
}
else {
    Write-Host "ERROR: Failed to load module" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Check if feedback provider is registered
Write-Host "Checking registered feedback providers..." -ForegroundColor Yellow
$feedbackProviders = Get-PSSubsystem -Kind FeedbackProvider -ErrorAction SilentlyContinue

if ($null -eq $feedbackProviders) {
    Write-Host "WARNING: No feedback providers found" -ForegroundColor Yellow
    Write-Host "This may indicate the subsystem is not available" -ForegroundColor Yellow
}
else {
    $pscueFeedback = $feedbackProviders | Where-Object { $_.Name -like "*PSCue*" }

    if ($pscueFeedback) {
        Write-Host "PSCue feedback provider registered ✓" -ForegroundColor Green
        Write-Host "  Name: $($pscueFeedback.Name)" -ForegroundColor Gray
        Write-Host "  Kind: $($pscueFeedback.Kind)" -ForegroundColor Gray
    }
    else {
        Write-Host "WARNING: PSCue feedback provider not found" -ForegroundColor Yellow
        Write-Host "Available providers:" -ForegroundColor Gray
        $feedbackProviders | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
    }
}

Write-Host ""

# Execute some test commands to trigger feedback
Write-Host "Executing test commands to trigger feedback..." -ForegroundColor Yellow
Write-Host ""

# Test 1: Simple command
Write-Host "Test 1: Simple echo command" -ForegroundColor Cyan
$result1 = Invoke-Expression "Write-Output 'Test 1'"
Write-Host "  Result: $result1" -ForegroundColor Gray

Start-Sleep -Milliseconds 500

# Test 2: Git-like command (if git is available)
if (Get-Command git -ErrorAction SilentlyContinue) {
    Write-Host "Test 2: Git status command" -ForegroundColor Cyan
    $result2 = git status --short 2>&1
    Write-Host "  Result: $(if ($result2) { 'Success' } else { 'Empty' })" -ForegroundColor Gray
}
else {
    Write-Host "Test 2: Skipped (git not available)" -ForegroundColor Yellow
}

Start-Sleep -Milliseconds 500

# Test 3: Another simple command
Write-Host "Test 3: Get-Date command" -ForegroundColor Cyan
$result3 = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
Write-Host "  Result: $result3" -ForegroundColor Gray

Write-Host ""
Write-Host "Commands executed. Feedback provider should have processed them." -ForegroundColor Green

Write-Host ""
Write-Host "=== Test Notes ===" -ForegroundColor Cyan
Write-Host "The feedback provider runs asynchronously after each command execution." -ForegroundColor Gray
Write-Host "To verify it's working, you would need to:" -ForegroundColor Gray
Write-Host "  1. Add logging to CommandCompleterFeedbackProvider.OnCommandLineAccepted()" -ForegroundColor Gray
Write-Host "  2. Check CompletionCache statistics" -ForegroundColor Gray
Write-Host "  3. Verify cache scores increase after repeated commands" -ForegroundColor Gray

Write-Host ""
Write-Host "=== Interactive Testing ===" -ForegroundColor Cyan
Write-Host "For manual testing:" -ForegroundColor Gray
Write-Host "  1. Type: git checkout <Tab>" -ForegroundColor Gray
Write-Host "  2. Execute a git checkout command" -ForegroundColor Gray
Write-Host "  3. Type: git checkout <Tab> again" -ForegroundColor Gray
Write-Host "  4. Check if the branch you used appears higher in suggestions" -ForegroundColor Gray

Write-Host ""
Write-Host "Test script completed." -ForegroundColor Green
