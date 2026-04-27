using System;
using PSCue.Shared;
using Xunit;

namespace PSCue.Module.Tests;

public class PathComparerTests
{
    [Fact]
    public void Comparison_IsCaseInsensitiveOnWindows_AndCaseSensitiveElsewhere()
    {
        var expected = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        Assert.Equal(expected, PathComparer.Comparison);
    }

    [Fact]
    public void Equality_IsCaseInsensitiveOnWindows_AndCaseSensitiveElsewhere()
    {
        var expected = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        Assert.Same(expected, PathComparer.Equality);
    }

    [Fact]
    public void CaseDistinctPaths_AreDistinct_OnLinuxAndMacOS()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows filesystems are case-insensitive; case-distinct paths are the same.");
        }

        Assert.False(PathComparer.Equality.Equals("/tmp/Foo", "/tmp/foo"));
    }

    [Fact]
    public void CaseDistinctPaths_AreSame_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Linux/macOS filesystems are case-sensitive; case-distinct paths are different.");
        }

        Assert.True(PathComparer.Equality.Equals(@"C:\Foo", @"C:\foo"));
    }
}
