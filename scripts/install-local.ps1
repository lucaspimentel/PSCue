#!/usr/bin/env pwsh
#Requires -Version 7.2

<#
.SYNOPSIS
    Install PSCue module by building from source.

.DESCRIPTION
    This script builds PSCue from source and installs it to ~/.local/pwsh-modules/PSCue/
    Requires .NET 9.0 SDK to be installed.

.PARAMETER Force
    Overwrite existing installation without prompting.

.EXAMPLE
    ./scripts/install-local.ps1
    Build and install PSCue from source.

.EXAMPLE
    ./scripts/install-local.ps1 -Force
    Build and install, overwriting any existing installation.
#>

[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ANSI color codes
$Reset = "`e[0m"
$Green = "`e[32m"
$Yellow = "`e[33m"
$Red = "`e[31m"
$Cyan = "`e[36m"
$Bold = "`e[1m"

function Write-Status {
    param([string]$Message)
    Write-Host "${Green}${Bold}==>${Reset} ${Message}" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "${Cyan}${Message}${Reset}" -ForegroundColor Cyan
}

function Write-Warning {
    param([string]$Message)
    Write-Host "${Yellow}Warning: ${Message}${Reset}" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "${Red}Error: ${Message}${Reset}" -ForegroundColor Red
}

# Get the repository root directory
$RepoRoot = Split-Path $PSScriptRoot -Parent
Write-Info "Repository: $RepoRoot"

# Detect platform and architecture
$IsWindowsPlatform = $IsWindows -or ($PSVersionTable.PSVersion.Major -le 5)
$Platform = if ($IsWindowsPlatform) { 'win' } elseif ($IsMacOS) { 'osx' } else { 'linux' }
$Architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLower()

# Map architecture names
$RID = "$Platform-$Architecture"
Write-Info "Platform: $RID"

# Check for .NET SDK
Write-Status "Checking for .NET SDK..."
try {
    $dotnetVersion = & dotnet --version 2>&1
    Write-Info ".NET SDK version: $dotnetVersion"
} catch {
    Write-Error ".NET SDK not found. Please install .NET 9.0 SDK or later."
    Write-Info "Download from: https://dotnet.microsoft.com/download"
    exit 1
}

# Define installation directory
$InstallDir = Join-Path $HOME ".local/pwsh-modules/PSCue"
Write-Info "Installation directory: $InstallDir"

# Check if already installed
if (Test-Path $InstallDir) {
    if ($Force) {
        Write-Warning "Removing existing installation..."
        Remove-Item -Path $InstallDir -Recurse -Force
    } else {
        Write-Warning "PSCue is already installed at: $InstallDir"
        $response = Read-Host "Overwrite existing installation? (y/N)"
        if ($response -ne 'y' -and $response -ne 'Y') {
            Write-Info "Installation cancelled."
            exit 0
        }
        Remove-Item -Path $InstallDir -Recurse -Force
    }
}

# Create installation directory
Write-Status "Creating installation directory..."
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

# Build ArgumentCompleter (NativeAOT)
Write-Status "Building ArgumentCompleter (NativeAOT)..."
$CompleterProject = Join-Path $RepoRoot "src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj"

Push-Location $RepoRoot
try {
    & dotnet publish $CompleterProject -c Release -r $RID --self-contained
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build ArgumentCompleter"
        exit 1
    }
} finally {
    Pop-Location
}

# Build CommandPredictor
Write-Status "Building CommandPredictor..."
$PredictorProject = Join-Path $RepoRoot "src/PSCue.CommandPredictor/PSCue.CommandPredictor.csproj"

Push-Location $RepoRoot
try {
    & dotnet build $PredictorProject -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build CommandPredictor"
        exit 1
    }
} finally {
    Pop-Location
}

# Copy files to installation directory
Write-Status "Installing files..."

# Copy ArgumentCompleter executable
$CompleterExe = if ($IsWindowsPlatform) { "pscue-completer.exe" } else { "pscue-completer" }
$CompleterSource = Join-Path $RepoRoot "src/PSCue.ArgumentCompleter/bin/Release/net9.0/$RID/publish/$CompleterExe"
$CompleterDest = Join-Path $InstallDir $CompleterExe

if (Test-Path $CompleterSource) {
    Copy-Item -Path $CompleterSource -Destination $CompleterDest -Force
    Write-Info "  Installed: $CompleterExe"

    # Make executable on Unix systems
    if (-not $IsWindowsPlatform) {
        & chmod +x $CompleterDest
    }
} else {
    Write-Error "ArgumentCompleter executable not found at: $CompleterSource"
    exit 1
}

# Copy CommandPredictor DLL and dependencies
$PredictorSource = Join-Path $RepoRoot "src/PSCue.CommandPredictor/bin/Release/net9.0"
$PredictorDll = "PSCue.CommandPredictor.dll"
$PredictorDllSource = Join-Path $PredictorSource $PredictorDll
$PredictorDllDest = Join-Path $InstallDir $PredictorDll

if (Test-Path $PredictorDllSource) {
    Copy-Item -Path $PredictorDllSource -Destination $PredictorDllDest -Force
    Write-Info "  Installed: $PredictorDll"
} else {
    Write-Error "CommandPredictor DLL not found at: $PredictorDllSource"
    exit 1
}

# Copy required dependencies
$Dependencies = @(
    "PSCue.Shared.dll"
    "PSCue.ArgumentCompleter.dll"
)

foreach ($dep in $Dependencies) {
    $depSource = Join-Path $PredictorSource $dep
    $depDest = Join-Path $InstallDir $dep

    if (Test-Path $depSource) {
        Copy-Item -Path $depSource -Destination $depDest -Force
        Write-Info "  Installed: $dep"
    } else {
        Write-Warning "Dependency not found: $dep (may not be required)"
    }
}

# Copy module files
$ModuleSource = Join-Path $RepoRoot "module"
Copy-Item -Path "$ModuleSource/PSCue.psd1" -Destination $InstallDir -Force
Copy-Item -Path "$ModuleSource/PSCue.psm1" -Destination $InstallDir -Force
Write-Info "  Installed: PSCue.psd1"
Write-Info "  Installed: PSCue.psm1"

# Success!
Write-Host ""
Write-Status "PSCue installation complete!"
Write-Host ""

# Display setup instructions
Write-Host "${Bold}Setup Instructions:${Reset}"
Write-Host ""
Write-Host "1. Add PSCue to your PowerShell profile:"
Write-Host ""
Write-Host "   ${Cyan}Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1${Reset}"
Write-Host ""
Write-Host "2. Enable inline predictions (recommended):"
Write-Host ""
Write-Host "   ${Cyan}Set-PSReadLineOption -PredictionSource HistoryAndPlugin${Reset}"
Write-Host ""
Write-Host "3. Restart your PowerShell session or reload your profile:"
Write-Host ""
Write-Host "   ${Cyan}. `$PROFILE${Reset}"
Write-Host ""
Write-Host "For more information, visit: ${Cyan}https://github.com/lucaspimentel/PSCue${Reset}"
Write-Host ""
