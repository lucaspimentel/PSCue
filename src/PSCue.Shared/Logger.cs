using System.Text;

namespace PSCue.Shared;

public static class Logger
{
    private static readonly Lock FileLock = new();
    private static readonly StreamWriter? StreamWriter;
    private static readonly bool IsDebugMode;
    private static readonly string ComponentName;

    static Logger()
    {
        // Only initialize file logging when PSCUE_DEBUG=1
        IsDebugMode = Environment.GetEnvironmentVariable("PSCUE_DEBUG") == "1";

        // Detect component name from process name
        var processName = Environment.ProcessPath != null
            ? Path.GetFileNameWithoutExtension(Environment.ProcessPath)
            : "Unknown";

        // Map process names to friendly component names
        ComponentName = processName switch
        {
            "pscue-completer" => "ArgumentCompleter",
            "pwsh" => "Module",
            "powershell" => "Module",
            "pscue-debug" => "Debug",
            _ => processName
        };

        if (!IsDebugMode)
        {
            return;
        }

        try
        {
            // ${env:LOCALAPPDATA}, e.g. C:\Users\${env:USERNAME}\AppData\Local
            var localAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(localAppDataFolder, "PSCue");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = Path.Combine(folder, "log.txt");
            // FileShare.ReadWrite allows multiple processes to write to the same log file
            var fileStream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            StreamWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };
        }
        catch
        {
            // ignored
        }
    }

    public static void Write(string message)
    {
        if (!IsDebugMode)
        {
            return;
        }

        // ReSharper disable once InconsistentlySynchronizedField
        if (StreamWriter is null)
        {
            return;
        }

        var log = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.ffff} [{ComponentName}] {message}";

        lock (FileLock)
        {
            StreamWriter.WriteLine(log);
            // AutoFlush is enabled, so no need to call Flush() explicitly
        }
    }

    /// <summary>
    /// Writes a critical error message to the log file regardless of PSCUE_DEBUG setting.
    /// This is used for exceptions and errors that should always be logged.
    /// </summary>
    public static void WriteError(string message)
    {
        try
        {
            // Ensure log directory exists
            var localAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(localAppDataFolder, "PSCue");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = Path.Combine(folder, "log.txt");
            var log = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.ffff} [{ComponentName}] ERROR: {message}";

            // Append to log file with appropriate file sharing
            lock (FileLock)
            {
                File.AppendAllText(path, log + Environment.NewLine);
            }
        }
        catch
        {
            // If logging fails, silently ignore - we don't want to crash the module
        }
    }
}
