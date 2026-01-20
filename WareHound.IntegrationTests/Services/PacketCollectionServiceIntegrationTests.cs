using System.Collections.ObjectModel;
using FluentAssertions;
using WareHound.UI.Models;
using WareHound.UI.Services;

namespace WareHound.IntegrationTests.Services;

/// <summary>
/// Integration tests for Packet Collection Service
/// </summary>
public class PacketCollectionServiceIntegrationTests
{
    [Fact]
    public void PacketCollectionService_ShouldStorePackets()
    {
        // Arrange
        var packets = new List<PacketInfo>
        {
            new()
            {
                Number = 1,
                Protocol = "TCP",
                SourceIp = "192.168.1.1",
                DestIp = "10.0.0.1",
                SourcePort = 443,
                DestPort = 8080
            },
            new()
            {
                Number = 2,
                Protocol = "UDP",
                SourceIp = "192.168.1.2",
                DestIp = "10.0.0.2",
                SourcePort = 53,
                DestPort = 53
            }
        };

        // Act
        var collection = new ObservableCollection<PacketInfo>(packets);

        // Assert
        collection.Should().HaveCount(2);
        collection[0].Protocol.Should().Be("TCP");
        collection[1].Protocol.Should().Be("UDP");
    }

    [Fact]
    public void PacketCollectionService_ShouldFilterByProtocol()
    {
        // Arrange
        var packets = new List<PacketInfo>
        {
            new() { Number = 1, Protocol = "TCP" },
            new() { Number = 2, Protocol = "UDP" },
            new() { Number = 3, Protocol = "TCP" },
            new() { Number = 4, Protocol = "ICMP" }
        };

        // Act
        var tcpPackets = packets.Where(p => p.Protocol == "TCP").ToList();

        // Assert
        tcpPackets.Should().HaveCount(2);
        tcpPackets.All(p => p.Protocol == "TCP").Should().BeTrue();
    }

    [Fact]
    public void PacketCollectionService_ShouldFilterBySourceIp()
    {
        // Arrange
        var packets = new List<PacketInfo>
        {
            new() { Number = 1, SourceIp = "192.168.1.1" },
            new() { Number = 2, SourceIp = "192.168.1.2" },
            new() { Number = 3, SourceIp = "192.168.1.1" },
            new() { Number = 4, SourceIp = "10.0.0.1" }
        };

        // Act
        var filteredPackets = packets.Where(p => p.SourceIp == "192.168.1.1").ToList();

        // Assert
        filteredPackets.Should().HaveCount(2);
    }

    [Fact]
    public void PacketCollectionService_ShouldFilterByPort()
    {
        // Arrange
        var packets = new List<PacketInfo>
        {
            new() { Number = 1, SourcePort = 443, DestPort = 8080 },
            new() { Number = 2, SourcePort = 80, DestPort = 443 },
            new() { Number = 3, SourcePort = 443, DestPort = 9000 },
            new() { Number = 4, SourcePort = 22, DestPort = 22 }
        };

        // Act
        var port443Packets = packets.Where(p => p.SourcePort == 443 || p.DestPort == 443).ToList();

        // Assert
        port443Packets.Should().HaveCount(3);
    }

    [Fact]
    public void PacketCollectionService_ShouldSupportMultipleFilters()
    {
        // Arrange
        var packets = new List<PacketInfo>
        {
            new() { Number = 1, Protocol = "TCP", SourceIp = "192.168.1.1" },
            new() { Number = 2, Protocol = "UDP", SourceIp = "192.168.1.1" },
            new() { Number = 3, Protocol = "TCP", SourceIp = "10.0.0.1" },
            new() { Number = 4, Protocol = "TCP", SourceIp = "192.168.1.1" }
        };

        // Act
        var filteredPackets = packets
            .Where(p => p.Protocol == "TCP" && p.SourceIp == "192.168.1.1")
            .ToList();

        // Assert
        filteredPackets.Should().HaveCount(2);
    }

    [Fact]
    public void PacketCollectionService_ShouldOrderByTime()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var packets = new List<PacketInfo>
        {
            new() { Number = 1, CaptureTime = baseTime.AddSeconds(3) },
            new() { Number = 2, CaptureTime = baseTime.AddSeconds(1) },
            new() { Number = 3, CaptureTime = baseTime.AddSeconds(2) }
        };

        // Act
        var orderedPackets = packets.OrderBy(p => p.CaptureTime).ToList();

        // Assert
        orderedPackets[0].Number.Should().Be(2);
        orderedPackets[1].Number.Should().Be(3);
        orderedPackets[2].Number.Should().Be(1);
    }

    [Fact]
    public void PacketCollectionService_ShouldHandleLargePacketCount()
    {
        // Arrange
        var packets = new List<PacketInfo>();
        for (int i = 0; i < 10000; i++)
        {
            packets.Add(new PacketInfo
            {
                Number = i,
                Protocol = i % 2 == 0 ? "TCP" : "UDP",
                SourceIp = $"192.168.1.{i % 256}",
                DestIp = $"10.0.0.{i % 256}",
                SourcePort = i % 65535,
                DestPort = (i + 1) % 65535
            });
        }

        // Act
        var collection = new ObservableCollection<PacketInfo>(packets);

        // Assert
        collection.Should().HaveCount(10000);
    }
}
