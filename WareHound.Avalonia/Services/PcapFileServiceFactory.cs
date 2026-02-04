using System.Reactive.Disposables;
using ReactiveUI;
using WareHound.Avalonia.Infrastructure.Events;

namespace WareHound.Avalonia.Services;

/// <summary>
/// Factory for selecting between native (C++) and managed (SharpPcap) PCAP file services.
/// Subscribes to backend change events to switch implementations at runtime.
/// </summary>
public class PcapFileServiceFactory : IDisposable
{
    private readonly SharpPcapFileService _sharpPcapService;
    private readonly CompositeDisposable _disposables = new();
    private PcapBackend _currentBackend = PcapBackend.Native;
    private bool _disposed;

    public PcapFileServiceFactory(SharpPcapFileService sharpPcapService)
    {
        _sharpPcapService = sharpPcapService ?? throw new ArgumentNullException(nameof(sharpPcapService));

        // Subscribe to backend change events
        MessageBus.Current.Listen<PcapBackendChangedEvent>()
            .Subscribe(e => _currentBackend = e.Backend)
            .DisposeWith(_disposables);
    }

    /// <summary>
    /// Gets the current backend type.
    /// </summary>
    public PcapBackend CurrentBackend => _currentBackend;

    /// <summary>
    /// Gets the appropriate PCAP file service based on current backend setting.
    /// </summary>
    public IPcapFileService GetService()
    {
        return _currentBackend switch
        {
            PcapBackend.Native => _sharpPcapService, // TODO: Add native service when implemented
            PcapBackend.SharpPcap => _sharpPcapService,
            _ => _sharpPcapService
        };
    }

    /// <summary>
    /// Sets the backend type programmatically.
    /// </summary>
    public void SetBackend(PcapBackend backend)
    {
        _currentBackend = backend;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposables.Dispose();
    }
}
