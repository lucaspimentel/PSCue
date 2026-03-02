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

.PARAMETER Update
    Pull the latest changes from the remote before building. Runs 'git pull' on the current branch.

.PARAMETER InstallPath
    Custom installation directory. When specified, installs to this path instead of ~/.local/pwsh-modules/PSCue.
    Useful for dev/test installations that don't conflict with the production module.

.EXAMPLE
    ./install-local.ps1
    Build and install PSCue from source.

.EXAMPLE
    ./install-local.ps1 -Force
    Build and install, overwriting any existing installation.

.EXAMPLE
    ./install-local.ps1 -Update -Force
    Pull latest changes, then build and install.

.EXAMPLE
    ./install-local.ps1 -Force -InstallPath D:\temp\PSCue-dev
    Build and install to a custom directory for dev testing.
#>

[CmdletBinding()]
param(
    [switch]$Force,

    [switch]$Update,

    [string]$InstallPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

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
$RepoRoot = $PSScriptRoot
Write-Info "Repository: $RepoRoot"

# Check if the local clone is up-to-date with remote
Write-Status "Checking if repository is up-to-date..."
try {
    Push-Location $RepoRoot
    $branch = & git rev-parse --abbrev-ref HEAD 2>&1
    & git fetch origin $branch --quiet 2>&1
    $local = & git rev-parse HEAD 2>&1
    $remote = & git rev-parse "origin/$branch" 2>&1

    if ($local -ne $remote) {
        $behind = & git rev-list --count "HEAD..origin/$branch" 2>&1
        $ahead = & git rev-list --count "origin/$branch..HEAD" 2>&1
        $status = @()
        if ([int]$behind -gt 0) { $status += "$behind commit(s) behind" }
        if ([int]$ahead -gt 0) { $status += "$ahead commit(s) ahead of" }
        Write-Warning "Local branch '$branch' is $($status -join ' and ') origin/$branch."

        if ($Update) {
            Write-Status "Pulling latest changes..."
            & git pull --quiet 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Error "git pull failed. Resolve conflicts and try again."
                exit 1
            }
            Write-Info "Repository updated successfully."
        } elseif (-not $Force) {
            $response = Read-Host "Continue anyway? (y/N)"
            if ($response -ne 'y' -and $response -ne 'Y') {
                Write-Info "Installation cancelled. Run 'git pull' or use -Update to update."
                exit 0
            }
        }
    } else {
        Write-Info "Repository is up-to-date with origin/$branch."
    }
} catch {
    Write-Warning "Could not check remote status: $_"
    Write-Warning "Continuing with installation..."
} finally {
    Pop-Location
}

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
    Write-Error ".NET SDK not found. Please install .NET SDK 10 or later."
    Write-Info "Download from: https://dotnet.microsoft.com/download"
    exit 1
}

# Define installation directory
$InstallDir = if ($InstallPath) { $InstallPath } else { Join-Path $HOME ".local/pwsh-modules/PSCue" }
Write-Info "Installation directory: $InstallDir"

# Check if already installed — prompt now, but defer deletion until after builds succeed
$AlreadyInstalled = Test-Path $InstallDir
if ($AlreadyInstalled) {
    if (-not $Force) {
        Write-Warning "PSCue is already installed at: $InstallDir"
        $response = Read-Host "Overwrite existing installation? (y/N)"
        if ($response -ne 'y' -and $response -ne 'Y') {
            Write-Info "Installation cancelled."
            exit 0
        }
    }
}

# Build ArgumentCompleter (NativeAOT)
Write-Status "Building ArgumentCompleter (NativeAOT)..."
$CompleterProject = Join-Path $RepoRoot "src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj"

Push-Location $RepoRoot
try {
    & dotnet publish $CompleterProject -c Release -o publish -r $RID
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build PSCue.ArgumentCompleter"
        exit 1
    }
} finally {
    Pop-Location
}

# Build Module
Write-Status "Building Module..."
$PredictorProject = Join-Path $RepoRoot "src/PSCue.Module/PSCue.Module.csproj"

Push-Location $RepoRoot
try {
    & dotnet publish $PredictorProject -c Release -o publish
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build PSCue.Module"
        exit 1
    }
} finally {
    Pop-Location
}

# Both builds succeeded — now safe to remove existing installation and create fresh directory
if ($AlreadyInstalled) {
    Write-Warning "Removing existing installation..."
    Remove-Item -Path $InstallDir -Recurse -Force
}

# Create installation directory
Write-Status "Creating installation directory..."
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

# Copy files to installation directory
Write-Status "Installing files..."

# Copy ArgumentCompleter executable
$CompleterExe = if ($IsWindowsPlatform) { "pscue-completer.exe" } else { "pscue-completer" }
$CompleterSource = Join-Path $RepoRoot "publish/$CompleterExe"
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

# Copy Module DLL and dependencies
$PredictorSource = Join-Path $RepoRoot "publish"
$PredictorDll = "PSCue.Module.dll"
$PredictorDllSource = Join-Path $PredictorSource $PredictorDll
$PredictorDllDest = Join-Path $InstallDir $PredictorDll

if (Test-Path $PredictorDllSource) {
    Copy-Item -Path $PredictorDllSource -Destination $PredictorDllDest -Force
    Write-Info "  Installed: $PredictorDll"
} else {
    Write-Error "Module DLL not found at: $PredictorDllSource"
    exit 1
}

# Copy required dependencies
$Dependencies = @(
    "PSCue.Shared.dll",
    "PSCue.Module.deps.json",
    "Microsoft.Data.Sqlite.dll",
    "SQLitePCLRaw.batteries_v2.dll",
    "SQLitePCLRaw.core.dll",
    "SQLitePCLRaw.provider.e_sqlite3.dll",
    "Spectre.Console.dll"
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

# Copy native SQLite libraries for current platform
$RuntimesSource = Join-Path $PredictorSource "runtimes/$RID/native"
if (Test-Path $RuntimesSource) {
    $RuntimesDest = Join-Path $InstallDir "runtimes/$RID/native"
    New-Item -ItemType Directory -Path (Split-Path $RuntimesDest -Parent) -Force | Out-Null
    Copy-Item -Path $RuntimesSource -Destination $RuntimesDest -Recurse -Force
    Write-Info "  Installed: runtimes/$RID/native/ (native SQLite libraries)"

    # Also copy native DLL directly to module root for easier loading by PowerShell
    $NativeDll = if ($IsWindowsPlatform) { "e_sqlite3.dll" } elseif ($IsMacOS) { "libe_sqlite3.dylib" } else { "libe_sqlite3.so" }
    $NativeDllSource = Join-Path $RuntimesSource $NativeDll
    if (Test-Path $NativeDllSource) {
        Copy-Item -Path $NativeDllSource -Destination (Join-Path $InstallDir $NativeDll) -Force
        Write-Info "  Installed: $NativeDll (to module root)"
    }
}

# Copy module files
$ModuleSource = Join-Path $RepoRoot "module"
Copy-Item -Path "$ModuleSource/PSCue.psd1" -Destination $InstallDir -Force
Copy-Item -Path "$ModuleSource/PSCue.psm1" -Destination $InstallDir -Force
Write-Info "  Installed: PSCue.psd1"
Write-Info "  Installed: PSCue.psm1"

# Copy Functions directory (PowerShell module functions)
$FunctionsSource = Join-Path $ModuleSource "Functions"
if (Test-Path $FunctionsSource) {
    $FunctionsDest = Join-Path $InstallDir "Functions"
    Copy-Item -Path $FunctionsSource -Destination $FunctionsDest -Recurse -Force
    Write-Info "  Installed: Functions/ (PowerShell module functions)"
}

# Success!
Write-Host ""
Write-Status "PSCue installation complete!"
Write-Host ""

# Display setup instructions
Write-Host "${Bold}Setup Instructions:${Reset}"
Write-Host ""
Write-Host "1. Add PSCue to your PowerShell profile:"
Write-Host ""
$ImportPath = Join-Path $InstallDir "PSCue.psd1"
Write-Host "   ${Cyan}Import-Module $ImportPath${Reset}"
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
