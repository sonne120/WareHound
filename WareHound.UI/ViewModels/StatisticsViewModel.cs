using System.Collections.Concurrent;
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
    
    private readonly ConcurrentDictionary<string, long> _protocolCounts = new();
    private readonly ConcurrentDictionary<string, long> _sourceIpCounts = new();
    private readonly ConcurrentDictionary<string, long> _destIpCounts = new();
    private readonly ConcurrentDictionary<int, long> _destPortCounts = new();
    private long _packetCount;
    private CancellationTokenSource? _statsCts;
    private bool _isRefreshing;

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
            _ = RefreshFromManagedAsync();
        }
    }
    
    private async Task RefreshFromManagedAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        
        try
        {
            _statsCts?.Cancel();
            _statsCts = new CancellationTokenSource();
            var ct = _statsCts.Token;
            
            // Run aggregation on thread pool 
            var result = await Task.Run(() => ComputeStatisticsParallel(ct), ct);
            
            if (ct.IsCancellationRequested) return;
            
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TotalPackets = result.TotalPackets;
                PacketsPerSecond = Statistics.PacketsPerSecond;
                CaptureTime = Statistics.Duration.ToString(@"hh\:mm\:ss");
                UniqueProtocols = result.ProtocolStats.Count;
                UniqueIPs = result.UniqueIPs;
                
                ProtocolStats.Clear();
                foreach (var stat in result.ProtocolStats)
                {
                    ProtocolStats.Add(stat);
                }
                
                TopSourceIPs.Clear();
                foreach (var ip in result.TopSourceIPs)
                {
                    TopSourceIPs.Add(ip);
                }
                
                TopDestIPs.Clear();
                foreach (var ip in result.TopDestIPs)
                {
                    TopDestIPs.Add(ip);
                }
                
                TopPorts.Clear();
                foreach (var port in result.TopPorts)
                {
                    TopPorts.Add(port);
                }
            });
        }
        catch (OperationCanceledException)
        {
           
        }
        finally
        {
            _isRefreshing = false;
        }
    }
    
    private StatisticsResult ComputeStatisticsParallel(CancellationToken ct)
    {
        var totalPackets = Interlocked.Read(ref _packetCount);
        var divisor = totalPackets > 0 ? totalPackets : 1;
        
        // Parallel top protocol
        var protocolStats = _protocolCounts
            .AsParallel()
            .WithCancellation(ct)
            .OrderByDescending(x => x.Value)
            .Take(10)
            .Select(kvp => new ProtocolStats
            {
                Protocol = kvp.Key,
                PacketCount = kvp.Value,
                Percentage = (double)kvp.Value / divisor * 100
            })
            .ToList();
        
        // Parallel top source IPs
        var topSourceIPs = _sourceIpCounts
            .AsParallel()
            .WithCancellation(ct)
            .OrderByDescending(x => x.Value)
            .Take(5)
            .Select(kvp => new TalkerInfo { IP = kvp.Key, PacketCount = kvp.Value })
            .ToList();
        
        // Parallel top destination IPs
        var topDestIPs = _destIpCounts
            .AsParallel()
            .WithCancellation(ct)
            .OrderByDescending(x => x.Value)
            .Take(5)
            .Select(kvp => new TalkerInfo { IP = kvp.Key, PacketCount = kvp.Value })
            .ToList();
        
        // Parallel top ports
        var topPorts = _destPortCounts
            .AsParallel()
            .WithCancellation(ct)
            .OrderByDescending(x => x.Value)
            .Take(5)
            .Select(kvp => new PortInfo 
            { 
                Port = kvp.Key, 
                PacketCount = kvp.Value, 
                ServiceName = GetServiceName(kvp.Key) 
            })
            .ToList();
        
        // Compute unique IPs in parallel
        var uniqueIPs = _sourceIpCounts.Keys
            .AsParallel()
            .WithCancellation(ct)
            .Union(_destIpCounts.Keys.AsParallel())
            .Count();
        
        return new StatisticsResult
        {
            TotalPackets = totalPackets,
            ProtocolStats = protocolStats,
            TopSourceIPs = topSourceIPs,
            TopDestIPs = topDestIPs,
            TopPorts = topPorts,
            UniqueIPs = uniqueIPs
        };
    }
    private class StatisticsResult
    {
        public long TotalPackets { get; init; }
        public List<ProtocolStats> ProtocolStats { get; init; } = new();
        public List<TalkerInfo> TopSourceIPs { get; init; } = new();
        public List<TalkerInfo> TopDestIPs { get; init; } = new();
        public List<PortInfo> TopPorts { get; init; } = new();
        public int UniqueIPs { get; init; }
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
            RefreshFromManaged();
        }
    }

    private void RefreshFromManaged()
    {
        _ = RefreshFromManagedAsync();
    }

    private void ClearStatistics()
    {
        Statistics = new CaptureStatistics { CaptureStartTime = DateTime.Now };
        
        // Clear thread-safe collections
        _protocolCounts.Clear();
        _sourceIpCounts.Clear();
        _destIpCounts.Clear();
        _destPortCounts.Clear();
        Interlocked.Exchange(ref _packetCount, 0);
        
        // Clear observable collections
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
