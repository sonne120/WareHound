using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using WareHound.UI.IPC;
using WareHound.UI.Services;

namespace WareHound.UI.ViewModels
{
    public class StatisticsStatusBarViewModel : INotifyPropertyChanged
    {
        private INativeStatisticsInterop? _nativeStats;

        private readonly DispatcherTimer _updateTimer;
        private readonly double[] _packetsData = new double[60];

        public event EventHandler<double[]>? ChartUpdateRequested;


        private string _totalPackets = "0";
        public string TotalPackets
        {
            get => _totalPackets;
            set { _totalPackets = value; OnPropertyChanged(); }
        }

        private string _packetsPerSecond = "0.0";
        public string PacketsPerSecond
        {
            get => _packetsPerSecond;
            set { _packetsPerSecond = value; OnPropertyChanged(); }
        }

        private string _totalDataSize = "0 B";
        public string TotalDataSize
        {
            get => _totalDataSize;
            set { _totalDataSize = value; OnPropertyChanged(); }
        }

        private string _captureTime = "00:00:00";
        public string CaptureTime
        {
            get => _captureTime;
            set { _captureTime = value; OnPropertyChanged(); }
        }

        private int _currentPPS;
        public int CurrentPPS
        {
            get => _currentPPS;
            set { _currentPPS = value; OnPropertyChanged(); }
        }

        private int _avgPPS;
        public int AvgPPS
        {
            get => _avgPPS;
            set { _avgPPS = value; OnPropertyChanged(); }
        }

        private int _maxPPS;
        public int MaxPPS
        {
            get => _maxPPS;
            set { _maxPPS = value; OnPropertyChanged(); }
        }

        private string _topProtocolName = "TLS";
        public string TopProtocolName
        {
            get => _topProtocolName;
            set { _topProtocolName = value; OnPropertyChanged(); }
        }

        private string _topProtocolPercent = "0%";
        public string TopProtocolPercent
        {
            get => _topProtocolPercent;
            set { _topProtocolPercent = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ProtocolInfo> Protocols { get; } = new();
        public ObservableCollection<MiniProtocolBar> MiniProtocolBars { get; } = new();
        public ObservableCollection<TopTalkerInfo> TopTalkers { get; } = new();

        private long _lastTotalPackets;

        private readonly Dictionary<string, string> _protocolColors = new()
        {
            { "TLS", "#3B82F6" }, { "TCP", "#10B981" }, { "UDP", "#F59E0B" },
            { "HTTP", "#EF4444" }, { "DNS", "#8B5CF6" }, { "QUIC", "#EC4899" },
            { "mDNS", "#14B8A6" }, { "SSDP", "#F97316" }, { "ARP", "#06B6D4" },
            { "ICMP", "#84CC16" }, { "Other", "#6B7280" }
        };


        public StatisticsStatusBarViewModel(ISnifferService? snifferService = null)
        {
            Array.Clear(_packetsData, 0, _packetsData.Length);

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        public void StartUpdating()
        {
            _lastTotalPackets = 0;
            Array.Clear(_packetsData, 0, _packetsData.Length);
            _updateTimer.Start();
        }

        public void StopUpdating()
        {
            _updateTimer.Stop();
        }

        public void Reset()
        {
            _lastTotalPackets = 0;
            Array.Clear(_packetsData, 0, _packetsData.Length);

            Protocols.Clear();
            MiniProtocolBars.Clear();
            TopTalkers.Clear();

            TotalPackets = "0";
            PacketsPerSecond = "0.0";
            TotalDataSize = "0 B";
            CaptureTime = "00:00:00";
            CurrentPPS = 0;
            AvgPPS = 0;
            MaxPPS = 0;
            TopProtocolName = "TLS";
            TopProtocolPercent = "0%";

            ChartUpdateRequested?.Invoke(this, _packetsData);
        }

        private INativeStatisticsInterop? EnsureNativeStats()
        {
            // Pass IntPtr.Zero so the native side resolves the active capture
            // pipeline via GetGlobalPipeline() (there is no Sniffer_Create handle).
            _nativeStats ??= new NativeStatisticsInterop(IntPtr.Zero);
            return _nativeStats;
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            var native = EnsureNativeStats();
            if (native == null) return;

            NativeCaptureStatistics? capture;
            try
            {
                capture = native.GetCaptureStatistics();
            }
            catch
            {
                return;
            }
            if (!capture.HasValue) return;

            long total = (long)capture.Value.TotalPackets;
            long perSecond = Math.Max(0, total - _lastTotalPackets);
            _lastTotalPackets = total;

            for (int i = 0; i < _packetsData.Length - 1; i++)
                _packetsData[i] = _packetsData[i + 1];
            _packetsData[^1] = perSecond;

            CurrentPPS = (int)perSecond;
            TotalPackets = total.ToString("N0");
            PacketsPerSecond = perSecond.ToString("F1");
            TotalDataSize = FormatBytes((long)capture.Value.TotalBytes);
            CaptureTime = TimeSpan.FromSeconds(capture.Value.CaptureDurationSeconds).ToString(@"hh\:mm\:ss");

            var nonZeroData = _packetsData.Where(x => x > 0).ToArray();
            if (nonZeroData.Length > 0)
            {
                AvgPPS = (int)Math.Round(nonZeroData.Average());
                MaxPPS = (int)nonZeroData.Max();
            }
            else
            {
                AvgPPS = 0;
                MaxPPS = 0;
            }

            UpdateProtocolsDisplay(native, total);
            UpdateTopTalkersDisplay(native);

            ChartUpdateRequested?.Invoke(this, (double[])_packetsData.Clone());
        }

        private string NormalizeProtocol(string? protocol)
        {
            if (string.IsNullOrEmpty(protocol))
                return "Other";

            return protocol.ToUpperInvariant() switch
            {
                "TLS" or "SSL" or "HTTPS" => "TLS",
                "TCP" => "TCP",
                "UDP" => "UDP",
                "HTTP" => "HTTP",
                "DNS" => "DNS",
                "QUIC" => "QUIC",
                "MDNS" => "mDNS",
                "SSDP" or "SSCOPMCE" => "SSDP",
                "ARP" => "ARP",
                "ICMP" or "ICMPV6" => "ICMP",
                _ => "Other"
            };
        }

        private void UpdateProtocolsDisplay(INativeStatisticsInterop native, long totalPackets)
        {
            if (totalPackets == 0) return;

            NativeProtocolStats[] protocols;
            try
            {
                protocols = native.GetProtocolStats(6);
            }
            catch
            {
                return;
            }
            if (protocols.Length == 0) return;

            var top = protocols[0];
            TopProtocolName = top.ProtocolName;
            TopProtocolPercent = $"{top.Percentage:F1}%";

            Protocols.Clear();
            MiniProtocolBars.Clear();

            double miniBarTotalWidth = 120;

            foreach (var proto in protocols)
            {
                string colorKey = NormalizeProtocol(proto.ProtocolName);
                string colorHex = _protocolColors.TryGetValue(colorKey, out var c) ? c : "#6B7280";
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));

                Protocols.Add(new ProtocolInfo
                {
                    Name = proto.ProtocolName,
                    Percent = proto.Percentage,
                    PacketCount = (long)proto.PacketCount,
                    Color = brush
                });

                MiniProtocolBars.Add(new MiniProtocolBar
                {
                    Width = Math.Max(proto.Percentage / 100.0 * miniBarTotalWidth, 1),
                    Color = brush
                });
            }
        }

        private void UpdateTopTalkersDisplay(INativeStatisticsInterop native)
        {
            NativeTalkerStats[] talkers;
            try
            {
                talkers = native.GetTopSourceIPs(5);
            }
            catch
            {
                return;
            }
            if (talkers.Length == 0) return;

            long maxCount = (long)talkers[0].PacketCount;
            if (maxCount == 0) maxCount = 1;
            double barMaxWidth = 160;

            long totalForPercent = talkers.Sum(t => (long)t.PacketCount);
            if (totalForPercent == 0) totalForPercent = 1;

            TopTalkers.Clear();

            foreach (var t in talkers)
            {
                long packets = (long)t.PacketCount;
                TopTalkers.Add(new TopTalkerInfo
                {
                    IpAddress = t.IpAddress,
                    PacketCount = (int)packets,
                    Percent = (double)packets / totalForPercent * 100,
                    BarWidth = (double)packets / maxCount * barMaxWidth
                });
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F1} {sizes[order]}";
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProtocolInfo
    {
        public string Name { get; set; } = "";
        public double Percent { get; set; }
        public long PacketCount { get; set; }
        public SolidColorBrush Color { get; set; } = Brushes.Gray;
    }

    public class MiniProtocolBar
    {
        public double Width { get; set; }
        public SolidColorBrush Color { get; set; } = Brushes.Gray;
    }

    public class TopTalkerInfo
    {
        public string IpAddress { get; set; } = "";
        public int PacketCount { get; set; }
        public double Percent { get; set; }
        public double BarWidth { get; set; }
    }
}
