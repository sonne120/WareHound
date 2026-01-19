using System.Runtime.InteropServices;

namespace WareHound.UI.Models;

public class PacketInfo
{
    public int Number { get; set; }
    public int Id { get; set; }
    public int SourcePort { get; set; }
    public int DestPort { get; set; }
    public string Protocol { get; set; } = "";
    public string SourceIp { get; set; } = "";
    public string DestIp { get; set; } = "";
    public string SourceMac { get; set; } = "";
    public string DestMac { get; set; } = "";
    public string HostName { get; set; } = "";
    public DateTime CaptureTime { get; set; }

    string unknown = "Unknown";
    public string TimeDisplay => CaptureTime.ToString("HH:mm:ss.fff");
    public string Info => $"{SourcePort} → {DestPort} | Host:{(string.IsNullOrEmpty(HostName) ? unknown : HostName)} | ID: {Id}";

    public static PacketInfo FromSnapshot(SnapshotStruct snapshot, int number)
    {
        return new PacketInfo
        {
            Number = number,
            Id = snapshot.Id,
            SourcePort = snapshot.SourcePort,
            DestPort = snapshot.DestPort,
            Protocol = snapshot.Protocol ?? "",
            SourceIp = snapshot.SourceIp ?? "",
            DestIp = snapshot.DestIp ?? "",
            SourceMac = snapshot.SourceMac ?? "",
            DestMac = snapshot.DestMac ?? "",
            HostName = snapshot.HostName ?? "",
            CaptureTime = DateTime.Now
        };
    }
}