using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Services;

public class CaptureSessionFacade : ICaptureSessionFacade
{
    private readonly ISnifferService _snifferService;
    private readonly PcapFileServiceFactory _pcapFactory;
    private readonly ILoggerService _logger;

    private NetworkDevice? _selectedDevice;
    private bool _isLoadingDevices;

    public ObservableCollection<NetworkDevice> Devices => _snifferService.Devices;
    public NetworkDevice? SelectedDevice => _selectedDevice;
    public bool IsLoadingDevices => _isLoadingDevices;
    public bool IsCapturing => _snifferService.IsCapturing;

    public event Action<bool>? CaptureStateChanged;
    public event Action<string>? ErrorOccurred;
    public event Action? DevicesLoaded;

    public CaptureSessionFacade(
        ISnifferService snifferService,
        PcapFileServiceFactory pcapFactory,
        ILoggerService logger)
    {
        _snifferService = snifferService ?? throw new ArgumentNullException(nameof(snifferService));
        _pcapFactory = pcapFactory ?? throw new ArgumentNullException(nameof(pcapFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _snifferService.ErrorOccurred += OnSnifferError;
        _snifferService.DevicesLoaded += OnSnifferDevicesLoaded;
    }

    private void OnSnifferError(string error)
    {
        _logger.LogError($"Sniffer error: {error}");
        ErrorOccurred?.Invoke(error);
    }

    private void OnSnifferDevicesLoaded()
    {
        _isLoadingDevices = false;
        DevicesLoaded?.Invoke();
    }

    public async Task LoadDevicesAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        _isLoadingDevices = true;
        _logger.Log("Loading network devices...");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            await _snifferService.LoadDevicesAsync(cts.Token);

            if (Devices.Count > 0 && _selectedDevice == null)
            {
                SelectDevice(0);
            }

            _logger.Log($"Loaded {Devices.Count} network devices");
            DevicesLoaded?.Invoke();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Device loading was cancelled");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Device loading timed out");
            ErrorOccurred?.Invoke("Device loading timed out. Please retry.");
            throw new TimeoutException("Device loading timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load devices: {ex.Message}", ex);
            ErrorOccurred?.Invoke($"Failed to load devices: {ex.Message}");
            throw;
        }
        finally
        {
            _isLoadingDevices = false;
        }
    }

    public void SelectDevice(int deviceIndex)
    {
        if (deviceIndex >= 0 && deviceIndex < Devices.Count)
        {
            _selectedDevice = Devices[deviceIndex];
            _snifferService.SelectDevice(deviceIndex);
            _logger.LogDebug($"Selected device: {_selectedDevice.DisplayName}");
        }
        else
        {
            _logger.LogWarning($"Invalid device index: {deviceIndex}");
        }
    }

    public void StartCapture()
    {
        if (_selectedDevice == null)
        {
            _logger.LogWarning("Cannot start capture: no device selected");
            ErrorOccurred?.Invoke("Please select a network device first");
            return;
        }

        _logger.Log($"Starting capture on {_selectedDevice.DisplayName}");
        _snifferService.StartCapture();

        if (_snifferService.IsCapturing)
        {
            CaptureStateChanged?.Invoke(true);
        }
        else
        {
            ErrorOccurred?.Invoke("Failed to start capture. Try running with elevated privileges.");
        }
    }

    public void StopCapture()
    {
        _logger.Log("Stopping capture");
        _snifferService.StopCapture();
        CaptureStateChanged?.Invoke(false);
    }

    public IAsyncEnumerable<IList<PacketInfo>> GetPacketBatchesAsync(CancellationToken ct)
    {
        return _snifferService.GetPacketBatchesAsync(ct);
    }

    public async Task<IList<PacketInfo>> LoadPcapAsync(
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        _logger.Log($"Loading PCAP file: {filePath}");

        try
        {
            var service = _pcapFactory.GetService();
            var packets = await service.LoadAsync(filePath, progress, ct);
            _logger.Log($"Loaded {packets.Count} packets from PCAP");
            return packets;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load PCAP: {ex.Message}", ex);
            ErrorOccurred?.Invoke($"Failed to load PCAP: {ex.Message}");
            throw;
        }
    }

    public async Task SavePcapAsync(
        string filePath,
        IList<PacketInfo> packets,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        _logger.Log($"Saving {packets.Count} packets to: {filePath}");

        try
        {
            var service = _pcapFactory.GetService();
            await service.SaveAsync(filePath, packets, progress, ct);
            _logger.Log($"Saved {packets.Count} packets to PCAP");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save PCAP: {ex.Message}", ex);
            ErrorOccurred?.Invoke($"Failed to save PCAP: {ex.Message}");
            throw;
        }
    }
}
