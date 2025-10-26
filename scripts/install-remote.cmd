@echo off
REM Install PSCue module by downloading pre-built binaries from GitHub
REM This script runs the PowerShell installation script using pwsh.

pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-remote.ps1" %*
