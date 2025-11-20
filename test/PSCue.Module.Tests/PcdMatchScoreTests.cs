using System;
using System.Reflection;
using PSCue.Module;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests for CalculateMatchScore method in PcdCompletionEngine.
/// This tests the directory name matching feature.
/// </summary>
public class PcdMatchScoreTests
{
    [Fact]
    public void CalculateMatchScore_ExactDirectoryName_Returns1()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var engine = new PcdCompletionEngine(graph);
        var method = typeof(PcdCompletionEngine).GetMethod("CalculateMatchScore",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var score = (double)method!.Invoke(engine, new object[] { "D:\\source\\datadog\\dd-trace-dotnet", "dd-trace-dotnet" })!;

        // Assert
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void CalculateMatchScore_ExactDirectoryNameWithTrailingSlash_Returns1()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var engine = new PcdCompletionEngine(graph);
        var method = typeof(PcdCompletionEngine).GetMethod("CalculateMatchScore",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var score = (double)method!.Invoke(engine, new object[] { "D:\\source\\datadog\\dd-trace-dotnet\\", "dd-trace-dotnet" })!;

        // Assert
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void CalculateMatchScore_PartialDirectoryName_ReturnsGreaterThan0()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var engine = new PcdCompletionEngine(graph);
        var method = typeof(PcdCompletionEngine).GetMethod("CalculateMatchScore",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var score = (double)method!.Invoke(engine, new object[] { "D:\\source\\datadog\\dd-trace-dotnet", "dd-trace" })!;

        // Assert
        Assert.True(score > 0.0, $"Expected score > 0 for partial match, got {score}");
    }

    [Fact]
    public void CalculateMatchScore_DifferentDirectoryName_Returns0()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var engine = new PcdCompletionEngine(graph);
        var method = typeof(PcdCompletionEngine).GetMethod("CalculateMatchScore",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var score = (double)method!.Invoke(engine, new object[] { "D:\\source\\datadog\\some-other-project", "dd-trace-dotnet" })!;

        // Assert
        Assert.Equal(0.0, score);
    }
}
