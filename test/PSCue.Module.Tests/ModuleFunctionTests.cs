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
    [Theory]
    [InlineData("LearningManagement.ps1")]
    [InlineData("DatabaseManagement.ps1")]
    [InlineData("WorkflowManagement.ps1")]
    [InlineData("PCD.ps1")]
    [InlineData("Debugging.ps1")]
    public void FunctionFile_DoesNotContain_ExportModuleMember(string fileName)
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var filePath = Path.Combine(solutionRoot, "module", "Functions", fileName);

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
            $"{fileName} should not contain uncommented Export-ModuleMember calls. " +
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
    public void ModuleScript_DotSourcesAllFunctionFiles()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var moduleScriptPath = Path.Combine(solutionRoot, "module", "PSCue.psm1");
        var moduleContent = File.ReadAllText(moduleScriptPath);

        var functionFiles = new[]
        {
            "LearningManagement.ps1",
            "DatabaseManagement.ps1",
            "WorkflowManagement.ps1",
            "PCD.ps1",
            "Debugging.ps1"
        };

        // Assert - each file should be dot-sourced
        foreach (var file in functionFiles)
        {
            Assert.Contains($". $PSScriptRoot/Functions/{file}", moduleContent, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FunctionFiles_ExistInModuleDirectory()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var functionsDir = Path.Combine(solutionRoot, "module", "Functions");

        var expectedFiles = new[]
        {
            "LearningManagement.ps1",
            "DatabaseManagement.ps1",
            "WorkflowManagement.ps1",
            "PCD.ps1",
            "Debugging.ps1"
        };

        // Assert
        Assert.True(Directory.Exists(functionsDir), $"Functions directory should exist at {functionsDir}");

        foreach (var file in expectedFiles)
        {
            var filePath = Path.Combine(functionsDir, file);
            Assert.True(File.Exists(filePath), $"Function file {file} should exist at {filePath}");
        }
    }

    [Theory]
    [InlineData("LearningManagement.ps1", "Clear-PSCueLearning")]
    [InlineData("DatabaseManagement.ps1", "Get-PSCueDatabaseStats")]
    [InlineData("WorkflowManagement.ps1", "Get-PSCueWorkflows")]
    [InlineData("PCD.ps1", "Invoke-PCD")]
    [InlineData("Debugging.ps1", "Test-PSCueCompletion")]
    public void FunctionFile_DefinesExpectedFunction(string fileName, string expectedFunction)
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var filePath = Path.Combine(solutionRoot, "module", "Functions", fileName);
        var content = File.ReadAllText(filePath);

        // Assert - function should be defined
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
