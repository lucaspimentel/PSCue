#!/usr/bin/env pwsh
#Requires -Version 7.2

<#
.SYNOPSIS
    Install PSCue module by downloading pre-built binaries from GitHub releases.

.DESCRIPTION
    This script downloads PSCue from GitHub releases and installs it to ~/.local/pwsh-modules/PSCue/
    No build tools or .NET SDK required.

.PARAMETER Version
    The version to install. Defaults to "latest".
    Example: "1.0.0"

.PARAMETER Force
    Overwrite existing installation without prompting.

.EXAMPLE
    irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex
    Download and install the latest version of PSCue.

.EXAMPLE
    $version = "1.0.0"; irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex
    Download and install a specific version of PSCue.

.EXAMPLE
    ./install-remote.ps1 -Version "1.0.0" -Force
    Install a specific version, overwriting any existing installation.
#>

[CmdletBinding()]
param(
    [string]$Version = "latest",
    [switch]$Force
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

# Detect platform and architecture
$IsWindowsPlatform = $IsWindows -or ($PSVersionTable.PSVersion.Major -le 5)
$Platform = if ($IsWindowsPlatform) { 'win' } elseif ($IsMacOS) { 'osx' } else { 'linux' }
$Architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLower()

# Map architecture names
$RID = "$Platform-$Architecture"
Write-Info "Platform: $RID"

# Validate supported platforms
$SupportedPlatforms = @('win-x64', 'linux-x64')
if ($RID -notin $SupportedPlatforms) {
    Write-Error "Platform '$RID' is not currently supported."
    Write-Info "Supported platforms: $($SupportedPlatforms -join ', ')"
    Write-Info ""
    Write-Info "To build from source, use: ./install-local.ps1"
    exit 1
}

# Map platform to release asset name
$Extension = if ($IsWindowsPlatform) { "zip" } else { "tar.gz" }
$AssetName = "PSCue-$RID.$Extension"
Write-Info "Asset: $AssetName"

# Determine download URL
$RepoOwner = "lucaspimentel"
$RepoName = "PSCue"

Write-Status "Determining download URL..."

if ($Version -eq "latest") {
    # Query GitHub API for latest release
    $ApiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
    Write-Info "Querying GitHub API: $ApiUrl"

    try {
        $Release = Invoke-RestMethod -Uri $ApiUrl -Headers @{ "User-Agent" = "PSCue-Installer" }
        $Version = $Release.tag_name.TrimStart('v')
        Write-Info "Latest version: $Version"

        # Find the matching asset
        $Asset = $Release.assets | Where-Object { $_.name -eq $AssetName }

        if (-not $Asset) {
            Write-Error "Asset '$AssetName' not found in release $($Release.tag_name)"
            Write-Info "Available assets:"
            $Release.assets | ForEach-Object { Write-Info "  - $($_.name)" }
            exit 1
        }

        $DownloadUrl = $Asset.browser_download_url
    } catch {
        Write-Error "Failed to query GitHub API: $_"
        Write-Info "You may need to specify a version manually with -Version parameter"
        exit 1
    }
} else {
    # Use specific version
    $VersionTag = "v$($Version.TrimStart('v'))"
    $DownloadUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/$VersionTag/$AssetName"
    Write-Info "Version: $Version"
}

Write-Info "Download URL: $DownloadUrl"

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

# Create temp directory for download
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "PSCue-install-$(Get-Random)"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
Write-Info "Temp directory: $TempDir"

try {
    # Download release archive
    Write-Status "Downloading PSCue $Version..."
    $ArchivePath = Join-Path $TempDir $AssetName

    try {
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $ArchivePath -UserAgent "PSCue-Installer"
        Write-Info "Downloaded: $ArchivePath"
    } catch {
        Write-Error "Failed to download release: $_"
        Write-Info "URL: $DownloadUrl"
        exit 1
    }

    # Extract archive
    Write-Status "Extracting archive..."
    $ExtractDir = Join-Path $TempDir "extracted"

    if ($IsWindowsPlatform) {
        # Extract ZIP on Windows
        Expand-Archive -Path $ArchivePath -DestinationPath $ExtractDir -Force
    } else {
        # Extract tar.gz on Unix
        New-Item -ItemType Directory -Path $ExtractDir -Force | Out-Null
        & tar -xzf $ArchivePath -C $ExtractDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to extract archive"
            exit 1
        }
    }

    Write-Info "Extracted to: $ExtractDir"

    # Create installation directory
    Write-Status "Installing files..."
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

    # Copy all files from extracted archive to installation directory
    $FilesToCopy = Get-ChildItem -Path $ExtractDir -File

    foreach ($file in $FilesToCopy) {
        Copy-Item -Path $file.FullName -Destination $InstallDir -Force
        Write-Info "  Installed: $($file.Name)"

        # Make pscue-completer executable on Unix systems
        if (-not $IsWindowsPlatform -and $file.Name -eq "pscue-completer") {
            & chmod +x (Join-Path $InstallDir $file.Name)
        }
    }

    # Copy all directories from extracted archive (Functions, runtimes, etc.)
    $DirectoriesToCopy = Get-ChildItem -Path $ExtractDir -Directory

    foreach ($dir in $DirectoriesToCopy) {
        Copy-Item -Path $dir.FullName -Destination $InstallDir -Recurse -Force
        Write-Info "  Installed: $($dir.Name)/"
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

} finally {
    # Clean up temp files
    Write-Status "Cleaning up temporary files..."
    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force
    }
}
