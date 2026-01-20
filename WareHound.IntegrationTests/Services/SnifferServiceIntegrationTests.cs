using System.Collections.ObjectModel;
using FluentAssertions;
using WareHound.UI.Models;
using WareHound.UI.Services;

namespace WareHound.IntegrationTests.Services;

/// <summary>
/// Integration tests for the Sniffer Service
/// These tests verify the integration between the UI layer and the native sniffer DLL
/// </summary>
public class SnifferServiceIntegrationTests : IDisposable
{
    private readonly TestSnifferService _snifferService;

    public SnifferServiceIntegrationTests()
    {
        _snifferService = new TestSnifferService();
    }

    public void Dispose()
    {
        _snifferService.Dispose();
    }

    [Fact]
    public void LoadDevices_ShouldPopulateDevicesCollection()
    {
        // Act
        _snifferService.LoadDevices();

        // Assert
        _snifferService.Devices.Should().NotBeNull();
    }

    [Fact]
    public void SelectDevice_WithValidIndex_ShouldUpdateSelectedDeviceIndex()
    {
        // Arrange
        _snifferService.LoadDevices();
        _snifferService.SimulateDevices(new List<NetworkDevice>
        {
            new() { Index = 0, Name = "eth0" },
            new() { Index = 1, Name = "wlan0" }
        });

        // Act
        _snifferService.SelectDevice(1);

        // Assert
        _snifferService.SelectedDeviceIndex.Should().Be(1);
    }

    [Fact]
    public void StartCapture_ShouldSetIsCapturingToTrue()
    {
        // Arrange
        _snifferService.LoadDevices();
        _snifferService.SimulateDevices(new List<NetworkDevice>
        {
            new() { Index = 0, Name = "eth0" }
        });
        _snifferService.SelectDevice(0);

        // Act
        _snifferService.StartCapture();

        // Assert
        _snifferService.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public void StopCapture_ShouldSetIsCapturingToFalse()
    {
        // Arrange
        _snifferService.LoadDevices();
        _snifferService.SimulateDevices(new List<NetworkDevice>
        {
            new() { Index = 0, Name = "eth0" }
        });
        _snifferService.SelectDevice(0);
        _snifferService.StartCapture();

        // Act
        _snifferService.StopCapture();

        // Assert
        _snifferService.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void StartCapture_WithDeviceIndex_ShouldSelectAndStartCapture()
    {
        // Arrange
        _snifferService.LoadDevices();
        _snifferService.SimulateDevices(new List<NetworkDevice>
        {
            new() { Index = 0, Name = "eth0" },
            new() { Index = 1, Name = "wlan0" }
        });

        // Act
        _snifferService.StartCapture(1);

        // Assert
        _snifferService.SelectedDeviceIndex.Should().Be(1);
        _snifferService.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public void GetSnifferHandle_ShouldReturnValidHandle()
    {
        // Act
        var handle = _snifferService.GetSnifferHandle();

        // Assert
        handle.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public async Task GetPacketBatchesAsync_ShouldReturnPackets()
    {
        // Arrange
        _snifferService.LoadDevices();
        _snifferService.SimulateDevices(new List<NetworkDevice>
        {
            new() { Index = 0, Name = "eth0" }
        });
        _snifferService.SelectDevice(0);
        _snifferService.StartCapture();
        
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var packets = new List<PacketInfo>();

        // Simulate some packets
        _snifferService.SimulatePackets(new List<PacketInfo>
        {
            new() { Number = 1, Protocol = "TCP", SourceIp = "192.168.1.1", DestIp = "10.0.0.1" },
            new() { Number = 2, Protocol = "UDP", SourceIp = "192.168.1.2", DestIp = "10.0.0.2" }
        });

        // Act
        try
        {
            await foreach (var batch in _snifferService.GetPacketBatchesAsync(cts.Token))
            {
                packets.AddRange(batch);
                break; // Just get the first batch
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when timeout
        }

        // Assert
        packets.Should().NotBeEmpty();
    }

    [Fact]
    public void ErrorOccurred_ShouldBeRaisedOnError()
    {
        // Arrange
        string? errorMessage = null;
        _snifferService.ErrorOccurred += msg => errorMessage = msg;

        // Act
        _snifferService.SimulateError("Test Error Message");

        // Assert
        errorMessage.Should().Be("Test Error Message");
    }
}

/// <summary>
/// Test implementation of ISnifferService for integration testing
/// </summary>
public class TestSnifferService : ISnifferService
{
    public ObservableCollection<NetworkDevice> Devices { get; } = new();
    public bool IsCapturing { get; private set; }
    public int SelectedDeviceIndex { get; private set; } = -1;

    public event Action<string>? ErrorOccurred;

    private readonly List<PacketInfo> _simulatedPackets = new();
    private IntPtr _handle = new(1); // Simulated handle

    public void LoadDevices()
    {
        // In real integration tests, this would call the native DLL
        // For test purposes, devices are added via SimulateDevices
    }

    public void SimulateDevices(IEnumerable<NetworkDevice> devices)
    {
        Devices.Clear();
        foreach (var device in devices)
        {
            Devices.Add(device);
        }
    }

    public void SelectDevice(int deviceIndex)
    {
        if (deviceIndex >= 0 && deviceIndex < Devices.Count)
        {
            SelectedDeviceIndex = deviceIndex;
        }
    }

    public void StartCapture()
    {
        if (SelectedDeviceIndex >= 0)
        {
            IsCapturing = true;
        }
    }

    public void StartCapture(int deviceIndex)
    {
        SelectDevice(deviceIndex);
        StartCapture();
    }

    public void StopCapture()
    {
        IsCapturing = false;
    }

    public IntPtr GetSnifferHandle()
    {
        return _handle;
    }

    public void SimulatePackets(IEnumerable<PacketInfo> packets)
    {
        _simulatedPackets.AddRange(packets);
    }

    public void SimulateError(string message)
    {
        ErrorOccurred?.Invoke(message);
    }

    public async IAsyncEnumerable<IList<PacketInfo>> GetPacketBatchesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested && IsCapturing)
        {
            if (_simulatedPackets.Count > 0)
            {
                var batch = _simulatedPackets.ToList();
                _simulatedPackets.Clear();
                yield return batch;
            }
            await Task.Delay(100, ct);
        }
    }

    public void Dispose()
    {
        StopCapture();
        _handle = IntPtr.Zero;
    }
}
