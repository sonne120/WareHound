using System.Runtime.InteropServices;

namespace WareHound.UI.Models
{
    [StructLayout(LayoutKind.Sequential, Pack = 2, CharSet = CharSet.Ansi)]
    public struct SnapshotStruct
    {
        public int Id;
        public int SourcePort;
        public int DestPort;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 22)]
        public string Protocol;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 22)]
        public string SourceIp;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 22)]
        public string DestIp;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 22)]
        public string SourceMac;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 22)]
        public string DestMac;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 22)]
        public string HostName;
    }
}
