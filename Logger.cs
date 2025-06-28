using System.Collections.Concurrent;

namespace AddonManager
{
    public static class Logger
    {
        // Use a thread-safe collection for logs to prevent race conditions
        // when logging from multiple threads (e.g., Parallel.ForEach).
        private static readonly ConcurrentBag<string> logs = new ConcurrentBag<string>();

        public static void Log(string message, string level = "INFO")
        {
            // The timestamp provides more context for debugging.
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            logs.Add($"{timestamp} [{level}] {message}");
        }
        
        // Returns a snapshot of the logs.
        public static List<string> GetLogs()
        {
            return logs.ToList();
        }
    }
}

// Logger.Log("Message here!", "INFO/WARN/ERROR")
// The level is optional, it will default to INFO.
// Colors are Cyan, Yellow, and Red. Unknown is white.