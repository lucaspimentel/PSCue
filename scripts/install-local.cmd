@echo off
REM Install PSCue module by building from source
REM This script runs the PowerShell installation script using pwsh.

pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-local.ps1" %*
