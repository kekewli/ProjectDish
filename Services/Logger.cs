using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
namespace ProjectDish.Services
{
    public class Logger
    {
        private static readonly string LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ProjectDishLogs");
        private static readonly string LogPath = Path.Combine(LogFolder, "application.json.log");
        private static readonly object _lock = new object();
        private const string ServiceName = "ProjectDish.WPF";
        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogFolder))
                {
                    Directory.CreateDirectory(LogFolder);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOGGER ERROR] Init failed: {ex.Message}");
            }
        }
        public static void Info(string message, object data = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            WriteLog("INFO", message, data, null, file, line);
        }
        public static void Warn(string message, object data = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            WriteLog("WARN", message, data, null, file, line);
        }
        public static void Error(string message, Exception ex = null, object data = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            WriteLog("ERROR", message, data, ex, file, line);
        }
        private static void WriteLog(string level, string message, object data, Exception ex, string filePath, int lineNumber)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);

                var logEntry = new
                {
                    time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    level = level,
                    service = ServiceName,
                    message = message,
                    details = data,
                    error = ex?.Message,
                    source = $"{fileName}:{lineNumber}"
                };

                string jsonLine = JsonConvert.SerializeObject(logEntry, Formatting.None);

                Debug.WriteLine(jsonLine);

                lock (_lock)
                {
                    File.AppendAllText(LogPath, jsonLine + Environment.NewLine);
                }
            }
            catch (Exception internalEx)
            {
                Debug.WriteLine($"[LOGGER FATAL] {internalEx.Message}");
            }
        }
    }
}
