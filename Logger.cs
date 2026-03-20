using System;
using System.IO;

namespace ScreenRecApp
{
    public static class Logger
    {
        public static event Action<string> OnLog;
        private static readonly string LogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenRecApp", "app.log");

        public static void Log(string message)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(directory) && directory != null)
                    Directory.CreateDirectory(directory);

                string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(LogFilePath, formattedMessage + Environment.NewLine);
                OnLog?.Invoke(formattedMessage);
            }
            catch
            {
                // Ignore log errors
            }
        }

        public static void LogError(Exception ex, string context = "")
        {
            Log($"ERROR [{context}]: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
