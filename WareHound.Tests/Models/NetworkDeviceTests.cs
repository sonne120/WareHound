using FluentAssertions;
using WareHound.UI.Models;

namespace WareHound.Tests.Models;

public class NetworkDeviceTests
{
    [Fact]
    public void NetworkDevice_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var device = new NetworkDevice();

        // Assert
        device.Index.Should().Be(0);
        device.Name.Should().BeEmpty();
        device.Description.Should().BeEmpty();
    }

    [Fact]
    public void DisplayName_WhenDescriptionIsNotEmpty_ShouldReturnDescription()
    {
        // Arrange
        var device = new NetworkDevice
        {
            Name = "eth0",
            Description = "Ethernet Adapter"
        };

        // Act
        var displayName = device.DisplayName;

        // Assert
        displayName.Should().Be("Ethernet Adapter");
    }

    [Fact]
    public void DisplayName_WhenDescriptionIsEmpty_ShouldReturnName()
    {
        // Arrange
        var device = new NetworkDevice
        {
            Name = "eth0",
            Description = ""
        };

        // Act
        var displayName = device.DisplayName;

        // Assert
        displayName.Should().Be("eth0");
    }

    [Fact]
    public void DisplayName_WhenDescriptionIsNull_ShouldReturnName()
    {
        // Arrange
        var device = new NetworkDevice
        {
            Name = "wlan0",
            Description = null!
        };

        // Act
        var displayName = device.DisplayName;

        // Assert
        displayName.Should().Be("wlan0");
    }

    [Fact]
    public void ToString_ShouldReturnDisplayName()
    {
        // Arrange
        var device = new NetworkDevice
        {
            Name = "lo0",
            Description = "Loopback Interface"
        };

        // Act
        var result = device.ToString();

        // Assert
        result.Should().Be("Loopback Interface");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    public void Index_ShouldStoreValue(int expectedIndex)
    {
        // Arrange
        var device = new NetworkDevice { Index = expectedIndex };

        // Act & Assert
        device.Index.Should().Be(expectedIndex);
    }
}
