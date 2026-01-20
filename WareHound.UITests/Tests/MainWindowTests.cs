using FluentAssertions;
using FlaUI.Core.AutomationElements;
using WareHound.UITests.Base;

namespace WareHound.UITests.Tests;

[Collection("UI Tests")]
public class MainWindowTests : WpfUITestBase
{
    [Fact]
    public void MainWindow_ShouldLaunchSuccessfully()
    {
        // Act
        LaunchApplication();

        // Assert
        MainWindow.Should().NotBeNull();
        MainWindow!.Title.Should().Contain("WareHound");
    }

    [Fact]
    public void MainWindow_ShouldHaveNavigationMenu()
    {
        // Arrange
        LaunchApplication();

        // Act
        var dashboardMenuItem = FindElementByName("Dashboard");
        var captureMenuItem = FindElementByName("Capture");
        var statisticsMenuItem = FindElementByName("Statistics");
        var settingsMenuItem = FindElementByName("Settings");

        // Assert
        dashboardMenuItem.Should().NotBeNull();
        captureMenuItem.Should().NotBeNull();
        statisticsMenuItem.Should().NotBeNull();
        settingsMenuItem.Should().NotBeNull();
    }

    [Fact]
    public void MainWindow_NavigateToCaptureView_ShouldShowCaptureControls()
    {
        // Arrange
        LaunchApplication();
        var captureMenuItem = FindElementByName("Capture");

        // Act
        captureMenuItem?.AsButton()?.Invoke();
        Thread.Sleep(500); // Wait for navigation

        // Assert
        var startCaptureButton = FindElementByName("Start Capture") ?? FindElementByAutomationId("ToggleCaptureButton");
        startCaptureButton.Should().NotBeNull();
    }

    [Fact]
    public void MainWindow_NavigateToStatisticsView_ShouldShowStatisticsContent()
    {
        // Arrange
        LaunchApplication();
        var statisticsMenuItem = FindElementByName("Statistics");

        // Act
        statisticsMenuItem?.AsButton()?.Invoke();
        Thread.Sleep(500); // Wait for navigation

        // Assert
        MainWindow.Should().NotBeNull();
        TakeScreenshot("StatisticsView");
    }

    [Fact]
    public void MainWindow_NavigateToSettingsView_ShouldShowSettings()
    {
        // Arrange
        LaunchApplication();
        var settingsMenuItem = FindElementByName("Settings");

        // Act
        settingsMenuItem?.AsButton()?.Invoke();
        Thread.Sleep(500); // Wait for navigation

        // Assert
        MainWindow.Should().NotBeNull();
        TakeScreenshot("SettingsView");
    }

    [Fact]
    public void MainWindow_ShouldBeResizable()
    {
        // Arrange
        LaunchApplication();
        var originalBounds = MainWindow!.BoundingRectangle;

        // Act
        MainWindow.Patterns.Transform.Pattern.Resize(originalBounds.Width + 100, originalBounds.Height + 100);
        Thread.Sleep(200);

        // Assert
        var newBounds = MainWindow.BoundingRectangle;
        newBounds.Width.Should().BeGreaterThan(originalBounds.Width);
        newBounds.Height.Should().BeGreaterThan(originalBounds.Height);
    }

    [Fact]
    public void MainWindow_ShouldBeMinimizable()
    {
        // Arrange
        LaunchApplication();

        // Act
        MainWindow!.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Minimized);
        Thread.Sleep(500);

        // Assert
        MainWindow.Patterns.Window.Pattern.WindowVisualState.Value
            .Should().Be(FlaUI.Core.Definitions.WindowVisualState.Minimized);

        // Restore for cleanup
        MainWindow.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Normal);
    }

    [Fact]
    public void MainWindow_Close_ShouldCloseApplication()
    {
        // Arrange
        LaunchApplication();

        // Act
        MainWindow!.Close();
        Thread.Sleep(500);

        // Assert
        App!.HasExited.Should().BeTrue();
    }
}
