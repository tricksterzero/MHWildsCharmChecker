using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace CharmChecker.App;

internal static class ErrorLogger
{
    private static readonly string LogFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");

    internal static void LogStartup()
    {
        try
        {
            var lines = new[]
            {
                "",
                $"==== Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====",
                $"OS: {RuntimeInformation.OSDescription}",
                $".NET: {RuntimeInformation.FrameworkDescription}",
                $"Screen: {(int)SystemParameters.PrimaryScreenWidth}x{(int)SystemParameters.PrimaryScreenHeight}",
            };
            File.AppendAllLines(LogFilePath, lines);
        }
        catch { }
    }

    internal static void Log(string category, string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {category}: {message}";
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }
        catch { }
    }

    internal static void Log(string category, string message, Exception ex)
    {
        Log(category, $"{message} -- {ex.GetType().Name}: {ex.Message}");
    }
}
