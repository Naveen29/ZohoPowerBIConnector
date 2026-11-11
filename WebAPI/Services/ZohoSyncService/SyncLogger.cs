namespace ZohoPowerBIConnector.Services.ZohoSyncService
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Logging;

    public static class SyncLogger
    {
        private static ILogger? _logger;
        private static string? logFile;

        public static void Initialize(ILogger logger, string logFolder = "Logs", bool fileLoggingEnabled = true)
        {
            _logger = logger;

            if (!fileLoggingEnabled)
                return;

            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);

            logFile = Path.Combine(
                logFolder,
                $"ZohoSync_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        public static void Log(string message)
        {
            _logger?.LogInformation(message);

            if (string.IsNullOrEmpty(logFile))
                return;

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}";
            Console.WriteLine(line);
            File.AppendAllText(logFile, line + Environment.NewLine);
        }

        public static void Log(Exception ex, string message = "")
        {
            _logger?.LogError(ex, message);

            if (string.IsNullOrEmpty(logFile))
                return;

            string log = $@"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ERROR
Message: {message}
Exception: {ex.Message}
StackTrace: {ex.StackTrace}
---------------------------------------------------";

            File.AppendAllText(logFile, log + Environment.NewLine);
        }
    }
}
