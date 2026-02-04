using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.ViewModels;

public class StatisticsViewModel : ViewModelBase
{
    private readonly DispatcherTimer _refreshTimer;
    private readonly ConcurrentDictionary<string, long> _protocolCounts = new();
    private readonly ConcurrentDictionary<string, long> _sourceIpCounts = new();
    private readonly ConcurrentDictionary<string, long> _destIpCounts = new();
    private readonly ConcurrentDictionary<int, long> _destPortCounts = new();
    private long _packetCount;
    private DateTime _captureStartTime = DateTime.Now;

    [Reactive] public long TotalPackets { get; set; }
    [Reactive] public double PacketsPerSecond { get; set; }
    [Reactive] public string CaptureTime { get; set; } = "00:00:00";
    [Reactive] public int UniqueProtocols { get; set; }
    [Reactive] public int UniqueIPs { get; set; }
    [Reactive] public bool UseNativeStatistics { get; set; } = false;
    [Reactive] public bool IsCapturing { get; set; }

    public ObservableCollection<ProtocolStats> ProtocolStats { get; } = new();
    public ObservableCollection<TalkerInfo> TopSourceIPs { get; } = new();
    public ObservableCollection<TalkerInfo> TopDestIPs { get; } = new();
    public ObservableCollection<PortInfo> TopPorts { get; } = new();

    public bool IsNativeStatsAvailable => false;
    public string StatsSourceText => UseNativeStatistics
        ? "Using C++ Native Statistics (FlowTracker)"
        : "Using C# Managed Statistics";

    // Commands
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleStatsSourceCommand { get; }

    public StatisticsViewModel()
    {
        RefreshCommand = ReactiveCommand.Create(RefreshStatistics);
        ClearCommand = ReactiveCommand.Create(ClearStatistics);
        ToggleStatsSourceCommand = ReactiveCommand.Create(ToggleStatsSource);
        
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => RefreshStatistics();
    }

    public void OnCaptureStarted()
    {
        _captureStartTime = DateTime.Now;
        IsCapturing = true;
        _refreshTimer.Start();
    }

    public void OnCaptureStopped()
    {
        IsCapturing = false;
        _refreshTimer.Stop();
        RefreshStatistics();
    }

    public void OnPacketsCaptured(IEnumerable<PacketInfo> packets)
    {
        foreach (var packet in packets)
        {
            OnPacketCaptured(packet);
        }
    }

    public void OnPacketCaptured(PacketInfo packet)
    {
        Interlocked.Increment(ref _packetCount);

        if (!string.IsNullOrEmpty(packet.Protocol))
        {
            _protocolCounts.AddOrUpdate(packet.Protocol, 1, (_, count) => count + 1);
        }

        if (!string.IsNullOrEmpty(packet.SourceIp))
        {
            _sourceIpCounts.AddOrUpdate(packet.SourceIp, 1, (_, count) => count + 1);
        }

        if (!string.IsNullOrEmpty(packet.DestIp))
        {
            _destIpCounts.AddOrUpdate(packet.DestIp, 1, (_, count) => count + 1);
        }

        if (packet.DestPort > 0)
        {
            _destPortCounts.AddOrUpdate(packet.DestPort, 1, (_, count) => count + 1);
        }
    }

    private void RefreshStatistics()
    {
        var totalPackets = Interlocked.Read(ref _packetCount);
        var divisor = totalPackets > 0 ? totalPackets : 1;

        TotalPackets = totalPackets;

        // Calculate packets per second
        var elapsed = DateTime.Now - _captureStartTime;
        PacketsPerSecond = elapsed.TotalSeconds > 0 ? totalPackets / elapsed.TotalSeconds : 0;
        CaptureTime = elapsed.ToString(@"hh\:mm\:ss");

        // Protocol stats
        var protocolStats = _protocolCounts
            .OrderByDescending(x => x.Value)
            .Take(10)
            .Select(kvp => new ProtocolStats
            {
                Protocol = kvp.Key,
                PacketCount = kvp.Value,
                Percentage = (double)kvp.Value / divisor * 100
            })
            .ToList();

        UpdateCollection(ProtocolStats, protocolStats);
        UniqueProtocols = _protocolCounts.Count;

        // Top source IPs
        var topSourceIPs = _sourceIpCounts
            .OrderByDescending(x => x.Value)
            .Take(5)
            .Select(kvp => new TalkerInfo { IP = kvp.Key, PacketCount = kvp.Value })
            .ToList();

        UpdateCollection(TopSourceIPs, topSourceIPs);

        // Top destination IPs
        var topDestIPs = _destIpCounts
            .OrderByDescending(x => x.Value)
            .Take(5)
            .Select(kvp => new TalkerInfo { IP = kvp.Key, PacketCount = kvp.Value })
            .ToList();

        UpdateCollection(TopDestIPs, topDestIPs);

        // Unique IPs
        var allIPs = _sourceIpCounts.Keys.Union(_destIpCounts.Keys).Count();
        UniqueIPs = allIPs;

        // Top ports
        var topPorts = _destPortCounts
            .OrderByDescending(x => x.Value)
            .Take(5)
            .Select(kvp => new PortInfo
            {
                Port = kvp.Key,
                PacketCount = kvp.Value,
                ServiceName = GetServiceName(kvp.Key)
            })
            .ToList();

        UpdateCollection(TopPorts, topPorts);
    }

    private static void UpdateCollection<T>(ObservableCollection<T> collection, List<T> newItems)
    {
        collection.Clear();
        foreach (var item in newItems)
        {
            collection.Add(item);
        }
    }

    private void ToggleStatsSource()
    {
        UseNativeStatistics = !UseNativeStatistics;
        this.RaisePropertyChanged(nameof(StatsSourceText));
    }

    private void ClearStatistics()
    {
        _protocolCounts.Clear();
        _sourceIpCounts.Clear();
        _destIpCounts.Clear();
        _destPortCounts.Clear();
        Interlocked.Exchange(ref _packetCount, 0);
        _captureStartTime = DateTime.Now;

        TotalPackets = 0;
        PacketsPerSecond = 0;
        CaptureTime = "00:00:00";
        UniqueProtocols = 0;
        UniqueIPs = 0;
        ProtocolStats.Clear();
        TopSourceIPs.Clear();
        TopDestIPs.Clear();
        TopPorts.Clear();
    }

    private static string GetServiceName(int port) => port switch
    {
        20 => "FTP-Data",
        21 => "FTP",
        22 => "SSH",
        23 => "Telnet",
        25 => "SMTP",
        53 => "DNS",
        67 or 68 => "DHCP",
        80 => "HTTP",
        110 => "POP3",
        123 => "NTP",
        143 => "IMAP",
        443 => "HTTPS",
        445 => "SMB",
        3306 => "MySQL",
        3389 => "RDP",
        5432 => "PostgreSQL",
        6379 => "Redis",
        8080 => "HTTP-Alt",
        _ => ""
    };
}

public class ProtocolStats
{
    public string Protocol { get; set; } = "";
    public long PacketCount { get; set; }
    public double Percentage { get; set; }
}

public class TalkerInfo
{
    public string IP { get; set; } = "";
    public long PacketCount { get; set; }
}

public class PortInfo
{
    public int Port { get; set; }
    public long PacketCount { get; set; }
    public string ServiceName { get; set; } = "";
    public string Display => string.IsNullOrEmpty(ServiceName) ? Port.ToString() : $"{Port} ({ServiceName})";
}
