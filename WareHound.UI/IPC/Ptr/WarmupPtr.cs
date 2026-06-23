using System.Runtime.InteropServices;

namespace WareHound.UI.IPC.Ptr
{
    public static class WarmupPtr
    {
        private const string DllName = "WareHound.Sniffer.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "fnWarmupDevice")]
        private static extern void fnWarmupDevice(int dev);

        public static void Warmup(int deviceIndex)
        {
            fnWarmupDevice(deviceIndex);
        }
    }
}
