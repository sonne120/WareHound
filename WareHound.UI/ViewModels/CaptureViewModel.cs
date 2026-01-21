using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Events;
using WareHound.UI.Infrastructure.Events;
using WareHound.UI.Infrastructure.ViewModels;
using WareHound.UI.Models;
using WareHound.UI.Services;
using WareHound.UI.Infrastructure.Services;

namespace WareHound.UI.ViewModels
{
    public class CaptureViewModel : BaseViewModel
    {
        private readonly ISnifferService _snifferService;
        private readonly IPacketCollectionService _collectionService;
        private readonly ILoggerService _logger;

        private NetworkDevice? _selectedDevice;
        private string _filterText = "";
        private bool _isCapturing;
        private PacketInfo? _selectedPacket;
        private ObservableCollection<TreeNode> _packetDetails = new();
        private string _packetHexDump = "";
        private bool _autoScroll = true;
        private bool _showMacAddresses = true;

        private CancellationTokenSource? _captureCts;
        private bool _hasPackets;

        public ObservableCollection<PacketInfo> Packets { get; } = new();
        public ObservableCollection<NetworkDevice> Devices => _snifferService.Devices;

        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        public bool ShowMacAddresses
        {
            get => _showMacAddresses;
            set => SetProperty(ref _showMacAddresses, value);
        }

        public NetworkDevice? SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
        }

        public string FilterText
        {
            get => _filterText;
            set => SetProperty(ref _filterText, value);
        }

        public bool IsCapturing
        {
            get => _isCapturing;
            set => SetProperty(ref _isCapturing, value);
        }

        public PacketInfo? SelectedPacket
        {
            get => _selectedPacket;
            set
            {
                if (SetProperty(ref _selectedPacket, value))
                {
                    UpdatePacketDetails();
                }
            }
        }
        public ObservableCollection<TreeNode> PacketDetails
        {
            get => _packetDetails;
            set => SetProperty(ref _packetDetails, value);
        }
        public string PacketHexDump
        {
            get => _packetHexDump;
            set => SetProperty(ref _packetHexDump, value);
        }

        public DelegateCommand ToggleCaptureCommand { get; }
        public DelegateCommand ClearCommand { get; }
        public DelegateCommand SaveToDashboardCommand { get; }
        public DelegateCommand<string> CopyCommand { get; }

        public CaptureViewModel(ISnifferService snifferService, IPacketCollectionService collectionService, IEventAggregator eventAggregator, ILoggerService logger)
            : base(eventAggregator, logger)
        {
            _snifferService = snifferService ?? throw new ArgumentNullException(nameof(snifferService));
            _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            Subscribe<CaptureStateChangedEvent, bool>(OnCaptureStateChanged);
            Subscribe<ClearPacketsEvent>(Clear);
            Subscribe<FilterChangedEvent, string>(filter => FilterText = filter);
            Subscribe<AutoScrollChangedEvent, bool>(enabled => AutoScroll = enabled);
            Subscribe<ShowMacAddressesChangedEvent, bool>(enabled => ShowMacAddresses = enabled);
            Subscribe<TimeFormatChangedEvent, TimeFormatType>(OnTimeFormatChanged);
            Subscribe<DevicesLoadedEvent>(OnDevicesLoaded);

            ToggleCaptureCommand = new DelegateCommand(ToggleCapture);
            ClearCommand = new DelegateCommand(Clear);
            SaveToDashboardCommand = new DelegateCommand(SaveToDashboard, () => Packets.Count > 0);
            CopyCommand = new DelegateCommand<string>(Copy);
            
            if (_snifferService.IsCapturing)
            {
               OnCaptureStateChanged(true);
            }

            _snifferService.ErrorOccurred += OnError;

            // Select first device if already loaded (for late navigation)
            if (Devices.Count > 0 && SelectedDevice == null)
                SelectedDevice = Devices[0];
        }

        private void OnDevicesLoaded()
        {
            // Select first device when devices finish loading
            if (Devices.Count > 0 && SelectedDevice == null)
            {
                SelectedDevice = Devices[0];
            }
        }

        private void OnTimeFormatChanged(TimeFormatType format)
        {
            PacketInfo.SetTimeFormat(format);
            foreach (var packet in Packets)
            {
                packet.NotifyTimeDisplayChanged();
            }
        }

        private void OnCaptureStateChanged(bool isCapturing)
        {
            if (isCapturing)
            {
                if (!IsCapturing && _snifferService.IsCapturing)
                {
                    IsCapturing = true;
                    _captureCts = new CancellationTokenSource();
                    _ = ConsumePacketsAsync(_captureCts.Token);
                }
            }
            else
            {
                if (IsCapturing)
                {
                   _captureCts?.Cancel();
                   IsCapturing = false;
                }
            }
        }

        private void ToggleCapture()
        {
            if (IsCapturing)
            {
                // 1. Cancel the packet consumer task (background loop)
                _captureCts?.Cancel();
                
                // 2. Stop the underlying sniffer service (closes pipes, native threads)
                _snifferService.StopCapture();
                
                // 3. Update UI state (enables/disables buttons)
                IsCapturing = false;
            }
            else
            {
                // 1. Validation: Ensure a device is selected
                if (SelectedDevice == null)
                {
                    MessageBox.Show("Please select a network interface.", "WareHound",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Start the service. 
                _snifferService.StartCapture(SelectedDevice.Index);
             
                if (_snifferService.IsCapturing)
                {
                    IsCapturing = true;
                    
                    _captureCts = new CancellationTokenSource();
                    
                    // 5. Fire-and-forget the packet consumer loop                  
                    _ = ConsumePacketsAsync(_captureCts.Token);
                }
                else
                {                 
                    IsCapturing = false; 
                }
            }
        }

        private void Clear()
        {
            Packets.Clear();
            SelectedPacket = null;
            _hasPackets = false;
            SaveToDashboardCommand.RaiseCanExecuteChanged();
        }

        private void SaveToDashboard()
        {
            if (Packets.Count == 0) return;

            var name = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}";
            _collectionService.CreateCollection(name, Packets);
            MessageBox.Show($"Saved {Packets.Count} packets to: {name}", "WareHound",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Copy(string field)
        {
            if (SelectedPacket == null) return;

            var text = field switch
            {
                "SourceIp" => SelectedPacket.SourceIp,
                "DestIp" => SelectedPacket.DestIp,
                _ => ""
            };

            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        private async Task ConsumePacketsAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var batch in _snifferService.GetPacketBatchesAsync(ct))
                {
                    await FlushBatchToUIAsync(batch);
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError($"[ConsumePacketsAsync] Error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ConsumePacketsAsync] Error: {ex.Message}", ex);
            }
        }

        private async Task FlushBatchToUIAsync(IList<PacketInfo> batch)
        {
            var packetsToAdd = batch.ToArray(); 

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var p in packetsToAdd)
                {
                    Packets.Add(p);               
                    Publish<PacketCapturedEvent, PacketInfo>(p);
                }

                if (!_hasPackets && Packets.Count > 0)
                {
                    _hasPackets = true;
                    SaveToDashboardCommand.RaiseCanExecuteChanged();
                }
            }, DispatcherPriority.Background);
        }

        private void OnError(string error)
        {
            BeginOnUI(() =>
            {
                MessageBox.Show(error, "WareHound Error", MessageBoxButton.OK, MessageBoxImage.Error);
                IsCapturing = false;
            });
        }

        private void UpdatePacketDetails()
        {
            PacketDetails.Clear();
            PacketHexDump = "";

            if (SelectedPacket == null) return;

            var p = SelectedPacket;

            // Frame info
            var frame = new TreeNode($"▶ Packet #{p.Number}: {p.Protocol}");
            frame.AddChild($"    Capture Time: {p.CaptureTime:yyyy-MM-dd HH:mm:ss.fff}");
            frame.AddChild($"    Packet ID: {p.Id}");
            PacketDetails.Add(frame);

            // Ethernet
            var eth = new TreeNode($"▶ Ethernet II, Src: {p.SourceMac}, Dst: {p.DestMac}");
            eth.AddChild($"    Source MAC: {p.SourceMac}");
            eth.AddChild($"    Destination MAC: {p.DestMac}");
            eth.AddChild($"    Type: IPv4 (0x0800)");
            PacketDetails.Add(eth);

            // IP
            var ip = new TreeNode($"▶ Internet Protocol Version 4, Src: {p.SourceIp}, Dst: {p.DestIp}");
            ip.AddChild($"    Version: 4");
            ip.AddChild($"    Source Address: {p.SourceIp}");
            ip.AddChild($"    Destination Address: {p.DestIp}");
            ip.AddChild($"    Protocol: {p.Protocol}");
            PacketDetails.Add(ip);

            // Protocol specific
            var proto = new TreeNode($"▶ {p.Protocol}, Src Port: {p.SourcePort}, Dst Port: {p.DestPort}");
            proto.AddChild($"    Source Port: {p.SourcePort}");
            proto.AddChild($"    Destination Port: {p.DestPort}");
            if (!string.IsNullOrEmpty(p.HostName))
                proto.AddChild($"    Host: {p.HostName}");
            PacketDetails.Add(proto);

            // Generate hex dump
            PacketHexDump = GenerateHexDump(p);
        }

        private string GenerateHexDump(PacketInfo p)
        {
            var packetBytes = BuildPacketBytes(p);
            return FormatHexDump(packetBytes);
        }

        private byte[] BuildPacketBytes(PacketInfo p)
        {
            var bytes = new List<byte>();

            // Ethernet Header (14 bytes)
            bytes.AddRange(ParseMacAddress(p.DestMac));
            bytes.AddRange(ParseMacAddress(p.SourceMac));
            bytes.Add(0x08); bytes.Add(0x00); // IPv4

            // IP Header (20 bytes)
            bytes.Add(0x45); // Version + IHL
            bytes.Add(0x00); // DSCP/ECN
            bytes.Add(0x00); bytes.Add(0x40); // Total Length
            bytes.Add((byte)((p.Id >> 8) & 0xFF));
            bytes.Add((byte)(p.Id & 0xFF));
            bytes.Add(0x40); bytes.Add(0x00); // Flags + Fragment
            bytes.Add(0x40); // TTL
            bytes.Add(GetProtocolNumber(p.Protocol));
            bytes.Add(0x00); bytes.Add(0x00); // Checksum
            bytes.AddRange(ParseIpAddress(p.SourceIp));
            bytes.AddRange(ParseIpAddress(p.DestIp));

            // Transport Header (8 bytes)
            bytes.Add((byte)((p.SourcePort >> 8) & 0xFF));
            bytes.Add((byte)(p.SourcePort & 0xFF));
            bytes.Add((byte)((p.DestPort >> 8) & 0xFF));
            bytes.Add((byte)(p.DestPort & 0xFF));
            bytes.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

            // Payload
            bytes.AddRange(Encoding.ASCII.GetBytes($"Packet #{p.Number}"));

            // Pad to 64 bytes
            while (bytes.Count < 64) bytes.Add(0x00);

            return bytes.ToArray();
        }

        private string FormatHexDump(byte[] bytes)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < bytes.Length; i += 16)
            {
                sb.Append($"{i:X8}  ");

                // First 8 hex bytes
                for (int j = 0; j < 8; j++)
                    sb.Append(i + j < bytes.Length ? $"{bytes[i + j]:X2} " : "   ");

                sb.Append(" ");

                // Second 8 hex bytes
                for (int j = 8; j < 16; j++)
                    sb.Append(i + j < bytes.Length ? $"{bytes[i + j]:X2} " : "   ");

                sb.Append(" ");

                // ASCII
                for (int j = 0; j < 16 && i + j < bytes.Length; j++)
                {
                    byte b = bytes[i + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static byte[] ParseMacAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac))
                return new byte[6];

            try
            {
                var parts = mac.Replace("-", ":").Split(':');
                if (parts.Length == 6)
                    return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
            }
            catch { }

            return new byte[6];
        }

        private static byte[] ParseIpAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return new byte[4];

            try
            {
                var parts = ip.Split('.');
                if (parts.Length == 4)
                    return parts.Select(byte.Parse).ToArray();
            }
            catch { }

            return new byte[4];
        }

        private static byte GetProtocolNumber(string protocol) => protocol?.ToUpperInvariant() switch
        {
            "TCP" => 6,
            "UDP" => 17,
            "ICMP" => 1,
            _ => 0
        };

        protected override void OnDispose()
        {
            _captureCts?.Cancel();
            _captureCts?.Dispose();

            _snifferService.ErrorOccurred -= OnError;
        }
    }
}
