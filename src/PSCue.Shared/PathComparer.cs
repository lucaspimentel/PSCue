using System;

namespace PSCue.Shared;

/// <summary>
/// OS-conditional comparer for filesystem paths and user-supplied argument values.
/// On Windows the filesystem is case-insensitive, so paths compare with OrdinalIgnoreCase.
/// On Linux and macOS paths are case-sensitive, so paths compare with Ordinal.
/// </summary>
public static class PathComparer
{
    /// <summary>
    /// StringComparer suitable for dictionary keys that hold paths or path-like argument values.
    /// </summary>
    public static StringComparer Equality { get; } =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>
    /// StringComparison suitable for Equals/StartsWith/Contains calls on paths.
    /// </summary>
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
