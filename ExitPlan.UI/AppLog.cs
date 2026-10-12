using System.IO;
using ExitPlan.Core;

namespace ExitPlan.UI;

internal static class AppLog
{
    public static void Write(string message, Exception? exception = null)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} {message}";
        if (exception != null)
            line += Environment.NewLine + exception;
        line += Environment.NewLine;

        try
        {
            var directory = AppDataPaths.LogDirectory;
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "exitplan.log"), line);
        }
        catch
        {
            // A logging failure must not take down startup or an update check.
        }

        System.Diagnostics.Trace.WriteLine(line);
    }
}
