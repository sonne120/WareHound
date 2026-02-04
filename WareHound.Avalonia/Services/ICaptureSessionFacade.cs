using System.Collections.ObjectModel;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Services;

public interface ICaptureSessionFacade
{
    ObservableCollection<NetworkDevice> Devices { get; }
    
    NetworkDevice? SelectedDevice { get; }
    
    bool IsLoadingDevices { get; }
    
    bool IsCapturing { get; }
    
    Task LoadDevicesAsync(TimeSpan timeout, CancellationToken ct = default);
    
    void SelectDevice(int deviceIndex);
    
    void StartCapture();
    
    void StopCapture();
    
    IAsyncEnumerable<IList<PacketInfo>> GetPacketBatchesAsync(CancellationToken ct);
    
    Task<IList<PacketInfo>> LoadPcapAsync(string filePath, IProgress<int>? progress = null, CancellationToken ct = default);
    
    Task SavePcapAsync(string filePath, IList<PacketInfo> packets, IProgress<int>? progress = null, CancellationToken ct = default);
    
    event Action<bool>? CaptureStateChanged;
    
    event Action<string>? ErrorOccurred;
    
    event Action? DevicesLoaded;
}
