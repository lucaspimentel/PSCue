using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions;

/// <summary>
/// Provides directory completions for Set-Location (cd) command.
/// Supports subdirectories, parent directories, absolute paths, and common shortcuts.
/// Uses smart caching to maintain <50ms performance target.
/// </summary>
public static class SetLocationCommand
{
    // Cache directory enumerations for 5 seconds to improve performance
    private static readonly ConcurrentDictionary<string, CacheEntry> _directoryCache = new();
    private static readonly TimeSpan CacheTTL = TimeSpan.FromSeconds(5);
    private const int MaxResults = 50;

    public static Command Create() => new("Set-Location", "Change the current working directory")
    {
        DynamicArguments = GetDirectories
    };

    private static IEnumerable<DynamicArgument> GetDirectories()
    {
        var currentDir = Directory.GetCurrentDirectory();

        // Always include common shortcuts first
        yield return new DynamicArgument(".", "Current directory");
        yield return new DynamicArgument("..", "Parent directory");

        // Add home directory shortcut
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(homeDir))
        {
            yield return new DynamicArgument("~", $"Home directory: {homeDir}");
        }

        // Enumerate subdirectories in current directory
        foreach (var dir in EnumerateDirectoriesWithCache(currentDir))
        {
            yield return dir;
        }

        // Enumerate parent directory siblings (one level up)
        var parentDir = Path.GetDirectoryName(currentDir);
        if (!string.IsNullOrEmpty(parentDir))
        {
            foreach (var dir in EnumerateDirectoriesWithCache(parentDir, prefix: ".."))
            {
                yield return dir;
            }
        }
    }

    private static IEnumerable<DynamicArgument> EnumerateDirectoriesWithCache(string basePath, string prefix = "")
    {
        var cacheKey = $"{basePath}|{prefix}";

        // Check cache
        if (_directoryCache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTime.UtcNow - cached.Timestamp < CacheTTL)
            {
                return cached.Items;
            }
            else
            {
                // Remove expired cache entry
                _directoryCache.TryRemove(cacheKey, out _);
            }
        }

        // Enumerate directories and cache results
        var results = new List<DynamicArgument>();
        var startTime = DateTime.UtcNow;

        try
        {
            var directories = Directory.EnumerateDirectories(basePath);
            var count = 0;

            foreach (var dir in directories)
            {
                // Check performance target (<50ms)
                if (count > 0 && count % 10 == 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed.TotalMilliseconds > 40) // Leave 10ms buffer
                    {
                        break;
                    }
                }

                // Stop at max results
                if (count >= MaxResults)
                {
                    break;
                }

                var dirName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(dirName))
                {
                    continue;
                }

                // Build completion text based on prefix
                string completionText;
                if (string.IsNullOrEmpty(prefix))
                {
                    // Current directory - just use directory name
                    completionText = dirName;
                }
                else if (prefix == "..")
                {
                    // Parent directory - use ../dirname format
                    completionText = Path.Combine("..", dirName);
                }
                else
                {
                    completionText = Path.Combine(prefix, dirName);
                }

                var tooltip = $"Directory: {dir}";
                var dynamicArg = new DynamicArgument(completionText, tooltip);

                results.Add(dynamicArg);
                count++;
            }

            // Cache the results
            _directoryCache[cacheKey] = new CacheEntry(results, DateTime.UtcNow);
        }
        catch (UnauthorizedAccessException)
        {
            // Silently ignore directories we can't access
        }
        catch (DirectoryNotFoundException)
        {
            // Silently ignore directories that don't exist
        }
        catch (IOException)
        {
            // Silently ignore I/O errors
        }

        return results;
    }

    /// <summary>
    /// Parses the input to detect context (absolute path, relative path, etc.) and returns appropriate suggestions.
    /// This method is called by GenericPredictor for inline predictions.
    /// </summary>
    public static IEnumerable<string> GetDirectorySuggestions(string wordToComplete)
    {
        return GetDirectorySuggestionsWithPaths(wordToComplete).Select(x => x.CompletionText);
    }

    /// <summary>
    /// Similar to GetDirectorySuggestions but returns both completion text and full path for tooltips.
    /// This method is used by GenericPredictor for inline predictions with rich descriptions.
    /// </summary>
    public static IEnumerable<(string CompletionText, string FullPath)> GetDirectorySuggestionsWithPaths(string wordToComplete)
    {
        if (string.IsNullOrEmpty(wordToComplete))
        {
            // No input yet - suggest current directory subdirectories
            return GetDirectories().Take(10).Select(d => (d.CompletionText, ExtractFullPath(d.Tooltip)));
        }

        // Detect context from input
        if (IsAbsolutePath(wordToComplete))
        {
            // Absolute path - enumerate from specified root
            return GetAbsolutePathSuggestionsWithPaths(wordToComplete);
        }
        else if (wordToComplete.StartsWith("~/") || wordToComplete == "~")
        {
            // Home directory expansion
            return GetHomeDirectorySuggestionsWithPaths(wordToComplete);
        }
        else if (wordToComplete.StartsWith("../"))
        {
            // Parent directory navigation
            return GetParentDirectorySuggestionsWithPaths(wordToComplete);
        }
        else if (wordToComplete.StartsWith("./"))
        {
            // Explicit current directory
            return GetCurrentDirectorySuggestionsWithPaths(wordToComplete.Substring(2));
        }
        else
        {
            // Implicit current directory
            return GetCurrentDirectorySuggestionsWithPaths(wordToComplete);
        }
    }

    private static string ExtractFullPath(string? tooltip)
    {
        if (string.IsNullOrEmpty(tooltip))
        {
            return string.Empty;
        }

        // Tooltip format is "Directory: <full_path>" or "Home directory: <path>"
        var colonIndex = tooltip.IndexOf(':');
        if (colonIndex > 0 && colonIndex < tooltip.Length - 1)
        {
            return tooltip.Substring(colonIndex + 1).Trim();
        }
        return tooltip;
    }

    private static bool IsAbsolutePath(string path)
    {
        // Windows: C:\, D:\, \\server\share
        // Unix: /home, /var
        return Path.IsPathRooted(path);
    }

    private static IEnumerable<string> GetAbsolutePathSuggestions(string partialPath)
    {
        return GetAbsolutePathSuggestionsWithPaths(partialPath).Select(x => x.CompletionText);
    }

    private static IEnumerable<(string CompletionText, string FullPath)> GetAbsolutePathSuggestionsWithPaths(string partialPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(partialPath);
            var fileName = Path.GetFileName(partialPath);

            if (string.IsNullOrEmpty(directory))
            {
                // Just a drive letter or root - enumerate drives/root
                return EnumerateRoots(partialPath).Select(p => (p, p));
            }

            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<(string, string)>();
            }

            return Directory.EnumerateDirectories(directory)
                .Where(d => Path.GetFileName(d)?.StartsWith(fileName, StringComparison.OrdinalIgnoreCase) ?? false)
                .Take(MaxResults)
                .Select(d => (d, d));
        }
        catch
        {
            return Enumerable.Empty<(string, string)>();
        }
    }

    private static IEnumerable<string> EnumerateRoots(string partialPath)
    {
        try
        {
            // On Windows, enumerate drives
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.Name.StartsWith(partialPath, StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.Name);
                return drives;
            }
            else
            {
                // On Unix, enumerate root directories
                return Directory.EnumerateDirectories("/")
                    .Where(d => d.StartsWith(partialPath, StringComparison.Ordinal))
                    .Take(MaxResults);
            }
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> GetHomeDirectorySuggestions(string partialPath)
    {
        return GetHomeDirectorySuggestionsWithPaths(partialPath).Select(x => x.CompletionText);
    }

    private static IEnumerable<(string CompletionText, string FullPath)> GetHomeDirectorySuggestionsWithPaths(string partialPath)
    {
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
            {
                return Enumerable.Empty<(string, string)>();
            }

            if (partialPath == "~")
            {
                // Just "~" - suggest subdirectories of home
                return Directory.EnumerateDirectories(homeDir)
                    .Take(MaxResults)
                    .Select(d => ($"~/{Path.GetFileName(d)}", d));
            }

            // "~/something" - expand and enumerate
            var expandedPath = partialPath.Replace("~", homeDir);
            var directory = Path.GetDirectoryName(expandedPath);
            var fileName = Path.GetFileName(expandedPath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return Enumerable.Empty<(string, string)>();
            }

            return Directory.EnumerateDirectories(directory)
                .Where(d => Path.GetFileName(d)?.StartsWith(fileName, StringComparison.OrdinalIgnoreCase) ?? false)
                .Take(MaxResults)
                .Select(d => (d.Replace(homeDir, "~"), d));
        }
        catch
        {
            return Enumerable.Empty<(string, string)>();
        }
    }

    private static IEnumerable<string> GetParentDirectorySuggestions(string partialPath)
    {
        return GetParentDirectorySuggestionsWithPaths(partialPath).Select(x => x.CompletionText);
    }

    private static IEnumerable<(string CompletionText, string FullPath)> GetParentDirectorySuggestionsWithPaths(string partialPath)
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            var parentDir = Path.GetDirectoryName(currentDir);

            if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir))
            {
                return Enumerable.Empty<(string, string)>();
            }

            // Parse how many levels up we're going
            var parts = partialPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var upCount = parts.Count(p => p == "..");
            var fileName = parts.LastOrDefault(p => p != "..") ?? "";

            // Navigate up the directory tree
            var targetDir = currentDir;
            for (int i = 0; i < upCount && !string.IsNullOrEmpty(targetDir); i++)
            {
                targetDir = Path.GetDirectoryName(targetDir);
            }

            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
            {
                return Enumerable.Empty<(string, string)>();
            }

            // Enumerate directories in target
            var prefix = string.Join("/", Enumerable.Repeat("..", upCount));
            return Directory.EnumerateDirectories(targetDir)
                .Where(d => Path.GetFileName(d)?.StartsWith(fileName, StringComparison.OrdinalIgnoreCase) ?? false)
                .Take(MaxResults)
                .Select(d => (Path.Combine(prefix, Path.GetFileName(d)!), d));
        }
        catch
        {
            return Enumerable.Empty<(string, string)>();
        }
    }

    private static IEnumerable<string> GetCurrentDirectorySuggestions(string partialName)
    {
        return GetCurrentDirectorySuggestionsWithPaths(partialName).Select(x => x.CompletionText);
    }

    private static IEnumerable<(string CompletionText, string FullPath)> GetCurrentDirectorySuggestionsWithPaths(string partialName)
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            if (!Directory.Exists(currentDir))
            {
                return Enumerable.Empty<(string, string)>();
            }

            return Directory.EnumerateDirectories(currentDir)
                .Where(d => Path.GetFileName(d)?.StartsWith(partialName, StringComparison.OrdinalIgnoreCase) ?? false)
                .Take(MaxResults)
                .Select(d => (Path.GetFileName(d)!, d));
        }
        catch
        {
            return Enumerable.Empty<(string, string)>();
        }
    }

    private sealed class CacheEntry
    {
        public List<DynamicArgument> Items { get; }
        public DateTime Timestamp { get; }

        public CacheEntry(List<DynamicArgument> items, DateTime timestamp)
        {
            Items = items;
            Timestamp = timestamp;
        }
    }
}
