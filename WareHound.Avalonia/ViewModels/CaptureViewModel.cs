using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using WareHound.Avalonia.Infrastructure.Collections;
using WareHound.Avalonia.Infrastructure.Events;
using WareHound.Avalonia.Infrastructure.Filters;
using WareHound.Avalonia.Models;
using WareHound.Avalonia.Services;

namespace WareHound.Avalonia.ViewModels;

public class CaptureViewModel : ViewModelBase
{
    private readonly ICaptureSessionFacade _captureFacade;
    private readonly ILoggerService _logger;
    private CancellationTokenSource? _captureCts;
    private IPacketFilter _currentFilter = new NoOpFilter();
    private const int MaxPacketCount = 10000;

    [Reactive] public PacketInfo? SelectedPacket { get; set; }
    [Reactive] public ObservableCollection<TreeNode> PacketDetails { get; set; } = new();
    [Reactive] public string PacketHexDump { get; set; } = "";
    [Reactive] public bool IsCapturing { get; set; }
    [Reactive] public int SelectedTabIndex { get; set; }
    [Reactive] public NetworkDevice? SelectedDevice { get; set; }
    [Reactive] public bool IsLoadingDevices { get; set; }
    [Reactive] public bool AutoScroll { get; set; } = true;

    public event Action<string, System.Collections.Generic.IEnumerable<PacketInfo>>? SaveToDashboardRequested;
    public event Action<IList<PacketInfo>>? PacketBatchReceived;

    public BulkObservableCollection<PacketInfo> Packets { get; } = new();
    public BulkObservableCollection<PacketInfo> FilteredPackets { get; } = new();
    public ObservableCollection<NetworkDevice> Devices => _captureFacade.Devices;

    public ReactiveCommand<Unit, Unit> RefreshDevicesCommand { get; }
    public ReactiveCommand<Unit, Unit> CopySourceIpCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyDestIpCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveToDashboardCommand { get; }

    public CaptureViewModel(ICaptureSessionFacade captureFacade, ILoggerService logger)
    {
        _captureFacade = captureFacade;
        _logger = logger;

        // Wire up facade events
        _captureFacade.ErrorOccurred += OnError;
        _captureFacade.DevicesLoaded += OnDevicesLoaded;
        _captureFacade.CaptureStateChanged += OnCaptureStateChanged;

        RefreshDevicesCommand = ReactiveCommand.CreateFromTask(LoadDevicesAsync);
        CopySourceIpCommand = ReactiveCommand.Create(CopySourceIp);
        CopyDestIpCommand = ReactiveCommand.Create(CopyDestIp);

        var hasPackets = this.WhenAnyValue(x => x.Packets.Count)
            .Select(count => count > 0);
        SaveToDashboardCommand = ReactiveCommand.Create(SaveToDashboard, hasPackets);

        this.WhenAnyValue(x => x.SelectedPacket)
            .Subscribe(_ => UpdatePacketDetails())
            .DisposeWith(Disposables);

        // Subscribe to events from other ViewModels
        SubscribeOnUIThread<AutoScrollChangedEvent>(e => AutoScroll = e.IsEnabled);
        SubscribeOnUIThread<FilterChangedEvent>(OnFilterChanged);
        SubscribeOnUIThread<ClearPacketsEvent>(_ => Clear());

        // Publish capture state changes
        this.WhenAnyValue(x => x.IsCapturing)
            .Subscribe(isCapturing => Publish(new CaptureStateChangedEvent(isCapturing)))
            .DisposeWith(Disposables);

        Packets.CollectionChanged += OnPacketsCollectionChanged;

        Task.Run(async () =>
        {
            await Task.Delay(100);
            await LoadDevicesAsync();
        });
    }

    private void OnFilterChanged(FilterChangedEvent e)
    {
        ApplyFilter(e.Criteria.Type, e.Criteria.Value);
    }

    private void OnPacketsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // For Reset events (bulk operations), refresh filtered packets
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            RefreshFilteredPackets();
        }
    }

    public void ApplyFilter(FilterType type, string value)
    {
        _currentFilter = FilterFactory.Create(type, value);
        RefreshFilteredPackets();
    }

    private void RefreshFilteredPackets()
    {
        var filtered = Packets.Where(p => _currentFilter.IsMatch(p)).ToList();
        FilteredPackets.ReplaceAll(filtered);
    }

    public async Task LoadDevicesAsync()
    {
        try
        {
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoadingDevices = true;
                ErrorMessage = "";
            });

            await _captureFacade.LoadDevicesAsync(TimeSpan.FromSeconds(30));

            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoadingDevices = false;
                if (Devices.Count > 0 && SelectedDevice == null)
                {
                    SelectedDevice = Devices[0];
                }
            });
        }
        catch (Exception ex)
        {
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorMessage = $"Failed to load devices: {ex.Message}";
                IsLoadingDevices = false;
            });
            _logger.LogError("Failed to load devices", ex);
        }
    }

    private void OnDevicesLoaded()
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsLoadingDevices = false;
            if (Devices.Count > 0 && SelectedDevice == null)
            {
                SelectedDevice = Devices[0];
            }
        });
    }

    private void OnCaptureStateChanged(bool isCapturing)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsCapturing = isCapturing;
        });
    }

    private void OnError(string error)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ErrorMessage = error;
            IsCapturing = false;
        });
    }

    public void StartCapture()
    {
        if (SelectedDevice == null)
        {
            ErrorMessage = "Please select a network interface.";
            return;
        }

        ErrorMessage = "";
        _captureFacade.SelectDevice(SelectedDevice.Index);
        _captureFacade.StartCapture();

        if (_captureFacade.IsCapturing)
        {
            IsCapturing = true;
            PacketInfo.SetCaptureStartTime(DateTime.Now);
            _captureCts = new CancellationTokenSource();
            _ = ConsumePacketsAsync(_captureCts.Token);
        }
    }

    public void StopCapture()
    {
        _captureCts?.Cancel();
        _captureFacade.StopCapture();
        IsCapturing = false;
    }

    public void Clear()
    {
        Packets.Clear();
        FilteredPackets.Clear();
        SelectedPacket = null;
        PacketDetails.Clear();
        PacketHexDump = "";
    }

    private async Task ConsumePacketsAsync(CancellationToken ct)
    {
        _logger.LogDebug("ConsumePacketsAsync started");
        int batchCount = 0;

        try
        {
            await foreach (var batch in _captureFacade.GetPacketBatchesAsync(ct))
            {
                batchCount++;
                if (batchCount <= 5 || batchCount % 10 == 0)
                {
                    _logger.LogDebug($"Received batch #{batchCount} with {batch.Count} packets");
                }

                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Packets.AddRange(batch);
                    
                    if (Packets.Count > MaxPacketCount)
                    {
                        Packets.TrimExcess(MaxPacketCount);
                    }

                    // Notify listeners about the batch (for statistics)
                    PacketBatchReceived?.Invoke(batch);
                });
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Packet consumer cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error consuming packets", ex);
        }

        _logger.LogDebug($"ConsumePacketsAsync ended, total batches: {batchCount}");
    }

    private void UpdatePacketDetails()
    {
        PacketDetails.Clear();
        PacketHexDump = "";

        if (SelectedPacket == null) return;

        var p = SelectedPacket;

        var frame = new TreeNode($"▶ Packet #{p.Number}: {p.Protocol}");
        frame.AddChild($"    Capture Time: {p.CaptureTime:yyyy-MM-dd HH:mm:ss.fff}");
        frame.AddChild($"    Packet ID: {p.Id}");
        PacketDetails.Add(frame);

        var eth = new TreeNode($"▶ Ethernet II, Src: {p.SourceMac}, Dst: {p.DestMac}");
        eth.AddChild($"    Source MAC: {p.SourceMac}");
        eth.AddChild($"    Destination MAC: {p.DestMac}");
        eth.AddChild($"    Type: IPv4 (0x0800)");
        PacketDetails.Add(eth);

        var ip = new TreeNode($"▶ Internet Protocol Version 4, Src: {p.SourceIp}, Dst: {p.DestIp}");
        ip.AddChild($"    Version: 4");
        ip.AddChild($"    Source Address: {p.SourceIp}");
        ip.AddChild($"    Destination Address: {p.DestIp}");
        ip.AddChild($"    Protocol: {p.Protocol}");
        PacketDetails.Add(ip);

        var proto = new TreeNode($"▶ {p.Protocol}, Src Port: {p.SourcePort}, Dst Port: {p.DestPort}");
        proto.AddChild($"    Source Port: {p.SourcePort}");
        proto.AddChild($"    Destination Port: {p.DestPort}");
        if (!string.IsNullOrEmpty(p.HostName))
            proto.AddChild($"    Host: {p.HostName}");
        PacketDetails.Add(proto);

        PacketHexDump = GenerateHexDump(p);
    }

    private string GenerateHexDump(PacketInfo p)
    {
        var bytes = p.RawData != null && p.RawData.Length > 0
            ? p.RawData
            : BuildPacketBytes(p);
        return FormatHexDump(bytes);
    }

    private byte[] BuildPacketBytes(PacketInfo p)
    {
        var bytes = new System.Collections.Generic.List<byte>();

        bytes.AddRange(ParseMacAddress(p.DestMac));
        bytes.AddRange(ParseMacAddress(p.SourceMac));
        bytes.Add(0x08); bytes.Add(0x00);

        bytes.Add(0x45);
        bytes.Add(0x00);
        bytes.Add(0x00); bytes.Add(0x40);
        bytes.Add((byte)((p.Id >> 8) & 0xFF));
        bytes.Add((byte)(p.Id & 0xFF));
        bytes.Add(0x40); bytes.Add(0x00);
        bytes.Add(0x40);
        bytes.Add(GetProtocolNumber(p.Protocol));
        bytes.Add(0x00); bytes.Add(0x00);
        bytes.AddRange(ParseIpAddress(p.SourceIp));
        bytes.AddRange(ParseIpAddress(p.DestIp));

        bytes.Add((byte)((p.SourcePort >> 8) & 0xFF));
        bytes.Add((byte)(p.SourcePort & 0xFF));
        bytes.Add((byte)((p.DestPort >> 8) & 0xFF));
        bytes.Add((byte)(p.DestPort & 0xFF));
        bytes.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

        bytes.AddRange(Encoding.ASCII.GetBytes($"Packet #{p.Number}"));

        while (bytes.Count < 64) bytes.Add(0x00);

        return bytes.ToArray();
    }

    private string FormatHexDump(byte[] bytes)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < bytes.Length; i += 16)
        {
            sb.Append($"{i:X8}  ");

            for (int j = 0; j < 8; j++)
                sb.Append(i + j < bytes.Length ? $"{bytes[i + j]:X2} " : "   ");

            sb.Append(" ");

            for (int j = 8; j < 16; j++)
                sb.Append(i + j < bytes.Length ? $"{bytes[i + j]:X2} " : "   ");

            sb.Append(" ");

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

    private void CopySourceIp()
    {
    }

    private void CopyDestIp()
    {
    }

    private void SaveToDashboard()
    {
        if (Packets.Count == 0) return;

        var name = $"Capture {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        SaveToDashboardRequested?.Invoke(name, Packets.ToList());
        _logger.Log($"Saved {Packets.Count} packets to dashboard as '{name}'");
    }
}
