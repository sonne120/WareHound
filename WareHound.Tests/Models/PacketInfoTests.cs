using FluentAssertions;
using WareHound.UI.Models;

namespace WareHound.Tests.Models;

public class PacketInfoTests
{
    [Fact]
    public void PacketInfo_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var packet = new PacketInfo();

        // Assert
        packet.Protocol.Should().BeEmpty();
        packet.SourceIp.Should().BeEmpty();
        packet.DestIp.Should().BeEmpty();
        packet.SourceMac.Should().BeEmpty();
        packet.DestMac.Should().BeEmpty();
        packet.HostName.Should().BeEmpty();
    }

    [Fact]
    public void PacketInfo_TimeDisplay_ShouldFormatCorrectly()
    {
        // Arrange
        var packet = new PacketInfo
        {
            CaptureTime = new DateTime(2026, 1, 20, 14, 30, 45, 123)
        };

        // Act
        var timeDisplay = packet.TimeDisplay;

        // Assert
        timeDisplay.Should().Be("14:30:45.123");
    }

    [Fact]
    public void PacketInfo_Info_ShouldIncludePortsAndHost()
    {
        // Arrange
        var packet = new PacketInfo
        {
            SourcePort = 443,
            DestPort = 8080,
            HostName = "example.com",
            Id = 12345
        };

        // Act
        var info = packet.Info;

        // Assert
        info.Should().Contain("443");
        info.Should().Contain("8080");
        info.Should().Contain("example.com");
        info.Should().Contain("12345");
    }

    [Fact]
    public void PacketInfo_Info_ShouldShowUnknownWhenHostNameIsEmpty()
    {
        // Arrange
        var packet = new PacketInfo
        {
            SourcePort = 80,
            DestPort = 443,
            HostName = "",
            Id = 1
        };

        // Act
        var info = packet.Info;

        // Assert
        info.Should().Contain("Unknown");
    }

    [Fact]
    public void FromSnapshot_ShouldMapAllPropertiesCorrectly()
    {
        // Arrange
        var snapshot = new SnapshotStruct
        {
            Id = 100,
            SourcePort = 8080,
            DestPort = 443,
            Protocol = "TCP",
            SourceIp = "192.168.1.1",
            DestIp = "10.0.0.1",
            SourceMac = "AA:BB:CC:DD:EE:FF",
            DestMac = "11:22:33:44:55:66",
            HostName = "test.local"
        };

        // Act
        var packet = PacketInfo.FromSnapshot(snapshot, 42);

        // Assert
        packet.Number.Should().Be(42);
        packet.Id.Should().Be(100);
        packet.SourcePort.Should().Be(8080);
        packet.DestPort.Should().Be(443);
        packet.Protocol.Should().Be("TCP");
        packet.SourceIp.Should().Be("192.168.1.1");
        packet.DestIp.Should().Be("10.0.0.1");
        packet.SourceMac.Should().Be("AA:BB:CC:DD:EE:FF");
        packet.DestMac.Should().Be("11:22:33:44:55:66");
        packet.HostName.Should().Be("test.local");
        packet.CaptureTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FromSnapshot_WithNullValues_ShouldDefaultToEmptyStrings()
    {
        // Arrange
        var snapshot = new SnapshotStruct
        {
            Id = 1,
            Protocol = null,
            SourceIp = null,
            DestIp = null,
            SourceMac = null,
            DestMac = null,
            HostName = null
        };

        // Act
        var packet = PacketInfo.FromSnapshot(snapshot, 1);

        // Assert
        packet.Protocol.Should().BeEmpty();
        packet.SourceIp.Should().BeEmpty();
        packet.DestIp.Should().BeEmpty();
        packet.SourceMac.Should().BeEmpty();
        packet.DestMac.Should().BeEmpty();
        packet.HostName.Should().BeEmpty();
    }
}
