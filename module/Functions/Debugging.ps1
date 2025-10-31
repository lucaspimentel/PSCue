# Debugging Functions for PSCue
# PowerShell functions for testing and debugging

function Test-PSCueCompletion {
    <#
    .SYNOPSIS
        Tests PSCue completion generation for a given input.

    .DESCRIPTION
        Generates and displays completions for the specified input string,
        showing what PSCue would suggest. Useful for testing and debugging
        completion logic without needing to trigger Tab completion.

    .PARAMETER Input
        The command line input to generate completions for.

    .PARAMETER IncludeTiming
        If specified, includes timing information showing how long completion took.

    .EXAMPLE
        Test-PSCueCompletion "git che"
        Shows completions for "git che".

    .EXAMPLE
        Test-PSCueCompletion "docker run -" -IncludeTiming
        Shows completions with timing information.

    .EXAMPLE
        Test-PSCueCompletion "scoop install"
        Tests completion for scoop install command.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$InputString,

        [Parameter()]
        [switch]$IncludeTiming
    )

    try {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

        # Parse input to extract command and cursor position
        $cursorPosition = $InputString.Length

        # Ensure PSCue.Shared is loaded
        $sharedDllPath = Join-Path (Split-Path (Get-Module PSCue).Path) "PSCue.Shared.dll"
        if (Test-Path $sharedDllPath) {
            Add-Type -Path $sharedDllPath -ErrorAction SilentlyContinue
        }

        # Call CommandCompleter directly (uses string overload for PowerShell compatibility)
        $completions = [PSCue.Shared.CommandCompleter]::GetCompletions(
            $InputString,
            $true   # includeDynamicArguments
        )

        $stopwatch.Stop()
        $elapsed = $stopwatch.ElapsedMilliseconds

        # Display results
        Write-Host "Input: $InputString" -ForegroundColor Cyan
        Write-Host "Completions: $($completions.Count)" -ForegroundColor Yellow

        if ($IncludeTiming) {
            Write-Host "Time: ${elapsed}ms" -ForegroundColor Gray
        }

        Write-Host ""

        if ($completions.Count -gt 0) {
            $completions | Select-Object -First 20 | ForEach-Object {
                Write-Host "  $($_.CompletionText)" -ForegroundColor Green -NoNewline
                if ($_.Description) {
                    Write-Host " - $($_.Description)" -ForegroundColor Gray
                } else {
                    Write-Host ""
                }
            }

            if ($completions.Count -gt 20) {
                Write-Host "  ... and $($completions.Count - 20) more" -ForegroundColor DarkGray
            }
        } else {
            Write-Host "  (no completions)" -ForegroundColor DarkGray
        }

        # Return objects for pipeline use
        $completions | ForEach-Object {
            [PSCustomObject]@{
                CompletionText = $_.CompletionText
                Description = $_.Description
                Type = $_.GetType().Name
            }
        }
    } catch {
        Write-Error "Failed to test completion: $_"
    }
}

function Get-PSCueModuleInfo {
    <#
    .SYNOPSIS
        Gets information about the PSCue module and its current state.

    .DESCRIPTION
        Returns detailed information about PSCue including version, configuration,
        component status, database info, and statistics.

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueModuleInfo
        Displays module information.

    .EXAMPLE
        Get-PSCueModuleInfo -AsJson
        Returns information as JSON.

    .EXAMPLE
        (Get-PSCueModuleInfo).DatabaseSize
        Gets just the database size.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$AsJson
    )

    try {
        # Get module version
        $module = Get-Module PSCue
        $version = if ($module) { $module.Version.ToString() } else { "Unknown" }

        # Get configuration from environment variables
        $config = [PSCustomObject]@{
            LearningEnabled = $env:PSCUE_DISABLE_LEARNING -ne "true"
            HistorySize = if ($env:PSCUE_HISTORY_SIZE) { [int]$env:PSCUE_HISTORY_SIZE } else { 100 }
            MaxCommands = if ($env:PSCUE_MAX_COMMANDS) { [int]$env:PSCUE_MAX_COMMANDS } else { 500 }
            MaxArgsPerCommand = if ($env:PSCUE_MAX_ARGS_PER_CMD) { [int]$env:PSCUE_MAX_ARGS_PER_CMD } else { 100 }
            DecayDays = if ($env:PSCUE_DECAY_DAYS) { [int]$env:PSCUE_DECAY_DAYS } else { 30 }
        }

        # Get component status
        $components = [PSCustomObject]@{
            Cache = [PSCue.Module.PSCueModule]::Cache -ne $null
            KnowledgeGraph = [PSCue.Module.PSCueModule]::KnowledgeGraph -ne $null
            CommandHistory = [PSCue.Module.PSCueModule]::CommandHistory -ne $null
            Persistence = [PSCue.Module.PSCueModule]::Persistence -ne $null
        }

        # Get database info
        $databaseInfo = if ([PSCue.Module.PSCueModule]::Persistence) {
            $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath
            $dbExists = Test-Path $dbPath
            $dbSize = if ($dbExists) { (Get-Item $dbPath).Length } else { 0 }

            [PSCustomObject]@{
                Path = $dbPath
                Exists = $dbExists
                SizeBytes = $dbSize
                SizeKB = [math]::Round($dbSize / 1KB, 2)
                SizeMB = [math]::Round($dbSize / 1MB, 2)
            }
        } else {
            $null
        }

        # Get cache statistics
        $cacheStats = if ([PSCue.Module.PSCueModule]::Cache) {
            $stats = [PSCue.Module.PSCueModule]::Cache.GetStatistics()
            [PSCustomObject]@{
                TotalEntries = $stats.EntryCount
                TotalHits = $stats.TotalHits
                AverageHits = if ($stats.EntryCount -gt 0) { [math]::Round($stats.TotalHits / $stats.EntryCount, 2) } else { 0 }
            }
        } else {
            $null
        }

        # Get learning statistics
        $learningStats = if ([PSCue.Module.PSCueModule]::KnowledgeGraph) {
            $commands = [PSCue.Module.PSCueModule]::KnowledgeGraph.GetAllCommands()
            $commandCount = ($commands | Measure-Object).Count
            $totalArgs = ($commands | ForEach-Object { $_.Value.Arguments.Count } | Measure-Object -Sum).Sum

            [PSCustomObject]@{
                LearnedCommands = $commandCount
                TotalArguments = $totalArgs
                AverageArgsPerCommand = if ($commandCount -gt 0) { [math]::Round($totalArgs / $commandCount, 2) } else { 0 }
            }
        } else {
            $null
        }

        # Get history statistics
        $historyStats = if ([PSCue.Module.PSCueModule]::CommandHistory) {
            $history = [PSCue.Module.PSCueModule]::CommandHistory.GetRecent()
            [PSCustomObject]@{
                EntryCount = $history.Count
                OldestEntry = if ($history.Count -gt 0) { ($history | Measure-Object -Property Timestamp -Minimum).Minimum } else { $null }
                NewestEntry = if ($history.Count -gt 0) { ($history | Measure-Object -Property Timestamp -Maximum).Maximum } else { $null }
            }
        } else {
            $null
        }

        # Build result object
        $result = [PSCustomObject]@{
            ModuleName = "PSCue"
            Version = $version
            ModulePath = if ($module) { $module.Path } else { $null }
            Configuration = $config
            Components = $components
            Database = $databaseInfo
            CacheStatistics = $cacheStats
            LearningStatistics = $learningStats
            HistoryStatistics = $historyStats
        }

        if ($AsJson) {
            $result | ConvertTo-Json -Depth 10
        } else {
            $result
        }
    } catch {
        Write-Error "Failed to retrieve module info: $_"
    }
}
