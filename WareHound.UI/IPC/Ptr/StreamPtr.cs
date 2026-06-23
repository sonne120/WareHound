using System;
using System.Runtime.InteropServices;
using WareHound.UI.Infrastructure.Services;

namespace WareHound.UI.IPC.Ptr
{
    public static class StreamPtr
    {
        private static ILoggerService? _logger;

        public static void SetLogger(ILoggerService logger) => _logger = logger;

        private const string DllName = "WareHound.Sniffer.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "fnCPPDLL")]
        private static extern void fnCPPDLL(int dev);

        private static readonly object _sync = new();
        private static bool _isInitialized;
        private static int _activeDevice = -1;

        public static bool IsLoaded => _isInitialized;

        public static int ActiveDevice => _activeDevice;

        public static void StartStream(int deviceIndex)
        {
            lock (_sync)
            {
                if (_isInitialized)
                    return;

                try
                {
                    fnCPPDLL(deviceIndex);
                    _activeDevice = deviceIndex;
                    _isInitialized = true;
                }
                catch (DllNotFoundException ex)
                {
                    _logger?.LogError($"[StreamPtr] ERROR: DLL not found: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[StreamPtr] fnCPPDLL failed: {ex.GetType().Name} - {ex.Message}", ex);
                }
            }
        }

        public static void Reset()
        {
            lock (_sync)
            {
                _isInitialized = false;
                _activeDevice = -1;
            }
        }
    }
}
