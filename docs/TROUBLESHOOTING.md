# PSCue Troubleshooting Guide

This document covers common issues and their solutions when using PSCue.

---

## Table of Contents

- [PCD (Smart Directory Navigation)](#pcd-smart-directory-navigation)
  - [Duplicate Directory Suggestions](#duplicate-directory-suggestions)
- [Learning System](#learning-system)
- [Performance Issues](#performance-issues)
- [Installation & Setup](#installation--setup)

---

## PCD (Smart Directory Navigation)

### Interactive Mode Edge Cases

**No learned data yet**:
```
No learned directories yet.
Use 'pcd <path>' to navigate and build history.
```

**All learned paths have been deleted or moved**:
```
No valid directories in history.
All learned paths have been deleted or moved.
```

**Module not initialized**:
```
PSCue module not initialized. Cannot show interactive selection.
```

**Non-interactive terminal** (e.g. redirected/piped session):
```
Error: Cannot show interactive prompt in this terminal.
Try running in Windows Terminal or use regular 'pcd' commands.
```

### Interactive Mode: Spectre.Console.dll Not Found

**Problem**: Missing DLL error when running `pcd -i`.

**Solution**: Reinstall using the install script, which includes all dependencies:
```powershell
./install-local.ps1 -Force
# or
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex
```

### Interactive Mode: Menu Not Showing

Check that:
1. You're running in an interactive terminal (not redirected or piped)
2. PSCue module is fully initialized (`Get-Module PSCue`)
3. You have some learned navigation history (`Get-PSCueLearning -Command cd`)

---

### Duplicate Directory Suggestions

**Problem**: The same directory appears twice in `pcd` suggestions with different paths (e.g., `C:\Users\lucas\source\...` and `D:\source\...`).

**Cause**: One path is a symlink, junction, or directory link pointing to the other. Older versions of PSCue (before Phase 21.1) did not resolve symlinks before storing paths.

**Solution** (PSCue v0.4.0+):
- **Automatic**: PSCue now automatically resolves symlinks, junctions, and directory links to their real paths
- All paths are normalized to their canonical real paths before storage
- Duplicates are automatically prevented

**For older versions** (before Phase 21.1):
1. Clear your learned data to remove duplicates:
   ```powershell
   Clear-PSCueLearning -Force
   ```

2. Reinstall PSCue to get the latest version:
   ```powershell
   irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex
   ```

3. Restart PowerShell and rebuild your learned paths by navigating to your frequently-used directories.

**Technical Details**:
- Symlink resolution happens during path normalization in `ArgumentGraph.RecordUsage()`
- Requires the `workingDirectory` parameter to be provided
- Uses `FileInfo.LinkTarget` to resolve reparse points
- Works with Windows symlinks, junctions, and directory links
- Also works with Unix symbolic links on Linux/macOS

**Verification**:
After clearing and rebuilding, run:
```powershell
Get-PSCueLearning -Command "cd" -AsJson | ConvertFrom-Json |
  Select-Object -ExpandProperty Arguments |
  Where-Object { $_.Argument -like "*your-path*" }
```

You should see only one entry per unique directory (the resolved real path).

---

## Learning System

### Learning Not Working

**Problem**: PSCue doesn't seem to learn from your commands.

**Possible Causes**:
1. **Learning disabled**: Check if `$env:PSCUE_DISABLE_LEARNING` is set to `"true"`
2. **Module not loaded**: Verify with `Get-Module PSCue`
3. **PowerShell version**: IFeedbackProvider requires PowerShell 7.4+

**Solutions**:
```powershell
# Check if learning is disabled
$env:PSCUE_DISABLE_LEARNING  # Should be empty or "false"

# Verify module is loaded
Get-Module PSCue

# Check PowerShell version (need 7.4+ for learning)
$PSVersionTable.PSVersion
```

### Corrupted Database

**Problem**: PSCue fails to initialize with database errors.

**Solution**:
```powershell
# Force delete the database without requiring module initialization
Clear-PSCueLearning -Force
```

This removes the SQLite database files directly from:
- Windows: `%LOCALAPPDATA%\PSCue\learned-data.db`
- Linux/macOS: `~/.local/share/PSCue/learned-data.db`

---

## Performance Issues

### Tab Completion Slow (>50ms)

**Problem**: Tab completion takes longer than expected.

**Common Causes**:
1. **Network drives**: Accessing network paths can be slow
2. **Large directory trees**: Recursive search with deep hierarchies
3. **Many learned entries**: Thousands of learned arguments

**Solutions**:

**Reduce PCD recursive search depth**:
```powershell
# For tab completion (default: 3)
$env:PSCUE_PCD_MAX_DEPTH = "2"

# For inline predictor (default: 1)
$env:PSCUE_PCD_PREDICTOR_MAX_DEPTH = "1"
```

**Disable recursive search entirely**:
```powershell
$env:PSCUE_PCD_RECURSIVE_SEARCH = "false"
```

**Limit learned entries**:
```powershell
$env:PSCUE_MAX_COMMANDS = "250"         # Default: 500
$env:PSCUE_MAX_ARGS_PER_CMD = "50"      # Default: 100
```

### Inline Predictions Slow

**Problem**: Predictions appear with noticeable delay.

**Solution**: The inline predictor should be <10ms. If slower:
1. Reduce PCD predictor search depth (see above)
2. Check if disk I/O is slow (database queries)
3. Consider disabling features:
   ```powershell
   # Disable ML sequence predictions
   $env:PSCUE_ML_ENABLED = "false"

   # Disable workflow learning
   $env:PSCUE_WORKFLOW_LEARNING = "false"
   ```

---

## Installation & Setup

### Module Not Loading

**Problem**: `Import-Module PSCue` fails or module doesn't load on startup.

**Solutions**:

**Check module path**:
```powershell
Get-Module PSCue -ListAvailable
```

**Manually import**:
```powershell
Import-Module PSCue -Force -Verbose
```

**Check PowerShell profile**:
```powershell
# View profile path
$PROFILE

# Edit profile
code $PROFILE  # or notepad $PROFILE

# Profile should contain:
Import-Module PSCue
```

### ArgumentCompleter Not Found

**Problem**: Tab completion doesn't work, errors about missing `pscue-completer.exe`.

**Cause**: The NativeAOT executable is missing or in wrong location.

**Solution**:
```powershell
# Reinstall PSCue
irm https://raw.githubusercontent.com/lucaspimentel/PSCue/main/install-remote.ps1 | iex
```

**Verify installation**:
```powershell
# Check if completer exists
$modulePath = (Get-Module PSCue -ListAvailable | Select-Object -First 1).ModuleBase
Test-Path "$modulePath\bin\pscue-completer.exe"  # Should return True
```

### Git Commit Fails with 1Password Error

**Problem**: Cannot commit changes due to 1Password authentication.

**Note**: This is documented in `CLAUDE.md` as a reminder for AI agents, not a PSCue issue. If you encounter this while testing PSCue development, the user is AFK and 1Password is awaiting authentication.

---

## Getting Help

If you encounter issues not covered here:

1. **Check the documentation**:
   - [README.md](../README.md) - User guide and features
   - [CLAUDE.md](../CLAUDE.md) - Technical reference
   - [docs/TECHNICAL_DETAILS.md](TECHNICAL_DETAILS.md) - Architecture details

2. **View debug information**:
   ```powershell
   Get-PSCueModuleInfo -AsJson
   Get-PSCueDatabaseStats -Detailed
   ```

3. **Test completions**:
   ```powershell
   Test-PSCueCompletion -InputString "git sta"
   ```

4. **Report an issue**:
   - GitHub: https://github.com/lucaspimentel/PSCue/issues
   - Include: PowerShell version, OS, PSCue version, error messages, output from diagnostic commands above

---

## Configuration Reference

See [README.md](../README.md#configuration) for complete list of environment variables.

**Quick Reference**:
```powershell
# Disable learning entirely
$env:PSCUE_DISABLE_LEARNING = "true"

# Performance tuning
$env:PSCUE_MAX_COMMANDS = "500"              # Max commands to track
$env:PSCUE_MAX_ARGS_PER_CMD = "100"          # Max arguments per command
$env:PSCUE_PCD_MAX_DEPTH = "3"               # Recursive search depth (tab)
$env:PSCUE_PCD_PREDICTOR_MAX_DEPTH = "1"     # Recursive search depth (predictor)

# Feature toggles
$env:PSCUE_ML_ENABLED = "true"               # ML sequence predictions
$env:PSCUE_WORKFLOW_LEARNING = "true"        # Workflow learning
$env:PSCUE_PARTIAL_COMMAND_PREDICTIONS = "true"  # Partial command predictions
$env:PSCUE_PCD_RECURSIVE_SEARCH = "true"     # PCD recursive search

# PCD filtering
$env:PSCUE_PCD_ENABLE_DOT_DIR_FILTER = "true"  # Filter cache/metadata dirs
$env:PSCUE_PCD_CUSTOM_BLOCKLIST = ".myapp,temp"  # Additional patterns
```
