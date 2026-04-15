using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PSCue.Module.Tests;

public class BookmarkManagerTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly PersistenceManager _persistence;
    private readonly string _tempDir;

    public BookmarkManagerTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PSCue.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        _tempDir = tempDir;
        _testDbPath = Path.Combine(tempDir, "bookmarks-test.db");
        _persistence = new PersistenceManager(_testDbPath);
    }

    public void Dispose()
    {
        _persistence.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void ToggleAndPersist_AddsBookmark_AndWritesThroughToPersistence()
    {
        var bm = new BookmarkManager(_persistence);
        var path = Path.Combine(Path.GetTempPath(), "bm-add");

        var result = bm.ToggleAndPersist(path);

        Assert.True(result.WasAdded);
        Assert.True(bm.IsBookmarked(path));

        var loaded = _persistence.LoadBookmarks();
        Assert.Contains(loaded, b => string.Equals(b.Path, result.NormalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ToggleAndPersist_RemovesBookmark_AndDeletesFromPersistence()
    {
        var bm = new BookmarkManager(_persistence);
        var path = Path.Combine(Path.GetTempPath(), "bm-remove");

        bm.ToggleAndPersist(path); // add
        var result = bm.ToggleAndPersist(path); // remove

        Assert.False(result.WasAdded);
        Assert.False(bm.IsBookmarked(path));

        var loaded = _persistence.LoadBookmarks();
        Assert.DoesNotContain(loaded, b => string.Equals(b.Path, result.NormalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsBookmarked_PreTrimmedDriveLetter_ResolvesViaCwd_IsBuggy()
    {
        // Documents (and guards against regressions of) the callers' pre-trim pitfall:
        // passing "C:" (drive letter with no separator) causes Path.GetFullPath to
        // resolve it against the per-drive current directory. Callers must pass paths
        // with the drive separator intact (e.g. "C:\") to get stable drive-root semantics.

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var originalCwd = Environment.CurrentDirectory;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!userProfile.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Environment.CurrentDirectory = userProfile;

            var bm = new BookmarkManager();
            bm.ToggleAndPersist(userProfile);

            // Passing "C:" (stripped of separator) incorrectly resolves to the CWD.
            // This is WHY callers must preserve the trailing separator.
            Assert.True(bm.IsBookmarked("C:"));

            // Passing "C:\" (with separator) correctly anchors at the drive root.
            Assert.False(bm.IsBookmarked(@"C:\"));
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    [Fact]
    public void IsBookmarked_DriveRootIsIndependentOfDriveRelativeCwd()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Drive-relative paths are a Windows-only concern
        }

        // Simulate being on the C: drive with a CWD of C:\Users\lucas.
        // The original CWD is restored after the test so we don't affect other tests.
        var originalCwd = Environment.CurrentDirectory;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Path.IsPathRooted(userProfile) || !userProfile.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
        {
            return; // Test requires a C:-rooted user profile
        }

        try
        {
            Environment.CurrentDirectory = userProfile;

            var bm = new BookmarkManager();
            bm.ToggleAndPersist(userProfile); // Bookmark the home dir, NOT the drive root

            // Querying the drive root with a trailing separator must not match
            // because Path.GetFullPath("C:") would otherwise resolve to the CWD.
            Assert.False(bm.IsBookmarked(@"C:\"));
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    [Fact]
    public void ToggleAndPersist_WithoutPersistence_StillToggles()
    {
        var bm = new BookmarkManager();
        var path = Path.Combine(Path.GetTempPath(), "bm-no-persist");

        var added = bm.ToggleAndPersist(path);
        var removed = bm.ToggleAndPersist(path);

        Assert.True(added.WasAdded);
        Assert.False(removed.WasAdded);
        Assert.False(bm.IsBookmarked(path));
    }
}
