using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Services;

public interface IPcapFileService
{
    string BackendName { get; }

    Task SaveAsync(string filePath, IEnumerable<PacketInfo> packets, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    Task<IList<PacketInfo>> LoadAsync(string filePath, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    bool CanHandle(string filePath);
}
