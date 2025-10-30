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
