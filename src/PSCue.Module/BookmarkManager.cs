using System.Collections.Concurrent;

namespace PSCue.Module;

/// <summary>
/// Manages user-bookmarked directories for quick access via pcd.
/// Thread-safe for concurrent reads. Bookmarks are persisted immediately
/// via write-through to PersistenceManager (no delta tracking needed).
/// </summary>
public class BookmarkManager
{
    private readonly ConcurrentDictionary<string, DateTime> _bookmarks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _toggleLock = new();
    private readonly PersistenceManager? _persistence;

    public BookmarkManager(PersistenceManager? persistence = null)
    {
        _persistence = persistence;
    }

    /// <summary>
    /// Initializes the bookmark set from persisted data.
    /// Called once during module load.
    /// </summary>
    public void Initialize(IEnumerable<(string Path, DateTime CreatedAt)> bookmarks)
    {
        foreach (var (path, createdAt) in bookmarks)
        {
            _bookmarks[Normalize(path)] = createdAt;
        }
    }

    /// <summary>
    /// Toggles a bookmark on the given path.
    /// Returns (wasAdded, normalizedPath) so callers can use the canonical path for persistence.
    /// </summary>
    /// <param name="path">Absolute path to toggle. Must be rooted.</param>
    public (bool WasAdded, string NormalizedPath) Toggle(string path)
    {
        var normalized = Normalize(path);

        lock (_toggleLock)
        {
            if (_bookmarks.TryRemove(normalized, out _))
            {
                return (false, normalized);
            }

            _bookmarks[normalized] = DateTime.UtcNow;
            return (true, normalized);
        }
    }

    /// <summary>
    /// Toggles a bookmark and writes the change through to persistence in one step.
    /// If no PersistenceManager was supplied, behaves like <see cref="Toggle"/>.
    /// </summary>
    public (bool WasAdded, string NormalizedPath) ToggleAndPersist(string path)
    {
        var (wasAdded, normalized) = Toggle(path);

        if (_persistence != null)
        {
            if (wasAdded)
            {
                _persistence.SaveBookmark(normalized);
            }
            else
            {
                _persistence.DeleteBookmark(normalized);
            }
        }

        return (wasAdded, normalized);
    }

    /// <summary>
    /// Checks whether a path is bookmarked.
    /// </summary>
    public bool IsBookmarked(string path) =>
        _bookmarks.ContainsKey(Normalize(path));

    /// <summary>
    /// Gets all bookmarked paths, sorted alphabetically.
    /// </summary>
    public IReadOnlyList<string> GetAll() =>
        _bookmarks.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Normalizes a path for canonical storage: absolute, trailing separator stripped.
    /// Callers must pass an absolute path; relative paths would resolve against
    /// the process CWD which may differ from PowerShell's $PWD.
    /// </summary>
    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
