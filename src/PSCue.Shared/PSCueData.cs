using System;
using System.IO;

namespace PSCue.Shared;

/// <summary>
/// Resolves the PSCue per-user data directory consistently across the
/// ArgumentCompleter exe, the Module DLL, and the Logger.
///
/// Honors PSCUE_DATA_DIR for dev/isolated installs; otherwise falls back to
/// LocalApplicationData on Windows and XDG_DATA_HOME (or ~/.local/share) on
/// Linux/macOS.
/// </summary>
public static class PSCueData
{
    public static string GetDataDirectory()
    {
        var overrideDir = Environment.GetEnvironmentVariable("PSCUE_DATA_DIR");
        if (!string.IsNullOrEmpty(overrideDir))
        {
            return overrideDir;
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "PSCue");
        }

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataHome = string.IsNullOrEmpty(xdgDataHome)
            ? Path.Combine(homeDir, ".local", "share")
            : xdgDataHome;
        return Path.Combine(dataHome, "PSCue");
    }
}
