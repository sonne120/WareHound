using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using WareHound.UI.Infrastructure.Events;
using WareHound.UI.IPC;
using WareHound.UI.Models;
using WareHound.UI.Services;

namespace WareHound.UI.ViewModels;

public class StatisticsViewModel : BindableBase, INavigationAware
{
    private readonly ISnifferService _snifferService;
    private readonly IEventAggregator _eventAggregator;
    private readonly INativeStatisticsInterop? _nativeStats;
    
    private CaptureStatistics _statistics = new();
    private bool _isCapturing;
    private bool _useNativeStatistics;
    private System.Windows.Threading.DispatcherTimer? _refreshTimer;

    public CaptureStatistics Statistics
    {
        get => _statistics;
        set => SetProperty(ref _statistics, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        set => SetProperty(ref _isCapturing, value);
    }

    public ObservableCollection<ProtocolStats> ProtocolStats { get; } = new();
    
    // Top talkers
    public ObservableCollection<TalkerInfo> TopSourceIPs { get; } = new();
    public ObservableCollection<TalkerInfo> TopDestIPs { get; } = new();
    public ObservableCollection<PortInfo> TopPorts { get; } = new();

    // Summary stats
    private long _totalPackets;
    public long TotalPackets
    {
        get => _totalPackets;
        set => SetProperty(ref _totalPackets, value);
    }

    private double _packetsPerSecond;
    public double PacketsPerSecond
    {
        get => _packetsPerSecond;
        set => SetProperty(ref _packetsPerSecond, value);
    }

    private string _captureTime = "00:00:00";
    public string CaptureTime
    {
        get => _captureTime;
        set => SetProperty(ref _captureTime, value);
    }

    private int _uniqueProtocols;
    public int UniqueProtocols
    {
        get => _uniqueProtocols;
        set => SetProperty(ref _uniqueProtocols, value);
    }

    private int _uniqueIPs;
    public int UniqueIPs
    {
        get => _uniqueIPs;
        set => SetProperty(ref _uniqueIPs, value);
    }


    public bool IsNativeStatsAvailable => _nativeStats != null;
    

    public string StatsSourceText => _useNativeStatistics 
        ? "Using C++ Native Statistics (FlowTracker)" 
        : "Using C# Managed Statistics";


    // Toggle between C# statistics (false) and C++ native statistics (true)

    public bool UseNativeStatistics
    {
        get => _useNativeStatistics;
        set
        {
            if (SetProperty(ref _useNativeStatistics, value))
            {
                _nativeStats?.EnableNativeStats(value);
                if (value)
                {
                    ClearStatistics();
                }
                RaisePropertyChanged(nameof(StatsSourceText));
                RefreshStatistics();
            }
        }
    }

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand ClearCommand { get; }
    public DelegateCommand ToggleStatsSourceCommand { get; }

    public StatisticsViewModel(ISnifferService snifferService, IEventAggregator eventAggregator)
    {
        _snifferService = snifferService ?? throw new ArgumentNullException(nameof(snifferService));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

        try
        {
            var snifferHandle = _snifferService.GetSnifferHandle();
            if (snifferHandle != IntPtr.Zero)
            {
                _nativeStats = new NativeStatisticsInterop(snifferHandle);
            }
        }
        catch
        {
            _nativeStats = null;
        }

        _eventAggregator.GetEvent<CaptureStateChangedEvent>().Subscribe(OnCaptureStateChanged);
        _eventAggregator.GetEvent<PacketCapturedEvent>().Subscribe(OnPacketCaptured);

        RefreshCommand = new DelegateCommand(RefreshStatistics);
        ClearCommand = new DelegateCommand(ClearStatistics);
        ToggleStatsSourceCommand = new DelegateCommand(() => UseNativeStatistics = !UseNativeStatistics);

        IsCapturing = _snifferService.IsCapturing;
        
        // Auto-refresh timer
        _refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += (s, e) => RefreshStatistics();
    }

    private void OnCaptureStateChanged(bool isCapturing)
    {
        IsCapturing = isCapturing;
        
        if (isCapturing)
        {
            Statistics.CaptureStartTime = DateTime.Now;
            _refreshTimer?.Start();
        }
        else
        {
            Statistics.CaptureEndTime = DateTime.Now;
            _refreshTimer?.Stop();
            RefreshStatistics();
        }
    }

    private void OnPacketCaptured(PacketInfo packet)
    {
        // Update protocol stats
        if (!string.IsNullOrEmpty(packet.Protocol))
        {
            if (!Statistics.ProtocolBreakdown.ContainsKey(packet.Protocol))
            {
                Statistics.ProtocolBreakdown[packet.Protocol] = new ProtocolStats
                {
                    Protocol = packet.Protocol
                };
            }
            Statistics.ProtocolBreakdown[packet.Protocol].PacketCount++;
        }

        // Update IP stats
        if (!string.IsNullOrEmpty(packet.SourceIp))
        {
            if (!Statistics.TopSourceIPs.ContainsKey(packet.SourceIp))
                Statistics.TopSourceIPs[packet.SourceIp] = 0;
            Statistics.TopSourceIPs[packet.SourceIp]++;
        }

        if (!string.IsNullOrEmpty(packet.DestIp))
        {
            if (!Statistics.TopDestIPs.ContainsKey(packet.DestIp))
                Statistics.TopDestIPs[packet.DestIp] = 0;
            Statistics.TopDestIPs[packet.DestIp]++;
        }

        // Update port stats
        if (packet.DestPort > 0)
        {
            if (!Statistics.TopDestPorts.ContainsKey(packet.DestPort))
                Statistics.TopDestPorts[packet.DestPort] = 0;
            Statistics.TopDestPorts[packet.DestPort]++;
        }

        Statistics.TotalPackets++;
        Statistics.CaptureEndTime = DateTime.Now;
    }

    private void RefreshStatistics()
    {
        if (_useNativeStatistics && _nativeStats != null)
        {
            RefreshFromNative();
        }
        else
        {
            RefreshFromManaged();
        }
    }

    private void RefreshFromNative()
    {
        if (_nativeStats == null) return;

        try
        {
            var stats = _nativeStats.GetCaptureStatistics();
            if (stats.HasValue)
            {
                TotalPackets = (long)stats.Value.TotalPackets;
                PacketsPerSecond = stats.Value.PacketsPerSecond;
                CaptureTime = TimeSpan.FromSeconds(stats.Value.CaptureDurationSeconds).ToString(@"hh\:mm\:ss");
                UniqueProtocols = stats.Value.UniqueProtocols;
                UniqueIPs = stats.Value.UniqueSourceIPs + stats.Value.UniqueDestIPs;
            }

            // Update protocol stats from native
            ProtocolStats.Clear();
            var protocols = _nativeStats.GetProtocolStats(10);
            foreach (var proto in protocols)
            {
                ProtocolStats.Add(new Models.ProtocolStats
                {
                    Protocol = proto.ProtocolName,
                    PacketCount = (long)proto.PacketCount,
                    Percentage = proto.Percentage
                });
            }

            // Update top source IPs from native
            TopSourceIPs.Clear();
            var srcIps = _nativeStats.GetTopSourceIPs(5);
            foreach (var ip in srcIps)
            {
                TopSourceIPs.Add(new TalkerInfo { IP = ip.IpAddress, PacketCount = (long)ip.PacketCount });
            }

            // Update top dest IPs from native
            TopDestIPs.Clear();
            var dstIps = _nativeStats.GetTopDestIPs(5);
            foreach (var ip in dstIps)
            {
                TopDestIPs.Add(new TalkerInfo { IP = ip.IpAddress, PacketCount = (long)ip.PacketCount });
            }

            // Update top ports from native
            TopPorts.Clear();
            var ports = _nativeStats.GetTopPorts(5);
            foreach (var port in ports)
            {
                TopPorts.Add(new PortInfo 
                { 
                    Port = port.Port, 
                    PacketCount = (long)port.PacketCount, 
                    ServiceName = port.ServiceName 
                });
            }
        }
        catch
        {
            // Fallback to managed if native fails
            RefreshFromManaged();
        }
    }

    private void RefreshFromManaged()
    {
        TotalPackets = Statistics.TotalPackets;
        PacketsPerSecond = Statistics.PacketsPerSecond;
        CaptureTime = Statistics.Duration.ToString(@"hh\:mm\:ss");
        
        // Update protocol stats
        ProtocolStats.Clear();
        var totalPackets = Statistics.TotalPackets > 0 ? Statistics.TotalPackets : 1;
        foreach (var kvp in Statistics.ProtocolBreakdown.OrderByDescending(x => x.Value.PacketCount).Take(10))
        {
            kvp.Value.Percentage = (double)kvp.Value.PacketCount / totalPackets * 100;
            ProtocolStats.Add(kvp.Value);
        }
        UniqueProtocols = Statistics.ProtocolBreakdown.Count;

        // Update top source IPs
        TopSourceIPs.Clear();
        foreach (var kvp in Statistics.TopSourceIPs.OrderByDescending(x => x.Value).Take(5))
        {
            TopSourceIPs.Add(new TalkerInfo { IP = kvp.Key, PacketCount = kvp.Value });
        }

        // Update top dest IPs
        TopDestIPs.Clear();
        foreach (var kvp in Statistics.TopDestIPs.OrderByDescending(x => x.Value).Take(5))
        {
            TopDestIPs.Add(new TalkerInfo { IP = kvp.Key, PacketCount = kvp.Value });
        }

        UniqueIPs = Statistics.TopSourceIPs.Keys.Union(Statistics.TopDestIPs.Keys).Count();

        // Update top ports
        TopPorts.Clear();
        foreach (var kvp in Statistics.TopDestPorts.OrderByDescending(x => x.Value).Take(5))
        {
            TopPorts.Add(new PortInfo { Port = kvp.Key, PacketCount = kvp.Value, ServiceName = GetServiceName(kvp.Key) });
        }
    }

    private void ClearStatistics()
    {
        Statistics = new CaptureStatistics { CaptureStartTime = DateTime.Now };
        ProtocolStats.Clear();
        TopSourceIPs.Clear();
        TopDestIPs.Clear();
        TopPorts.Clear();
        TotalPackets = 0;
        PacketsPerSecond = 0;
        CaptureTime = "00:00:00";
        UniqueProtocols = 0;
        UniqueIPs = 0;
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

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        RefreshStatistics();
        _refreshTimer?.Start();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _refreshTimer?.Stop();
    }
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
