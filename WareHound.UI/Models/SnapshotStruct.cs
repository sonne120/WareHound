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
        
        // Raw packet data for PCAP file save/load
        public uint CaptureLen;
        public uint OriginalLen;
        public ulong TimestampSec;
        public uint TimestampUsec;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 65536)]
        public byte[] RawData;
    }
    
    /// <summary>
    /// Compact snapshot header for IPC (without raw_data for metadata-only transfers).
    /// Total size: ~150 bytes instead of 65KB.
    /// Use this for variable-length IPC where raw_data follows immediately after.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2, CharSet = CharSet.Ansi)]
    public struct SnapshotHeader
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
        
        public uint CaptureLen;      // Actual captured length (use this to know how many bytes follow)
        public uint OriginalLen;     // Original packet length on wire
        public ulong TimestampSec;   // Seconds since Unix epoch
        public uint TimestampUsec;   // Microseconds component
        // raw_data follows immediately after this struct in IPC, with size = CaptureLen
        
        /// <summary>
        /// Converts to full SnapshotStruct with raw data
        /// </summary>
        public SnapshotStruct ToSnapshot(byte[]? rawData)
        {
            return new SnapshotStruct
            {
                Id = Id,
                SourcePort = SourcePort,
                DestPort = DestPort,
                Protocol = Protocol,
                SourceIp = SourceIp,
                DestIp = DestIp,
                SourceMac = SourceMac,
                DestMac = DestMac,
                HostName = HostName,
                CaptureLen = CaptureLen,
                OriginalLen = OriginalLen,
                TimestampSec = TimestampSec,
                TimestampUsec = TimestampUsec,
                RawData = rawData ?? Array.Empty<byte>()
            };
        }
        
        /// <summary>
        /// Gets the total IPC message size for variable-length transfer
        /// </summary>
        public int GetTotalIPCSize() => Marshal.SizeOf<SnapshotHeader>() + (int)CaptureLen;
    }
}
