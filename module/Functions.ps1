# PSCue Module Functions
# Consolidated from Functions/*.ps1 to reduce per-file parse overhead on cold import.

#region Learning Management
# Learning Management Functions for PSCue
# PowerShell functions for learned data operations

function Get-PSCueLearning {
    <#
    .SYNOPSIS
        Gets learned command knowledge from PSCue.

    .DESCRIPTION
        Retrieves learned data from PSCue's knowledge graph, showing which commands
        have been learned, their arguments, usage frequencies, and recency.

    .PARAMETER Command
        Optional command name to filter results. If specified, shows only learned
        data for that specific command.

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueLearning
        Gets all learned commands and their knowledge.

    .EXAMPLE
        Get-PSCueLearning -Command "git"
        Gets learned arguments for git command.

    .EXAMPLE
        Get-PSCueLearning | Where-Object TotalUsage -gt 10
        Gets frequently used commands.

    .EXAMPLE
        Get-PSCueLearning -Command "docker" -AsJson
        Gets docker knowledge as JSON.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)]
        [string]$Command,

        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::KnowledgeGraph) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        if ($Command) {
            # Get knowledge for specific command
            $knowledge = [PSCue.Module.PSCueModule]::KnowledgeGraph.GetCommandKnowledge($Command)

            if ($null -eq $knowledge) {
                Write-Warning "No learned data found for command: $Command"
                return
            }

            $result = [PSCustomObject]@{
                Command = $knowledge.Command
                TotalUsage = $knowledge.TotalUsageCount
                FirstSeen = $knowledge.FirstSeen
                LastUsed = $knowledge.LastUsed
                Arguments = $knowledge.Arguments.Values | ForEach-Object {
                    [PSCustomObject]@{
                        Argument = $_.Argument
                        UsageCount = $_.UsageCount
                        FirstSeen = $_.FirstSeen
                        LastUsed = $_.LastUsed
                        IsFlag = $_.IsFlag
                        Score = $_.GetScore($knowledge.TotalUsageCount, 30)
                    }
                } | Sort-Object -Property Score -Descending
            }

            if ($AsJson) {
                $result | ConvertTo-Json -Depth 10
            } else {
                $result
            }
        } else {
            # Get all learned commands - GetAllCommands() returns the ConcurrentDictionary
            # which implements IEnumerable<KeyValuePair<string, CommandKnowledge>>
            $commandsDict = [PSCue.Module.PSCueModule]::KnowledgeGraph.GetAllCommands()

            # Check if empty
            if ($commandsDict.Count -eq 0) {
                if ($AsJson) {
                    return "[]"
                } else {
                    return @()
                }
            }

            # Enumerate the dictionary to get KeyValuePairs
            $results = @($commandsDict.GetEnumerator() | ForEach-Object {
                $knowledge = $_.Value
                [PSCustomObject]@{
                    Command = $knowledge.Command
                    TotalUsage = $knowledge.TotalUsageCount
                    ArgumentCount = $knowledge.Arguments.Count
                    FirstSeen = $knowledge.FirstSeen
                    LastUsed = $knowledge.LastUsed
                    TopArguments = ($knowledge.Arguments.Values |
                        Sort-Object -Property UsageCount -Descending |
                        Select-Object -First 5 |
                        ForEach-Object { $_.Argument }) -join ', '
                }
            } | Sort-Object -Property TotalUsage -Descending)

            if ($AsJson) {
                $results | ConvertTo-Json -Depth 10
            } else {
                $results
            }
        }
    } catch {
        Write-Error "Failed to retrieve learned data: $_"
    }
}

function Clear-PSCueLearning {
    <#
    .SYNOPSIS
        Clears PSCue's learned command knowledge.

    .DESCRIPTION
        Removes all learned data including command history and argument knowledge graph.
        This also deletes the persisted database file.

    .PARAMETER Force
        Forces deletion of the database file even if PSCue is not initialized.
        Useful for recovery scenarios when the database is corrupted.

    .PARAMETER WhatIf
        Shows what would happen if the command runs without actually clearing data.

    .PARAMETER Confirm
        Prompts for confirmation before clearing learned data.

    .EXAMPLE
        Clear-PSCueLearning
        Clears all learned data after confirming.

    .EXAMPLE
        Clear-PSCueLearning -Confirm:$false
        Clears learned data without confirmation.

    .EXAMPLE
        Clear-PSCueLearning -WhatIf
        Shows what would be cleared without actually clearing.

    .EXAMPLE
        Clear-PSCueLearning -Force
        Forces deletion of database file even if PSCue isn't initialized.
        Use this when the database is corrupted and preventing initialization.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter()]
        [switch]$Force
    )

    # Helper function to get database path (mirrors PersistenceManager.GetDataDirectory logic)
    function Get-PSCueDatabasePath {
        if ($IsWindows -or $PSVersionTable.PSVersion.Major -lt 6) {
            # Windows: Use LocalApplicationData
            $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
            $dataDir = Join-Path $localAppData "PSCue"
        } else {
            # Linux/macOS: Use XDG_DATA_HOME or ~/.local/share
            $homeDir = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
            $xdgDataHome = $env:XDG_DATA_HOME
            if ($xdgDataHome) {
                $dataDir = Join-Path $xdgDataHome "PSCue"
            } else {
                $dataDir = Join-Path $homeDir ".local" "share" "PSCue"
            }
        }
        return Join-Path $dataDir "learned-data.db"
    }

    # Force mode: Delete database file directly without requiring initialization
    if ($Force) {
        try {
            $dbPath = Get-PSCueDatabasePath
            $dbExists = Test-Path $dbPath
            $walPath = "$dbPath-wal"
            $shmPath = "$dbPath-shm"
            $walExists = Test-Path $walPath
            $shmExists = Test-Path $shmPath

            if (-not $dbExists -and -not $walExists -and -not $shmExists) {
                Write-Warning "Database file not found at: $dbPath"
                return
            }

            $filesToDelete = @()
            if ($dbExists) { $filesToDelete += "Database file" }
            if ($walExists) { $filesToDelete += "WAL journal" }
            if ($shmExists) { $filesToDelete += "Shared memory file" }

            if ($PSCmdlet.ShouldProcess("$($filesToDelete -join ', ') at $dbPath", "Delete database files")) {
                $deletedFiles = @()

                if ($dbExists) {
                    Remove-Item -Path $dbPath -Force
                    $deletedFiles += "Database"
                }
                if ($walExists) {
                    Remove-Item -Path $walPath -Force
                    $deletedFiles += "WAL journal"
                }
                if ($shmExists) {
                    Remove-Item -Path $shmPath -Force
                    $deletedFiles += "Shared memory"
                }

                Write-Host "✓ Force deleted database files:" -ForegroundColor Green
                Write-Host "  - Path: $dbPath" -ForegroundColor Gray
                Write-Host "  - Deleted: $($deletedFiles -join ', ')" -ForegroundColor Gray
                Write-Warning "PSCue module may need to be reloaded for changes to take effect."
            }
        } catch {
            Write-Error "Failed to force delete database: $_"
        }
        return
    }

    # Normal mode: Use PersistenceManager if available
    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue learning system is not initialized. Use -Force to delete the database file directly, or make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        # Get counts before clearing
        $commandCount = 0
        if ([PSCue.Module.PSCueModule]::KnowledgeGraph) {
            $commandCount = ([PSCue.Module.PSCueModule]::KnowledgeGraph.GetAllCommands() | Measure-Object).Count
        }

        $historyCount = 0
        if ([PSCue.Module.PSCueModule]::CommandHistory) {
            $historyCount = ([PSCue.Module.PSCueModule]::CommandHistory.GetRecent() | Measure-Object).Count
        }

        $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath

        if ($PSCmdlet.ShouldProcess("$commandCount learned commands, $historyCount history entries, database at $dbPath", "Clear all learned data")) {
            [PSCue.Module.PSCueModule]::Persistence.Clear()

            Write-Host "✓ Cleared learned data:" -ForegroundColor Green
            Write-Host "  - $commandCount learned commands" -ForegroundColor Gray
            Write-Host "  - $historyCount history entries" -ForegroundColor Gray
            Write-Host "  - Database: $dbPath" -ForegroundColor Gray
        }
    } catch {
        Write-Error "Failed to clear learned data: $_"
    }
}

function Export-PSCueLearning {
    <#
    .SYNOPSIS
        Exports PSCue's learned data to a JSON file.

    .DESCRIPTION
        Exports all learned command knowledge and history to a JSON file for backup,
        migration, or inspection purposes.

    .PARAMETER Path
        Path to the JSON file to create. Directory will be created if it doesn't exist.

    .EXAMPLE
        Export-PSCueLearning -Path ~/pscue-backup.json
        Exports learned data to a JSON file in the home directory.

    .EXAMPLE
        Export-PSCueLearning -Path ./learned-data-$(Get-Date -Format 'yyyy-MM-dd').json
        Exports with a dated filename.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Path
    )

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        # Resolve path
        $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)

        [PSCue.Module.PSCueModule]::Persistence.Export(
            $resolvedPath,
            [PSCue.Module.PSCueModule]::KnowledgeGraph,
            [PSCue.Module.PSCueModule]::CommandHistory
        )

        $fileSize = (Get-Item $resolvedPath).Length
        Write-Host "✓ Exported learned data to: $resolvedPath" -ForegroundColor Green
        Write-Host "  File size: $([math]::Round($fileSize / 1KB, 2)) KB" -ForegroundColor Gray
    } catch {
        Write-Error "Failed to export learned data: $_"
    }
}

function Import-PSCueLearning {
    <#
    .SYNOPSIS
        Imports PSCue's learned data from a JSON file.

    .DESCRIPTION
        Imports learned command knowledge and history from a JSON file created by
        Export-PSCueLearning. Can either merge with existing data or replace it.

    .PARAMETER Path
        Path to the JSON file to import.

    .PARAMETER Merge
        If specified, merges imported data with existing learned data (additive).
        If not specified, replaces existing data entirely.

    .EXAMPLE
        Import-PSCueLearning -Path ~/pscue-backup.json
        Imports learned data, replacing existing data.

    .EXAMPLE
        Import-PSCueLearning -Path ~/shared-learning.json -Merge
        Imports and merges with existing learned data.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Path,

        [Parameter()]
        [switch]$Merge
    )

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        # Resolve and validate path
        $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)

        if (-not (Test-Path $resolvedPath)) {
            Write-Error "File not found: $resolvedPath"
            return
        }

        $action = if ($Merge) { "Merge with" } else { "Replace" }
        $fileSize = (Get-Item $resolvedPath).Length

        if ($PSCmdlet.ShouldProcess("learned data from $resolvedPath ($([math]::Round($fileSize / 1KB, 2)) KB)", $action)) {
            [PSCue.Module.PSCueModule]::Persistence.Import(
                $resolvedPath,
                [PSCue.Module.PSCueModule]::KnowledgeGraph,
                [PSCue.Module.PSCueModule]::CommandHistory,
                $Merge
            )

            $commandCount = ([PSCue.Module.PSCueModule]::KnowledgeGraph.GetAllCommands() | Measure-Object).Count
            Write-Host "✓ Imported learned data from: $resolvedPath" -ForegroundColor Green
            Write-Host "  Total learned commands: $commandCount" -ForegroundColor Gray
            Write-Host "  Mode: $(if ($Merge) { 'Merged' } else { 'Replaced' })" -ForegroundColor Gray
        }
    } catch {
        Write-Error "Failed to import learned data: $_"
    }
}

function Save-PSCueLearning {
    <#
    .SYNOPSIS
        Forces an immediate save of learned data to disk.

    .DESCRIPTION
        Triggers an immediate save of learned data to the SQLite database, bypassing
        the automatic 5-minute save timer. Useful before system shutdown or when you
        want to ensure data is persisted immediately.

    .EXAMPLE
        Save-PSCueLearning
        Saves learned data immediately.
    #>
    [CmdletBinding()]
    param()

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        if ([PSCue.Module.PSCueModule]::KnowledgeGraph -and [PSCue.Module.PSCueModule]::CommandHistory) {
            [PSCue.Module.PSCueModule]::Persistence.SaveArgumentGraph([PSCue.Module.PSCueModule]::KnowledgeGraph)
            [PSCue.Module.PSCueModule]::Persistence.SaveCommandHistory([PSCue.Module.PSCueModule]::CommandHistory)

            $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath
            $dbSize = (Get-Item $dbPath).Length

            Write-Host "✓ Saved learned data to: $dbPath" -ForegroundColor Green
            Write-Host "  Database size: $([math]::Round($dbSize / 1KB, 2)) KB" -ForegroundColor Gray
        } else {
            Write-Warning "No learned data to save (learning may be disabled)"
        }
    } catch {
        Write-Error "Failed to save learned data: $_"
    }
}
#endregion

#region Database Management
# Database Management Functions for PSCue
# Direct SQLite database query functions

function Get-PSCueDatabaseStats {
    <#
    .SYNOPSIS
        Gets statistics directly from the PSCue SQLite database.

    .DESCRIPTION
        Reads directly from the SQLite database file to show what's actually persisted,
        bypassing the in-memory cache. This is useful for debugging and understanding
        what data has been saved across sessions.

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .PARAMETER Detailed
        Shows detailed per-command statistics including top arguments.

    .EXAMPLE
        Get-PSCueDatabaseStats
        Shows summary statistics from the database.

    .EXAMPLE
        Get-PSCueDatabaseStats -Detailed
        Shows detailed per-command statistics with top arguments.

    .EXAMPLE
        Get-PSCueDatabaseStats -AsJson
        Returns statistics as JSON.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$AsJson,

        [Parameter()]
        [switch]$Detailed
    )

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue persistence manager is not initialized. Make sure the PSCue module is loaded."
        return
    }

    try {
        $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath

        if (-not (Test-Path $dbPath)) {
            Write-Warning "Database file does not exist yet: $dbPath"
            Write-Host "Run some commands to generate learning data." -ForegroundColor Gray
            return
        }

        # Load SQLite assembly
        Add-Type -Path ([PSCue.Module.PSCueModule]::Persistence.GetType().Assembly.Location)

        # Open database connection
        $connectionString = "Data Source=$dbPath;Mode=ReadOnly"
        $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
        $connection.Open()

        try {
            # Get summary statistics
            $summary = @{}

            # Count commands
            $cmdCount = New-Object Microsoft.Data.Sqlite.SqliteCommand("SELECT COUNT(*) FROM commands", $connection)
            $summary.TotalCommands = [int]$cmdCount.ExecuteScalar()
            $cmdCount.Dispose()

            # Count arguments
            $argCount = New-Object Microsoft.Data.Sqlite.SqliteCommand("SELECT COUNT(*) FROM arguments", $connection)
            $summary.TotalArguments = [int]$argCount.ExecuteScalar()
            $argCount.Dispose()

            # Count history entries
            $histCount = New-Object Microsoft.Data.Sqlite.SqliteCommand("SELECT COUNT(*) FROM command_history", $connection)
            $summary.TotalHistoryEntries = [int]$histCount.ExecuteScalar()
            $histCount.Dispose()

            # Count co-occurrences
            $coCount = New-Object Microsoft.Data.Sqlite.SqliteCommand("SELECT COUNT(*) FROM co_occurrences", $connection)
            $summary.TotalCoOccurrences = [int]$coCount.ExecuteScalar()
            $coCount.Dispose()

            # Count flag combinations
            $flagCount = New-Object Microsoft.Data.Sqlite.SqliteCommand("SELECT COUNT(*) FROM flag_combinations", $connection)
            $summary.TotalFlagCombinations = [int]$flagCount.ExecuteScalar()
            $flagCount.Dispose()

            # Get database size
            $dbSize = (Get-Item $dbPath).Length
            $summary.DatabaseSizeKB = [math]::Round($dbSize / 1KB, 2)
            $summary.DatabasePath = $dbPath

            # Get most used commands
            $topCommandsCmd = New-Object Microsoft.Data.Sqlite.SqliteCommand(@"
                SELECT command, total_usage_count, first_seen, last_used
                FROM commands
                ORDER BY total_usage_count DESC
                LIMIT 10
"@, $connection)

            $reader = $topCommandsCmd.ExecuteReader()
            $topCommands = @()
            while ($reader.Read()) {
                $topCommands += [PSCustomObject]@{
                    Command = $reader.GetString(0)
                    TotalUsage = $reader.GetInt32(1)
                    FirstSeen = [DateTime]::Parse($reader.GetString(2))
                    LastUsed = [DateTime]::Parse($reader.GetString(3))
                }
            }
            $reader.Close()
            $topCommandsCmd.Dispose()

            # Build result
            if ($Detailed) {
                # Get detailed per-command statistics
                $detailedStats = @()

                foreach ($cmd in $topCommands) {
                    # Get top arguments for this command
                    $topArgsCmd = New-Object Microsoft.Data.Sqlite.SqliteCommand(@"
                        SELECT argument, usage_count, first_seen, last_used, is_flag
                        FROM arguments
                        WHERE command = @command
                        ORDER BY usage_count DESC
                        LIMIT 10
"@, $connection)
                    $topArgsCmd.Parameters.AddWithValue("@command", $cmd.Command) | Out-Null

                    $argsReader = $topArgsCmd.ExecuteReader()
                    $arguments = @()
                    while ($argsReader.Read()) {
                        $arguments += [PSCustomObject]@{
                            Argument = $argsReader.GetString(0)
                            UsageCount = $argsReader.GetInt32(1)
                            FirstSeen = [DateTime]::Parse($argsReader.GetString(2))
                            LastUsed = [DateTime]::Parse($argsReader.GetString(3))
                            IsFlag = $argsReader.GetBoolean(4)
                        }
                    }
                    $argsReader.Close()
                    $topArgsCmd.Dispose()

                    $detailedStats += [PSCustomObject]@{
                        Command = $cmd.Command
                        TotalUsage = $cmd.TotalUsage
                        FirstSeen = $cmd.FirstSeen
                        LastUsed = $cmd.LastUsed
                        ArgumentCount = $arguments.Count
                        TopArguments = $arguments
                    }
                }

                $result = [PSCustomObject]@{
                    Summary = $summary
                    DetailedCommands = $detailedStats
                }
            } else {
                # Simple summary with top commands
                $result = [PSCustomObject]@{
                    TotalCommands = $summary.TotalCommands
                    TotalArguments = $summary.TotalArguments
                    TotalHistoryEntries = $summary.TotalHistoryEntries
                    TotalCoOccurrences = $summary.TotalCoOccurrences
                    TotalFlagCombinations = $summary.TotalFlagCombinations
                    DatabaseSizeKB = $summary.DatabaseSizeKB
                    DatabasePath = $summary.DatabasePath
                    TopCommands = $topCommands
                }
            }

            if ($AsJson) {
                $result | ConvertTo-Json -Depth 10
            } else {
                $result
            }
        } finally {
            $connection.Close()
            $connection.Dispose()
        }
    } catch {
        Write-Error "Failed to query database: $_"
    }
}

function Get-PSCueDatabaseHistory {
    <#
    .SYNOPSIS
        Gets command history directly from the PSCue SQLite database.

    .DESCRIPTION
        Reads command history entries from the database, showing the last N commands
        that have been executed and learned.

    .PARAMETER Last
        Number of most recent history entries to retrieve (default: 20).

    .PARAMETER Command
        Filter history to only show entries for a specific command.

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueDatabaseHistory
        Shows the last 20 command history entries.

    .EXAMPLE
        Get-PSCueDatabaseHistory -Last 50
        Shows the last 50 history entries.

    .EXAMPLE
        Get-PSCueDatabaseHistory -Command "git"
        Shows all git command history entries.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [int]$Last = 20,

        [Parameter()]
        [string]$Command,

        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue persistence manager is not initialized. Make sure the PSCue module is loaded."
        return
    }

    try {
        $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath

        if (-not (Test-Path $dbPath)) {
            Write-Warning "Database file does not exist yet: $dbPath"
            return
        }

        # Open database connection
        $connectionString = "Data Source=$dbPath;Mode=ReadOnly"
        $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
        $connection.Open()

        try {
            $query = if ($Command) {
                @"
                SELECT id, command, command_line, arguments, timestamp, success, working_directory
                FROM command_history
                WHERE command = @command COLLATE NOCASE
                ORDER BY id DESC
                LIMIT @last
"@
            } else {
                @"
                SELECT id, command, command_line, arguments, timestamp, success, working_directory
                FROM command_history
                ORDER BY id DESC
                LIMIT @last
"@
            }

            $cmd = New-Object Microsoft.Data.Sqlite.SqliteCommand($query, $connection)
            $cmd.Parameters.AddWithValue("@last", $Last) | Out-Null
            if ($Command) {
                $cmd.Parameters.AddWithValue("@command", $Command) | Out-Null
            }

            $reader = $cmd.ExecuteReader()
            $history = @()

            while ($reader.Read()) {
                $history += [PSCustomObject]@{
                    Id = $reader.GetInt32(0)
                    Command = $reader.GetString(1)
                    CommandLine = $reader.GetString(2)
                    Arguments = $reader.GetString(3)
                    Timestamp = [DateTime]::Parse($reader.GetString(4))
                    Success = $reader.GetBoolean(5)
                    WorkingDirectory = if ($reader.IsDBNull(6)) { $null } else { $reader.GetString(6) }
                }
            }

            $reader.Close()
            $cmd.Dispose()

            if ($AsJson) {
                # Handle empty array
                if ($history.Count -eq 0) {
                    return "[]"
                }
                $history | ConvertTo-Json -Depth 10
            } else {
                $history
            }
        } finally {
            $connection.Close()
            $connection.Dispose()
        }
    } catch {
        Write-Error "Failed to query command history: $_"
    }
}
#endregion

#region Workflow Management
# Workflow Management Functions for PSCue
# Functions for managing and querying learned workflow patterns

function Get-PSCueWorkflows {
    <#
    .SYNOPSIS
        Gets learned workflow patterns from PSCue.

    .DESCRIPTION
        Shows command → next command transitions that PSCue has learned from your usage patterns.
        This helps you understand what workflows PSCue has identified and will use for predictions.

    .PARAMETER Command
        Filter workflows starting from a specific command (e.g., "git add").

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueWorkflows
        Shows all learned workflows.

    .EXAMPLE
        Get-PSCueWorkflows -Command "git add"
        Shows workflows that start with "git add".

    .EXAMPLE
        Get-PSCueWorkflows -AsJson
        Returns workflows as JSON.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$Command,

        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    try {
        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner
        $allWorkflows = $learner.GetAllWorkflows()

        if ($allWorkflows.Count -eq 0) {
            Write-Host "No workflow patterns learned yet." -ForegroundColor Yellow
            Write-Host "Use commands normally and PSCue will learn your workflows automatically." -ForegroundColor Gray
            return
        }

        # Filter by command if specified
        if ($Command) {
            $filteredWorkflows = @{}
            foreach ($key in $allWorkflows.Keys) {
                if ($key -like "*$Command*") {
                    $filteredWorkflows[$key] = $allWorkflows[$key]
                }
            }
            $allWorkflows = $filteredWorkflows

            if ($allWorkflows.Count -eq 0) {
                Write-Host "No workflows found for command: $Command" -ForegroundColor Yellow
                return
            }
        }

        # Build output
        $results = @()
        foreach ($fromCmd in $allWorkflows.Keys | Sort-Object) {
            $transitions = $allWorkflows[$fromCmd] | Sort-Object -Property Frequency -Descending

            foreach ($transition in $transitions) {
                $avgTimeSec = $transition.AverageTimeDelta.TotalSeconds

                $results += [PSCustomObject]@{
                    FromCommand      = $fromCmd
                    ToCommand        = $transition.NextCommand
                    Frequency        = $transition.Frequency
                    AvgTimeDelta     = "{0:F1}s" -f $avgTimeSec
                    Confidence       = "{0:P0}" -f $transition.GetConfidence(5, 30)
                    FirstSeen        = $transition.FirstSeen.ToString("yyyy-MM-dd")
                    LastSeen         = $transition.LastSeen.ToString("yyyy-MM-dd")
                }
            }
        }

        if ($AsJson) {
            $results | ConvertTo-Json -Depth 10
        }
        else {
            $results | Format-Table -AutoSize
        }
    }
    catch {
        Write-Error "Error retrieving workflows: $_"
    }
}

function Get-PSCueWorkflowStats {
    <#
    .SYNOPSIS
        Gets statistics about learned workflow patterns.

    .DESCRIPTION
        Shows summary statistics about the workflow learning system, including
        total transitions, unique commands, and most common workflows.

    .PARAMETER Detailed
        Shows detailed statistics including top workflows and database size.

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueWorkflowStats
        Shows summary statistics.

    .EXAMPLE
        Get-PSCueWorkflowStats -Detailed
        Shows detailed statistics with top workflows.

    .EXAMPLE
        Get-PSCueWorkflowStats -AsJson
        Returns statistics as JSON.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$Detailed,

        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    try {
        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner
        $diagnostics = $learner.GetDiagnostics()

        $stats = [PSCustomObject]@{
            TotalTransitions     = $diagnostics.Item1
            UniqueCommands       = $diagnostics.Item2
            PendingTransitions   = $diagnostics.Item3
        }

        if ($Detailed) {
            # Get all workflows to find most common
            $allWorkflows = $learner.GetAllWorkflows()

            $topWorkflows = @()
            foreach ($fromCmd in $allWorkflows.Keys) {
                foreach ($transition in $allWorkflows[$fromCmd]) {
                    $topWorkflows += [PSCustomObject]@{
                        Workflow    = "$fromCmd → $($transition.NextCommand)"
                        Frequency   = $transition.Frequency
                        AvgTime     = "{0:F1}s" -f $transition.AverageTimeDelta.TotalSeconds
                    }
                }
            }

            $topWorkflows = $topWorkflows | Sort-Object -Property Frequency -Descending | Select-Object -First 10

            # Get database size if persistence is available
            $dbSize = "N/A"
            if ($null -ne [PSCue.Module.PSCueModule]::Persistence) {
                $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath
                if (Test-Path $dbPath) {
                    $dbSizeBytes = (Get-Item $dbPath).Length
                    $dbSize = "{0:N2} KB" -f ($dbSizeBytes / 1KB)
                }
            }

            $stats | Add-Member -NotePropertyName TopWorkflows -NotePropertyValue $topWorkflows
            $stats | Add-Member -NotePropertyName DatabaseSize -NotePropertyValue $dbSize
        }

        if ($AsJson) {
            $stats | ConvertTo-Json -Depth 10
        }
        else {
            if ($Detailed) {
                Write-Host "`nWorkflow Learning Statistics:" -ForegroundColor Cyan
                Write-Host "  Total Transitions: $($stats.TotalTransitions)"
                Write-Host "  Unique Commands: $($stats.UniqueCommands)"
                Write-Host "  Pending (unsaved): $($stats.PendingTransitions)"
                Write-Host "  Database Size: $($stats.DatabaseSize)"

                if ($stats.TopWorkflows.Count -gt 0) {
                    Write-Host "`nTop Workflows:" -ForegroundColor Cyan
                    $stats.TopWorkflows | Format-Table -AutoSize
                }
            }
            else {
                $stats | Format-List
            }
        }
    }
    catch {
        Write-Error "Error retrieving workflow statistics: $_"
    }
}

function Clear-PSCueWorkflows {
    <#
    .SYNOPSIS
        Clears learned workflow patterns.

    .DESCRIPTION
        Clears all workflow patterns from memory and optionally from the database.
        Use with caution - this will delete all learned workflow data.

    .PARAMETER WhatIf
        Shows what would happen if the command runs without actually clearing data.

    .PARAMETER Confirm
        Prompts for confirmation before clearing workflow data.

    .EXAMPLE
        Clear-PSCueWorkflows -WhatIf
        Shows what would be cleared without actually clearing.

    .EXAMPLE
        Clear-PSCueWorkflows -Confirm
        Prompts for confirmation before clearing workflows.

    .EXAMPLE
        Clear-PSCueWorkflows
        Clears workflow data (prompts for confirmation by default).
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param()

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    try {
        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner
        $diagnostics = $learner.GetDiagnostics()
        $totalTransitions = $diagnostics.Item1

        if ($PSCmdlet.ShouldProcess(
            "$totalTransitions workflow transitions",
            "Clear workflow data from memory and database")) {

            # Clear in-memory workflow data
            $learner.Clear()

            # Also clear delta to prevent re-saving
            $learner.ClearDelta()

            # Clear from database
            if ($null -ne [PSCue.Module.PSCueModule]::Persistence) {
                $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath

                if (Test-Path $dbPath) {
                    # Load SQLite assembly
                    Add-Type -Path ([PSCue.Module.PSCueModule]::Persistence.GetType().Assembly.Location)

                    # Open database connection
                    $connectionString = "Data Source=$dbPath;Mode=ReadWrite"
                    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
                    $connection.Open()

                    try {
                        # Delete all workflow transitions
                        $cmd = New-Object Microsoft.Data.Sqlite.SqliteCommand("DELETE FROM workflow_transitions", $connection)
                        $rowsDeleted = $cmd.ExecuteNonQuery()
                        $cmd.Dispose()

                        Write-Host "Cleared $rowsDeleted workflow transitions from database." -ForegroundColor Green
                    }
                    finally {
                        $connection.Close()
                        $connection.Dispose()
                    }
                }
            }

            Write-Host "Workflow data cleared successfully." -ForegroundColor Green
            Write-Host "PSCue will start learning new workflows from scratch." -ForegroundColor Gray
        }
    }
    catch {
        Write-Error "Error clearing workflows: $_"
    }
}

function Export-PSCueWorkflows {
    <#
    .SYNOPSIS
        Exports learned workflows to a JSON file.

    .DESCRIPTION
        Exports all learned workflow patterns to a JSON file for backup or sharing.
        The exported file can be imported on another machine or after clearing workflows.

    .PARAMETER Path
        Path to the JSON file to create.

    .EXAMPLE
        Export-PSCueWorkflows -Path ~/workflows.json
        Exports all workflows to workflows.json.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    try {
        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner
        $allWorkflows = $learner.GetAllWorkflows()

        if ($allWorkflows.Count -eq 0) {
            Write-Warning "No workflows to export."
            return
        }

        # Convert to serializable format
        $exportData = @{
            ExportedAt = (Get-Date).ToString("o")
            Version    = "1.0"
            Workflows  = @{}
        }

        foreach ($fromCmd in $allWorkflows.Keys) {
            $exportData.Workflows[$fromCmd] = @{}

            foreach ($transition in $allWorkflows[$fromCmd]) {
                $exportData.Workflows[$fromCmd][$transition.NextCommand] = @{
                    Frequency        = $transition.Frequency
                    TotalTimeDeltaMs = $transition.TotalTimeDeltaMs
                    FirstSeen        = $transition.FirstSeen.ToString("o")
                    LastSeen         = $transition.LastSeen.ToString("o")
                }
            }
        }

        $exportData | ConvertTo-Json -Depth 10 | Set-Content -Path $Path -Encoding UTF8

        Write-Host "Exported $($allWorkflows.Count) workflow patterns to: $Path" -ForegroundColor Green
    }
    catch {
        Write-Error "Error exporting workflows: $_"
    }
}

function Import-PSCueWorkflows {
    <#
    .SYNOPSIS
        Imports workflows from a JSON file.

    .DESCRIPTION
        Imports workflow patterns from a previously exported JSON file.
        Can merge with existing workflows or replace them.

    .PARAMETER Path
        Path to the JSON file to import.

    .PARAMETER Merge
        Merge imported workflows with existing data instead of replacing.
        Frequencies will be summed for matching transitions.

    .EXAMPLE
        Import-PSCueWorkflows -Path ~/workflows.json
        Replaces current workflows with imported data.

    .EXAMPLE
        Import-PSCueWorkflows -Path ~/workflows.json -Merge
        Merges imported workflows with existing data.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [switch]$Merge
    )

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    if (-not (Test-Path $Path)) {
        Write-Error "File not found: $Path"
        return
    }

    try {
        $importData = Get-Content -Path $Path -Raw | ConvertFrom-Json

        if (-not $importData.Workflows) {
            Write-Error "Invalid workflow export file format."
            return
        }

        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner

        # Clear existing data if not merging
        if (-not $Merge) {
            $learner.Clear()
        }

        # Convert imported data to WorkflowTransition objects
        $workflowDict = @{}

        foreach ($fromCmd in $importData.Workflows.PSObject.Properties.Name) {
            $workflowDict[$fromCmd] = @{}

            foreach ($toCmd in $importData.Workflows.$fromCmd.PSObject.Properties.Name) {
                $data = $importData.Workflows.$fromCmd.$toCmd

                $transition = New-Object PSCue.Module.WorkflowTransition
                $transition.NextCommand = $toCmd
                $transition.Frequency = $data.Frequency
                $transition.TotalTimeDeltaMs = $data.TotalTimeDeltaMs
                $transition.FirstSeen = [DateTime]::Parse($data.FirstSeen)
                $transition.LastSeen = [DateTime]::Parse($data.LastSeen)

                $workflowDict[$fromCmd][$toCmd] = $transition
            }
        }

        # Initialize with imported data (merges automatically if learner already has data)
        $learner.Initialize($workflowDict)

        $totalImported = ($workflowDict.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum

        if ($Merge) {
            Write-Host "Merged $totalImported workflow transitions from: $Path" -ForegroundColor Green
        }
        else {
            Write-Host "Imported $totalImported workflow transitions from: $Path" -ForegroundColor Green
        }

        Write-Host "Use Save-PSCueLearning to persist the imported workflows to the database." -ForegroundColor Gray
    }
    catch {
        Write-Error "Error importing workflows: $_"
    }
}

# Note: Functions are exported via FunctionsToExport in PSCue.psd1 manifest
# Export-ModuleMember is not needed when using dot-sourcing in PSCue.psm1
#endregion

#region Smart Navigation (pcd)
# PCD - PowerShell Change Directory with PSCue smart suggestions
# Leverages PSCue's learned directory data for intelligent navigation

# Tracks the previous directory for 'pcd -' navigation (like cd -)
$script:PreviousLocation = $null

function Invoke-PCD {
    <#
    .SYNOPSIS
    Change directory with enhanced smart suggestions from PSCue's learned data.

    .DESCRIPTION
    PowerShell Change Directory (pcd) provides intelligent directory navigation
    with fuzzy matching, frecency scoring (frequency + recency), optional
    recursive filesystem search, and interactive selection mode.

    Features:
    - Well-known shortcuts: ~, .., .
    - Fuzzy matching: Finds directories even with typos
    - Frecency scoring: Balances frequency and recency
    - Distance scoring: Prefers nearby directories
    - Best-match navigation: Automatically finds closest match if exact path doesn't exist
    - Optional recursive search: Finds directories by name in subdirectories
    - Interactive selection: Browse and select from frequently visited directories

    .PARAMETER Path
    The directory path to change to. Supports tab completion with learned suggestions.
    If the path doesn't exist exactly, pcd will find the best fuzzy match.
    If not specified and -Interactive is not used, changes to the user's home directory.

    .PARAMETER Interactive
    Show an interactive selection menu to browse and select from learned directories.
    Alias: -i

    .PARAMETER Top
    Number of directories to show in interactive mode. Default is 20.
    Valid range: 5-100.

    .PARAMETER Root
    Navigate to the root of the current git repository by walking up from $PWD looking for a .git
    directory or file (supports worktrees). If not inside a git repo, navigates to the filesystem root.
    Alias: -r

    .PARAMETER Bookmark
    Toggle a bookmark on the current directory (or explicit path). If the directory is already
    bookmarked, removes it. Bookmarked directories appear at the top of tab completions and
    interactive mode with a star indicator.
    Alias: -b

    .PARAMETER ListBookmarks
    List all bookmarked directories.
    Alias: -lb

    .EXAMPLE
    pcd D:\source\datadog
    Changes to the specified directory.

    .EXAMPLE
    pcd datadog
    If "datadog" doesn't exist in current directory, finds best match like "D:\source\datadog".

    .EXAMPLE
    pcd
    Changes to the user's home directory.

    .EXAMPLE
    pcd -i
    Shows an interactive menu to browse and select from your 20 most frequently visited directories.

    .EXAMPLE
    pcd -i dotnet
    Shows an interactive menu filtered to directories matching "dotnet".

    .EXAMPLE
    pcdi dotnet
    Shorthand for pcd -i dotnet.

    .EXAMPLE
    pcd -Root
    Navigates to the root of the current git repository, or filesystem root if not in a repo.

    .EXAMPLE
    pcd -r
    Same as pcd -Root (alias).

    .EXAMPLE
    pcd -Interactive -Top 50
    Shows an interactive menu with up to 50 learned directories.

    .EXAMPLE
    pcd -b
    Bookmarks the current directory. Run again to remove the bookmark.

    .EXAMPLE
    pcd -b D:\source\myproject
    Bookmarks an explicit path.

    .EXAMPLE
    pcd -lb
    Lists all bookmarked directories.

    .EXAMPLE
    pcd dat<TAB>
    Tab completion shows learned directories matching "dat" with fuzzy matching.

    .NOTES
    This function uses PSCue's PcdCompletionEngine with enhanced scoring.
    If the module is not fully initialized, falls back to native Set-Location behavior.

    Configuration via environment variables:
    - $env:PSCUE_PCD_FREQUENCY_WEIGHT (default: 0.5)
    - $env:PSCUE_PCD_RECENCY_WEIGHT (default: 0.3)
    - $env:PSCUE_PCD_DISTANCE_WEIGHT (default: 0.2)
    - $env:PSCUE_PCD_RECURSIVE_SEARCH (default: true)
    - $env:PSCUE_PCD_MAX_DEPTH (default: 3)
    #>
    [CmdletBinding()]
    [Alias('pcd', 'pcdi')]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Path,

        [Parameter(Mandatory = $false)]
        [Alias('i')]
        [switch]$Interactive,

        [Parameter(Mandatory = $false)]
        [ValidateRange(5, 100)]
        [int]$Top = 20,

        [Parameter(Mandatory = $false)]
        [Alias('r')]
        [switch]$Root,

        [Parameter(Mandatory = $false)]
        [Alias('v')]
        [switch]$Version,

        [Parameter(Mandatory = $false)]
        [Alias('b')]
        [switch]$Bookmark,

        [Parameter(Mandatory = $false)]
        [Alias('lb')]
        [switch]$ListBookmarks
    )

    if ($Version) {
        $moduleInfo = Get-Module PSCue
        if ($moduleInfo) {
            Write-Output "pcd (PSCue) $($moduleInfo.Version)"
        } else {
            Write-Output "pcd (PSCue) version unknown - module not loaded"
        }
        return
    }

    # List all bookmarks
    if ($ListBookmarks) {
        $bm = [PSCue.Module.PSCueModule]::Bookmarks
        if ($null -eq $bm) {
            Write-Error "PSCue module not initialized."
            return
        }
        $all = $bm.GetAll()
        if ($all.Count -eq 0) {
            Write-Host "No bookmarks yet. Use 'pcd -b' to bookmark the current directory." -ForegroundColor Yellow
            return
        }
        Write-Host "Bookmarks ($($all.Count)):" -ForegroundColor Cyan
        foreach ($bmPath in $all) {
            Write-Host "  $bmPath"
        }
        return
    }

    # Toggle bookmark on current directory or explicit path
    if ($Bookmark) {
        $bm = [PSCue.Module.PSCueModule]::Bookmarks
        if ($null -eq $bm) {
            Write-Error "PSCue module not initialized."
            return
        }

        # Resolve target path: explicit argument or $PWD
        $targetPath = if (-not [string]::IsNullOrWhiteSpace($Path)) {
            (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
        } else {
            $PWD.Path
        }

        # ToggleAndPersist returns ValueTuple<bool, string> (WasAdded, NormalizedPath)
        # PowerShell can't resolve named tuple elements, use Item1/Item2
        $result = $bm.ToggleAndPersist($targetPath)
        $wasAdded = $result.Item1
        $normalizedPath = $result.Item2

        if ($wasAdded) {
            Write-Host "Bookmarked: $normalizedPath" -ForegroundColor Green
        } else {
            Write-Host "Removed bookmark: $normalizedPath" -ForegroundColor Yellow
        }
        return
    }

    # When invoked as 'pcdi', implicitly enable interactive mode
    if ($MyInvocation.InvocationName -eq 'pcdi') {
        $Interactive = [switch]::new($true)
    }

    # Root mode: navigate to git repository root (or filesystem root if not in a repo)
    if ($Root) {
        $dir = $PWD.Path
        $gitRoot = $null
        while ($dir) {
            if (Test-Path -LiteralPath (Join-Path $dir '.git')) {
                $gitRoot = $dir
                break
            }
            $parent = Split-Path $dir -Parent
            if ($parent -eq $dir -or [string]::IsNullOrEmpty($parent)) { break }
            $dir = $parent
        }

        if (-not $gitRoot) {
            $gitRoot = [System.IO.Path]::GetPathRoot($PWD.Path)
        }

        $oldLocation = $PWD.Path
        Set-Location -LiteralPath $gitRoot
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($gitRoot), $oldLocation)
        }
        return
    }

    # Interactive mode: show selection prompt
    if ($Interactive) {
        if ($null -eq [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            Write-Error "PSCue module not initialized. Cannot show interactive selection."
            return
        }

        try {
            $selector = [PSCue.Module.PcdInteractiveSelector]::new(
                [PSCue.Module.PSCueModule]::KnowledgeGraph,
                [PSCue.Module.PSCueModule]::Bookmarks
            )

            $selectedPath = $selector.ShowSelectionPrompt($PWD.Path, $Top, $Path)

            if ($null -ne $selectedPath) {
                $oldLocation = $PWD.Path
                Set-Location -LiteralPath $selectedPath
                # Manually record navigation under 'cd' command so stats are updated
                # FeedbackProvider only sees 'pcd -i', not the internal Set-Location call
                [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($selectedPath), $oldLocation)
            }
            # User cancelled (Esc) - do nothing
        }
        catch {
            Write-Error "Failed to show interactive selection: $_"
        }

        return
    }

    # Navigate to previous directory (like cd -)
    if ($Path -eq '-') {
        $oldLocation = $PWD.Path
        Set-Location -
        $newLocation = $PWD.Path
        Write-Host $newLocation
        # Record navigation
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($newLocation), $oldLocation)
        }
        return
    }

    # If no path specified, go to home directory (like native cd)
    if ([string]::IsNullOrWhiteSpace($Path)) {
        $oldLocation = $PWD.Path
        Set-Location ~
        # Record navigation
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @('~'), $oldLocation)
        }
        return
    }

    # Try Set-Location first - if it works, we're done
    if (Test-Path -LiteralPath $Path -PathType Container) {
        $oldLocation = $PWD.Path
        Set-Location -LiteralPath $Path
        # Record navigation
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($Path), $oldLocation)
        }
        return
    }

    # Path doesn't exist - try to find best match using PcdCompletionEngine
    if ($null -eq [PSCue.Module.PSCueModule]::KnowledgeGraph) {
        # Not initialized - fall back to Set-Location error
        Set-Location $Path
        return
    }

    try {
        # Get current directory
        $currentDir = (Get-Location).Path

        # Create PcdCompletionEngine using shared configuration
        $engine = [PSCue.Module.PcdCompletionEngine]::new(
            [PSCue.Module.PSCueModule]::KnowledgeGraph,
            [PSCue.Module.PSCueModule]::Bookmarks,
            [PSCue.Module.PcdConfiguration]::ScoreDecayDays,
            [PSCue.Module.PcdConfiguration]::FrequencyWeight,
            [PSCue.Module.PcdConfiguration]::RecencyWeight,
            [PSCue.Module.PcdConfiguration]::DistanceWeight,
            [PSCue.Module.PcdConfiguration]::TabCompletionMaxDepth,  # Use tab-specific depth (deeper for thoroughness)
            [PSCue.Module.PcdConfiguration]::EnableRecursiveSearch,
            [PSCue.Module.PcdConfiguration]::ExactMatchBoost,
            [PSCue.Module.PcdConfiguration]::FuzzyMinMatchPercentage
        )

        # Get best match - request more suggestions for better fuzzy matching
        $suggestions = $engine.GetSuggestions($Path, $currentDir, 10)

        if ($suggestions -and $suggestions.Count -gt 0) {
            # Try suggestions in order until we find one that exists
            foreach ($suggestion in $suggestions) {
                $absolutePath = $suggestion.DisplayPath

                # Verify the matched path exists before navigating
                if (Test-Path -LiteralPath $absolutePath -PathType Container) {
                    Write-Host "No exact match, navigating to: $absolutePath" -ForegroundColor Yellow
                    $oldLocation = $currentDir
                    Set-Location -LiteralPath $absolutePath
                    # Record navigation
                    [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($absolutePath), $oldLocation)
                    return
                }
            }

            # All suggestions exist in database but not on filesystem (stale data or race condition)
            Write-Host "Found $($suggestions.Count) potential match(es) for '$Path', but none exist on the filesystem." -ForegroundColor Red
            Write-Host "Suggestions were:" -ForegroundColor Yellow
            foreach ($suggestion in $suggestions) {
                Write-Host "  - $($suggestion.DisplayPath)" -ForegroundColor Gray
            }
            return
        }

        # No matches found in learned data
        Write-Host "No learned directory matches '$Path'." -ForegroundColor Red
        Write-Host "Tip: Navigate to directories to teach PSCue, or use tab completion to see suggestions." -ForegroundColor Yellow
    }
    catch {
        # If anything goes wrong, fall back to Set-Location
        Write-Debug "PSCue pcd best-match error: $_"
        Set-Location $Path
    }
}

# Register tab completion for Invoke-PCD and pcd alias
Register-ArgumentCompleter -CommandName 'Invoke-PCD', 'pcd', 'pcdi' -ParameterName 'Path' -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameters)

    # Check if PSCue module is initialized
    if ($null -eq [PSCue.Module.PSCueModule]::KnowledgeGraph) {
        # Not initialized - fall back to native file system completion
        # Return nothing, let PowerShell's default completion handle it
        return
    }

    try {
        # Get current directory
        $currentDir = (Get-Location).Path

        # Create PcdCompletionEngine using shared configuration
        $engine = [PSCue.Module.PcdCompletionEngine]::new(
            [PSCue.Module.PSCueModule]::KnowledgeGraph,
            [PSCue.Module.PSCueModule]::Bookmarks,
            [PSCue.Module.PcdConfiguration]::ScoreDecayDays,
            [PSCue.Module.PcdConfiguration]::FrequencyWeight,
            [PSCue.Module.PcdConfiguration]::RecencyWeight,
            [PSCue.Module.PcdConfiguration]::DistanceWeight,
            [PSCue.Module.PcdConfiguration]::TabCompletionMaxDepth,  # Use tab-specific depth (deeper for thoroughness)
            [PSCue.Module.PcdConfiguration]::EnableRecursiveSearch,
            [PSCue.Module.PcdConfiguration]::ExactMatchBoost,
            [PSCue.Module.PcdConfiguration]::FuzzyMinMatchPercentage
        )

        # Get suggestions using enhanced algorithm
        $suggestions = $engine.GetSuggestions($wordToComplete, $currentDir, 20)

        # Convert PcdSuggestion objects to CompletionResult objects
        # Filter out paths that don't exist on the filesystem
        $suggestions | Where-Object { Test-Path -LiteralPath $_.DisplayPath -PathType Container } | ForEach-Object {
            $relativePath = $_.Path          # Relative path (e.g., "..", "./src", "../sibling")
            $fullPath = $_.DisplayPath       # Full absolute path
            $matchType = $_.MatchType
            $tooltip = $_.Tooltip

            # Add match type indicator to tooltip
            $matchIndicator = switch ($matchType) {
                'Bookmark' { '[bookmark]' }
                'WellKnown' { '[shortcut]' }
                'Exact' { '[exact]' }
                'Prefix' { '[prefix]' }
                'Fuzzy' { '[fuzzy]' }
                'Filesystem' { '[found]' }
                default { '[learned]' }
            }
            $fullTooltip = "$matchIndicator $tooltip"

            # Prepare paths for display and completion
            # ListItemText: Clean display without prefixes/separators/quotes (e.g., "Screenshots")
            # CompletionText: What gets inserted, matching native cd behavior (e.g., '.\Screenshots\')

            # For list display: Use directory name only for child/sibling, full path otherwise
            $sep = [System.IO.Path]::DirectorySeparatorChar
            $listItemText = if ([System.IO.Path]::IsPathRooted($relativePath)) {
                # Absolute path - show full path in list (without trailing separator for cleaner display)
                $fullPath.TrimEnd($sep)
            } else {
                # Relative path - show clean name without .\ prefix or trailing separator
                # Remove .\ or ./ prefix if present, but preserve dot-prefixed names like .config
                $cleanPath = $relativePath
                if ($cleanPath.StartsWith(".$sep")) {
                    $cleanPath = $cleanPath.Substring(2)
                }
                $cleanPath.TrimEnd($sep)
            }

            # For completion text: Match native cd behavior exactly
            # - Relative paths: Add .\ prefix and trailing separator (e.g., .\Screenshots\)
            # - Absolute paths: Use as-is with trailing separator
            # - Add single quotes if path contains spaces
            $pathForCompletion = if ([System.IO.Path]::IsPathRooted($relativePath)) {
                # Absolute path - use as-is
                $relativePath
            } else {
                # Relative path - add .\ prefix (PowerShell style on Windows, ./ on Unix)
                if ($relativePath -eq '..') {
                    # Special case: parent directory doesn't need .\ prefix
                    '..'
                } elseif ($relativePath.StartsWith('..' + $sep)) {
                    # Sibling directory (e.g., ..\sibling) - already has proper prefix
                    $relativePath
                } else {
                    # Child directory - add .\ prefix
                    '.' + $sep + $relativePath
                }
            }

            # Ensure trailing separator for directory completion (like native cd)
            if (-not $pathForCompletion.EndsWith($sep)) {
                $pathForCompletion += $sep
            }

            # Add single quotes if path contains spaces (PowerShell style)
            $completionText = if ($pathForCompletion -match '\s') {
                "'$pathForCompletion'"
            } else {
                $pathForCompletion
            }

            [System.Management.Automation.CompletionResult]::new(
                $completionText,    # completionText (what gets inserted - relative path when possible)
                $listItemText,      # listItemText (what's shown in list - context-appropriate)
                'ParameterValue',   # resultType
                $fullTooltip        # toolTip (shows full path)
            )
        }
    }
    catch {
        # If anything goes wrong, fail silently and let native completion take over
        Write-Debug "PSCue pcd completion error: $_"
        return
    }
}

# Note: Function and alias are exported via FunctionsToExport/AliasesToExport in PSCue.psd1 manifest
# Export-ModuleMember is not needed when using dot-sourcing in PSCue.psm1
#endregion

#region Debugging & Diagnostics
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
#endregion
