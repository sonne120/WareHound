using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using WareHound.UI.Models;

namespace WareHound.UI.Services
{
    public interface ISnifferService : IDisposable
    {
        ObservableCollection<NetworkDevice> Devices { get; }
        bool IsCapturing { get; }
        int SelectedDeviceIndex { get; }
       
        event Action<string>? ErrorOccurred;
        
        void LoadDevices();
        void SelectDevice(int deviceIndex);
        void StartCapture();
        void StartCapture(int deviceIndex);
        void StopCapture();
        
        IntPtr GetSnifferHandle();
        
        IAsyncEnumerable<IList<PacketInfo>> GetPacketBatchesAsync(CancellationToken ct = default);
    }
}
