using System.Diagnostics;
using WareHound.UI.Infrastructure.Services;
using System.Runtime.InteropServices;

namespace WareHound.UI.IPC.Ptr
{
    /// <summary>
    /// P/Invoke wrapper for fnStopCapture - signals C++ to stop packet capture
    /// </summary>
    public static class StopCapturePtr
    {
        private static ILoggerService? _logger;

        public static void SetLogger(ILoggerService logger) => _logger = logger;

        private const string DllName = "WareHound.Sniffer.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "fnStopCapture")]
        private static extern void fnStopCapture();

        /// <summary>
        /// Signals the C++ capture thread to stop capturing packets.
        /// Sets quit_flag = false in C++.
        /// </summary>
        public static void Stop()
        {
            _logger?.LogDebug("[StopCapturePtr] Calling fnStopCapture()...");
            fnStopCapture();
            _logger?.LogDebug("[StopCapturePtr] fnStopCapture() completed");
        }
    }
}
