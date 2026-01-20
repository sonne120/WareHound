using System.ComponentModel;
using System.Runtime.InteropServices;
using WareHound.UI.Infrastructure.Events;

namespace WareHound.UI.Models;

public class PacketInfo : INotifyPropertyChanged
{
    private static TimeFormatType _currentTimeFormat = TimeFormatType.Relative;
    private static DateTime _captureStartTime = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;

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
    
    public string TimeDisplay => _currentTimeFormat switch
    {
        TimeFormatType.Absolute => CaptureTime.ToString("HH:mm:ss.fff"),
        TimeFormatType.Delta => $"+{(CaptureTime - _captureStartTime).TotalSeconds:F3}s",
        _ => CaptureTime.ToString("HH:mm:ss.fff") // Relative (default)
    };
    
    public string Info => $"{SourcePort} → {DestPort} | Host:{(string.IsNullOrEmpty(HostName) ? unknown : HostName)} | ID: {Id}";

    public static void SetTimeFormat(TimeFormatType format)
    {
        _currentTimeFormat = format;
    }

    public static void SetCaptureStartTime(DateTime startTime)
    {
        _captureStartTime = startTime;
    }

    public void NotifyTimeDisplayChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeDisplay)));
    }

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