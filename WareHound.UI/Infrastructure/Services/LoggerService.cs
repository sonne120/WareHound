using System.Diagnostics;

namespace WareHound.UI.Infrastructure.Services
{
    public interface ILoggerService
    {
        void Log(string message);
        void LogError(string message, Exception? exception = null);
        void LogDebug(string message);
    }

    public class DebugLoggerService : ILoggerService
    {
        public void Log(string message)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] INFO: {message}");
        }

        public void LogError(string message, Exception? exception = null)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}");
            if (exception != null)
            {
                Debug.WriteLine($"    Exception: {exception.Message}");
                Debug.WriteLine($"    StackTrace: {exception.StackTrace}");
            }
        }

        public void LogDebug(string message)
        {
#if DEBUG
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] DEBUG: {message}");
#endif
        }
    }
}
