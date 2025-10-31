namespace PSCue.Module;

/// <summary>
/// Static module state container for PSCue.
/// Provides access to module components from PowerShell functions.
/// </summary>
public static class PSCueModule
{
    /// <summary>
    /// Gets the argument graph instance.
    /// Contains learned command knowledge (command → arguments with usage statistics).
    /// </summary>
    public static ArgumentGraph? KnowledgeGraph { get; internal set; }

    /// <summary>
    /// Gets the command history instance.
    /// Tracks recent command executions (ring buffer, last N commands).
    /// </summary>
    public static CommandHistory? CommandHistory { get; internal set; }

    /// <summary>
    /// Gets the persistence manager instance.
    /// Handles cross-session persistence of learned data using SQLite.
    /// </summary>
    public static PersistenceManager? Persistence { get; internal set; }
}
