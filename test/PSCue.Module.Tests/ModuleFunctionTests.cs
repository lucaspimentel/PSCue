using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests to ensure PowerShell function files are correctly structured for dot-sourcing.
/// </summary>
public class ModuleFunctionTests
{
    [Fact]
    public void FunctionsFile_DoesNotContain_ExportModuleMember()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var filePath = Path.Combine(solutionRoot, "module", "Functions.ps1");

        // Act
        var content = File.ReadAllText(filePath);
        var lines = content.Split('\n');

        // Assert - check for uncommented Export-ModuleMember calls
        var hasActiveExportModuleMember = lines.Any(line =>
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith("Export-ModuleMember", StringComparison.Ordinal) &&
                   !trimmed.StartsWith("#", StringComparison.Ordinal);
        });

        Assert.False(hasActiveExportModuleMember,
            "Functions.ps1 should not contain uncommented Export-ModuleMember calls. " +
            "Functions are exported via FunctionsToExport in PSCue.psd1 manifest. " +
            "Export-ModuleMember in dot-sourced files causes silent load failures.");
    }

    [Fact]
    public void ModuleManifest_ExportsAllExpectedFunctions()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var manifestPath = Path.Combine(solutionRoot, "module", "PSCue.psd1");
        var manifestContent = File.ReadAllText(manifestPath);

        // Expected functions that should be exported
        var expectedFunctions = new[]
        {
            // Learning Management
            "Get-PSCueLearning",
            "Clear-PSCueLearning",
            "Export-PSCueLearning",
            "Import-PSCueLearning",
            "Save-PSCueLearning",
            // Database Management
            "Get-PSCueDatabaseStats",
            "Get-PSCueDatabaseHistory",
            // Workflow Management
            "Get-PSCueWorkflows",
            "Get-PSCueWorkflowStats",
            "Clear-PSCueWorkflows",
            "Export-PSCueWorkflows",
            "Import-PSCueWorkflows",
            // Navigation
            "Invoke-PCD",
            // Debugging
            "Test-PSCueCompletion",
            "Get-PSCueModuleInfo"
        };

        // Assert - each function should be in FunctionsToExport
        foreach (var function in expectedFunctions)
        {
            Assert.Contains($"'{function}'", manifestContent, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ModuleScript_DotSourcesConsolidatedFunctionsFile()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var moduleScriptPath = Path.Combine(solutionRoot, "module", "PSCue.psm1");
        var moduleContent = File.ReadAllText(moduleScriptPath);

        // Assert - the consolidated file should be dot-sourced
        Assert.Contains(". $PSScriptRoot/Functions.ps1", moduleContent, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionsFile_ExistsInModuleDirectory()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var functionsFile = Path.Combine(solutionRoot, "module", "Functions.ps1");

        // Assert
        Assert.True(File.Exists(functionsFile), $"Functions.ps1 should exist at {functionsFile}");
    }

    [Theory]
    [InlineData("Clear-PSCueLearning")]
    [InlineData("Get-PSCueDatabaseStats")]
    [InlineData("Get-PSCueWorkflows")]
    [InlineData("Invoke-PCD")]
    [InlineData("Test-PSCueCompletion")]
    public void FunctionsFile_DefinesExpectedFunction(string expectedFunction)
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var filePath = Path.Combine(solutionRoot, "module", "Functions.ps1");
        var content = File.ReadAllText(filePath);

        // Assert - function should be defined in the consolidated file
        Assert.Contains($"function {expectedFunction}", content, StringComparison.Ordinal);
    }

    private static string FindSolutionRoot()
    {
        // Start from the test assembly location
        var assemblyLocation = typeof(ModuleFunctionTests).Assembly.Location;
        var currentDir = Path.GetDirectoryName(assemblyLocation);

        // Navigate up from bin/Debug/net9.0 to find repository root (has 'module' directory)
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir, "module")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        if (currentDir == null || !Directory.Exists(Path.Combine(currentDir, "module")))
        {
            throw new InvalidOperationException($"Could not find repository root (with 'module' directory) starting from {assemblyLocation}");
        }

        return currentDir;
    }
}
