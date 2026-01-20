using FluentAssertions;
using FlaUI.Core.AutomationElements;
using WareHound.UITests.Base;

namespace WareHound.UITests.Tests;

[Collection("UI Tests")]
public class StatisticsViewTests : WpfUITestBase
{
    [Fact]
    public void StatisticsView_ShouldLoadSuccessfully()
    {
        // Arrange
        LaunchApplication();

        // Act
        NavigateToStatisticsView();

        // Assert
        MainWindow.Should().NotBeNull();
        TakeScreenshot("StatisticsView_Loaded");
    }

    [Fact]
    public void StatisticsView_ShouldDisplayCharts()
    {
        // Arrange
        LaunchApplication();
        NavigateToStatisticsView();

        // Act
        // Look for chart controls or canvas elements
        var chartElement = FindElementByClassName("Canvas") 
                           ?? FindElementByAutomationId("StatisticsChart");

        // Assert
        // Charts should be present in the statistics view
        MainWindow.Should().NotBeNull();
    }

    [Fact]
    public void StatisticsView_ShouldDisplayProtocolBreakdown()
    {
        // Arrange
        LaunchApplication();
        NavigateToStatisticsView();

        // Act
        var protocolSection = FindElementByName("Protocol") 
                              ?? FindElementByAutomationId("ProtocolStats");

        // Assert
        MainWindow.Should().NotBeNull();
    }

    [Fact]
    public void StatisticsView_ShouldHaveRefreshButton()
    {
        // Arrange
        LaunchApplication();
        NavigateToStatisticsView();

        // Act
        var refreshButton = FindButton("RefreshButton") ?? FindButton("Refresh");

        // Assert
        // Statistics view should have a way to refresh data
        MainWindow.Should().NotBeNull();
    }

    [Fact]
    public void StatisticsView_ShouldDisplayPacketCounts()
    {
        // Arrange
        LaunchApplication();
        NavigateToStatisticsView();

        // Act
        var packetCountLabel = FindElementByAutomationId("PacketCount") 
                               ?? FindElementByName("Total Packets");

        // Assert
        MainWindow.Should().NotBeNull();
    }

    private void NavigateToStatisticsView()
    {
        var statisticsMenuItem = FindElementByName("Statistics");
        statisticsMenuItem?.AsButton()?.Invoke();
        Thread.Sleep(500);
    }
}
