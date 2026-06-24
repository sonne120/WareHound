using Prism.Commands;
using Prism.Events;
using Prism.Regions;
using System.Collections.ObjectModel;
using System.Linq;
using WareHound.UI.Infrastructure.Events;
using WareHound.UI.Infrastructure.Services;
using WareHound.UI.Infrastructure.ViewModels;
using WareHound.UI.IPC;
using WareHound.UI.Models;
using WareHound.UI.Services;

namespace WareHound.UI.ViewModels;

public class StatisticsViewModel : BaseViewModel
{
    private readonly ISnifferService _snifferService;
    private readonly INativeStatisticsInterop? _nativeStats;
    private readonly IStatisticsChannel _statisticsChannel;

    private CaptureStatistics _statistics = new();
    private bool _isCapturing;
    private long _totalBytes;
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
    public ObservableCollection<TalkerInfo> TopSourceIPs { get; } = new();
    public ObservableCollection<TalkerInfo> TopDestIPs { get; } = new();
    public ObservableCollection<PortInfo> TopPorts { get; } = new();

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

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand ClearCommand { get; }

    public StatisticsViewModel(ISnifferService snifferService, IEventAggregator eventAggregator, ILoggerService logger, IStatisticsChannel statisticsChannel)
        : base(eventAggregator, logger)
    {
        _snifferService = snifferService ?? throw new ArgumentNullException(nameof(snifferService));
        _statisticsChannel = statisticsChannel ?? throw new ArgumentNullException(nameof(statisticsChannel));

        try
        {
            // Pass IntPtr.Zero so the native side resolves the active capture
            // pipeline via GetGlobalPipeline() (there is no Sniffer_Create handle).
            _nativeStats = new NativeStatisticsInterop(IntPtr.Zero);
        }
        catch
        {
            _nativeStats = null;
        }

        Subscribe<CaptureStateChangedEvent, bool>(OnCaptureStateChanged);

        RefreshCommand = new DelegateCommand(RefreshStatistics);
        ClearCommand = new DelegateCommand(ClearStatistics);

        IsCapturing = _snifferService.IsCapturing;

        _refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
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

    private void RefreshStatistics()
    {
        if (_nativeStats == null) return;

        try
        {
            var stats = _nativeStats.GetCaptureStatistics();
            if (stats.HasValue)
            {
                TotalPackets = (long)stats.Value.TotalPackets;
                _totalBytes = (long)stats.Value.TotalBytes;
                PacketsPerSecond = stats.Value.PacketsPerSecond;
                CaptureTime = TimeSpan.FromSeconds(stats.Value.CaptureDurationSeconds).ToString(@"hh\:mm\:ss");
                UniqueProtocols = stats.Value.UniqueProtocols;
                UniqueIPs = stats.Value.UniqueSourceIPs + stats.Value.UniqueDestIPs;
            }

            var protocols = _nativeStats.GetProtocolStats(10);
            var protocolList = protocols.Select(proto => new Models.ProtocolStats
            {
                Protocol = proto.ProtocolName,
                PacketCount = (long)proto.PacketCount,
                ByteCount = (long)proto.ByteCount,
                Percentage = proto.Percentage
            }).ToList();
            UpdateCollectionSimple(
                ProtocolStats,
                protocolList,
                p => p.Protocol,
                (old, @new) => old.PacketCount != @new.PacketCount || Math.Abs(old.Percentage - @new.Percentage) > 0.1);

            var srcIps = _nativeStats.GetTopSourceIPs(5);
            var srcIpList = srcIps.Select(ip => new TalkerInfo { IP = ip.IpAddress, PacketCount = (long)ip.PacketCount }).ToList();
            UpdateCollectionSimple(
                TopSourceIPs,
                srcIpList,
                ip => ip.IP,
                (old, @new) => old.PacketCount != @new.PacketCount);

            var dstIps = _nativeStats.GetTopDestIPs(5);
            var dstIpList = dstIps.Select(ip => new TalkerInfo { IP = ip.IpAddress, PacketCount = (long)ip.PacketCount }).ToList();
            UpdateCollectionSimple(
                TopDestIPs,
                dstIpList,
                ip => ip.IP,
                (old, @new) => old.PacketCount != @new.PacketCount);

            var ports = _nativeStats.GetTopPorts(5);
            var portList = ports.Select(port => new PortInfo
            {
                Port = port.Port,
                PacketCount = (long)port.PacketCount,
                ServiceName = port.ServiceName
            }).ToList();
            UpdateCollectionSimple(
                TopPorts,
                portList,
                p => p.Port,
                (old, @new) => old.PacketCount != @new.PacketCount);

            PublishStatisticsSnapshot();
        }
        catch (Exception ex)
        {
            LogError($"[StatisticsViewModel] Native statistics refresh failed: {ex.Message}", ex);
        }
    }

    private static void UpdateCollectionSimple<T, TKey>(
        ObservableCollection<T> collection,
        IReadOnlyList<T> newItems,
        Func<T, TKey> keySelector,
        Func<T, T, bool> hasChanged) where TKey : notnull
    {
        if (Math.Abs(collection.Count - newItems.Count) > 5 || collection.Count == 0)
        {
            collection.Clear();
            foreach (var item in newItems)
            {
                collection.Add(item);
            }
            return;
        }

        bool needsUpdate = collection.Count != newItems.Count;
        if (!needsUpdate)
        {
            for (int i = 0; i < collection.Count && !needsUpdate; i++)
            {
                if (!keySelector(collection[i]).Equals(keySelector(newItems[i])) ||
                    hasChanged(collection[i], newItems[i]))
                {
                    needsUpdate = true;
                }
            }
        }

        if (!needsUpdate) return;

        for (int i = 0; i < newItems.Count; i++)
        {
            if (i < collection.Count)
            {
                if (!keySelector(collection[i]).Equals(keySelector(newItems[i])) ||
                    hasChanged(collection[i], newItems[i]))
                {
                    collection[i] = newItems[i];
                }
            }
            else
            {
                collection.Add(newItems[i]);
            }
        }

        while (collection.Count > newItems.Count)
        {
            collection.RemoveAt(collection.Count - 1);
        }
    }

    private readonly Queue<double> _ppsHistory = new();
    private double _maxPps;

    private void PublishStatisticsSnapshot()
    {
        _ppsHistory.Enqueue(PacketsPerSecond);
        while (_ppsHistory.Count > 60) _ppsHistory.Dequeue();

        if (PacketsPerSecond > _maxPps) _maxPps = PacketsPerSecond;
        var avgPps = _ppsHistory.Count > 0 ? _ppsHistory.Average() : 0;

        var topTalkers = TopSourceIPs.Take(5).Select(t =>
        {
            var percentage = TotalPackets > 0 ? (double)t.PacketCount / TotalPackets * 100 : 0;
            return new TopTalkerItem(t.IP, t.PacketCount, percentage);
        }).ToList();

        var captureElapsed = IsCapturing
            ? DateTime.Now - Statistics.CaptureStartTime
            : Statistics.Duration;

        var snapshot = new StatisticsSnapshot
        {
            TotalPackets = TotalPackets,
            PacketsPerSecond = PacketsPerSecond,
            Timestamp = DateTime.Now,
            ProtocolStats = ProtocolStats.Select(p => new ProtocolStatItem(p.Protocol, p.PacketCount, p.Percentage)).ToList(),
            TotalBytes = _totalBytes,
            UniqueProtocols = UniqueProtocols,
            UniqueIps = UniqueIPs,
            CaptureElapsed = captureElapsed,
            TopTalkers = topTalkers,
            CurrentPps = PacketsPerSecond,
            AveragePps = avgPps,
            MaxPps = _maxPps
        };

        _statisticsChannel.Writer.TryWrite(snapshot);
    }

    private void ClearStatistics()
    {
        _nativeStats?.ClearStatistics();

        Statistics = new CaptureStatistics { CaptureStartTime = DateTime.Now };

        _ppsHistory.Clear();
        _maxPps = 0;
        _totalBytes = 0;

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

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        RefreshStatistics();
        _refreshTimer?.Start();
    }

    public override bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _refreshTimer?.Stop();
    }

    protected override void OnDispose()
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
