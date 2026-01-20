using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Prism.Events;
using WareHound.UI.Infrastructure.Events;
using WareHound.UI.Infrastructure.Services;
using WareHound.UI.Models;
using WareHound.UI.Services;
using WareHound.UI.ViewModels;

namespace WareHound.IntegrationTests.ViewModels;

public class ViewModelServiceIntegrationTests
{
    [Fact]
    public void CaptureViewModel_Integration_ShouldDisplayDevicesFromService()
    {
        // Arrange
        var devices = new ObservableCollection<NetworkDevice>
        {
            new() { Index = 0, Name = "eth0", Description = "Ethernet" },
            new() { Index = 1, Name = "wlan0", Description = "WiFi" }
        };

        var mockSnifferService = new Mock<ISnifferService>();
        mockSnifferService.Setup(s => s.Devices).Returns(devices);

        var mockCollectionService = new Mock<IPacketCollectionService>();
        var mockEventAggregator = new Mock<IEventAggregator>();
        var mockLoggerService = new Mock<ILoggerService>();
        var mockCaptureStateEvent = new Mock<CaptureStateChangedEvent>();

        mockEventAggregator
            .Setup(ea => ea.GetEvent<CaptureStateChangedEvent>())
            .Returns(mockCaptureStateEvent.Object);

        // Act
        var viewModel = new CaptureViewModel(
            mockSnifferService.Object,
            mockCollectionService.Object,
            mockEventAggregator.Object,
            mockLoggerService.Object);

        // Assert
        viewModel.Devices.Should().HaveCount(2);
        viewModel.Devices[0].DisplayName.Should().Be("Ethernet");
        viewModel.Devices[1].DisplayName.Should().Be("WiFi");
    }

    [Fact]
    public void CaptureViewModel_Integration_PacketsShouldBeObservable()
    {
        // Arrange
        var devices = new ObservableCollection<NetworkDevice>();
        var mockSnifferService = new Mock<ISnifferService>();
        mockSnifferService.Setup(s => s.Devices).Returns(devices);

        var mockCollectionService = new Mock<IPacketCollectionService>();
        var mockEventAggregator = new Mock<IEventAggregator>();
        var mockLoggerService = new Mock<ILoggerService>();
        var mockCaptureStateEvent = new Mock<CaptureStateChangedEvent>();

        mockEventAggregator
            .Setup(ea => ea.GetEvent<CaptureStateChangedEvent>())
            .Returns(mockCaptureStateEvent.Object);

        var viewModel = new CaptureViewModel(
            mockSnifferService.Object,
            mockCollectionService.Object,
            mockEventAggregator.Object,
            mockLoggerService.Object);

        var packetsChangedCount = 0;
        viewModel.Packets.CollectionChanged += (_, _) => packetsChangedCount++;

        // Act
        viewModel.Packets.Add(new PacketInfo { Number = 1, Protocol = "TCP" });
        viewModel.Packets.Add(new PacketInfo { Number = 2, Protocol = "UDP" });

        // Assert
        packetsChangedCount.Should().Be(2);
        viewModel.Packets.Should().HaveCount(2);
    }

    [Fact]
    public void NetworkDevice_Integration_ShouldDisplayCorrectly()
    {
        // Arrange
        var device = new NetworkDevice
        {
            Index = 0,
            Name = "\\Device\\NPF_{12345678-1234-1234-1234-123456789ABC}",
            Description = "Intel(R) Ethernet Connection"
        };

        // Act & Assert
        device.DisplayName.Should().Be("Intel(R) Ethernet Connection");
        device.ToString().Should().Be("Intel(R) Ethernet Connection");
    }

    [Fact]
    public void PacketInfo_Integration_ShouldConvertFromSnapshot()
    {
        // Arrange
        var snapshot = new SnapshotStruct
        {
            Id = 1,
            SourcePort = 443,
            DestPort = 8080,
            Protocol = "HTTPS",
            SourceIp = "192.168.1.100",
            DestIp = "93.184.216.34",
            SourceMac = "AA:BB:CC:DD:EE:FF",
            DestMac = "11:22:33:44:55:66",
            HostName = "example.com"
        };

        // Act
        var packet = PacketInfo.FromSnapshot(snapshot, 42);

        // Assert
        packet.Number.Should().Be(42);
        packet.Id.Should().Be(1);
        packet.Protocol.Should().Be("HTTPS");
        packet.SourceIp.Should().Be("192.168.1.100");
        packet.DestIp.Should().Be("93.184.216.34");
        packet.HostName.Should().Be("example.com");
        packet.Info.Should().Contain("443");
        packet.Info.Should().Contain("8080");
        packet.Info.Should().Contain("example.com");
    }
}
