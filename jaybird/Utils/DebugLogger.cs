namespace jaybird.Utils;

using System.Diagnostics;

public static class DebugLogger
{
    private static readonly string LogFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "jaybird-debug.log"
    );

    static DebugLogger()
    {
#if DEBUG
        // Clear log file on startup in debug mode
        try
        {
            if (File.Exists(LogFilePath))
            {
                File.Delete(LogFilePath);
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void Log(string message, string? context = null)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = context != null
                ? $"[{timestamp}] [{context}] {message}"
                : $"[{timestamp}] {message}";

            File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
        }
        catch
        {
            // Silently fail - logging shouldn't break the app
        }
    }

    [Conditional("DEBUG")]
    public static void LogException(Exception ex, string? context = null)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = context != null
                ? $"[{timestamp}] [{context}] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
                : $"[{timestamp}] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";

            File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
        }
        catch
        {
            // Silently fail - logging shouldn't break the app
        }
    }

    [Conditional("DEBUG")]
    public static void LogStartup()
    {
        Log("========================================");
        Log("jaybird - DEBUG MODE");
        Log($"Log file: {LogFilePath}");
        Log("========================================");
    }
}
