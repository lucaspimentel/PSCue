using System.Text;

namespace PSCue.Shared;

public static class Logger
{
    private static readonly Lock FileLock = new();
    private static readonly StreamWriter? StreamWriter;

    static Logger()
    {
        try
        {
            // ${env:LOCALAPPDATA}, e.g. C:\Users\${env:USERNAME}\AppData\Local
            var localAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(localAppDataFolder, "pwsh-argument-completer");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = Path.Combine(folder, "log.txt");
            var fileStream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            StreamWriter = new StreamWriter(fileStream, Encoding.UTF8);
        }
        catch
        {
            // ignored
        }
    }

    public static void Write(string message)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        if (StreamWriter is null)
        {
            return;
        }

        var log = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.ffff} {message}";

        lock (FileLock)
        {
            StreamWriter.WriteLine(log);
            StreamWriter.Flush();
        }
    }
}
