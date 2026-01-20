using FluentAssertions;
using WareHound.UITests.Base;

namespace WareHound.UITests.Tests;

[Collection("UI Tests")]
public class SeleniumMainWindowTests : SeleniumUITestBase
{
    [Fact(Skip = "Requires WinAppDriver and WareHound.UI to be built. Remove Skip to run.")]
    public void MainWindow_Selenium_ShouldLaunchSuccessfully()
    {
        // Act
        InitializeDriver();

        // Assert
        Driver.Should().NotBeNull();
        Driver!.Title.Should().Contain("WareHound");
    }

    [Fact(Skip = "Requires WinAppDriver and WareHound.UI to be built. Remove Skip to run.")]
    public void MainWindow_Selenium_ShouldHaveNavigationItems()
    {
        // Arrange
        InitializeDriver();

        // Act
        var dashboardItem = FindElementByName("Dashboard");
        var captureItem = FindElementByName("Capture");
        var statisticsItem = FindElementByName("Statistics");
        var settingsItem = FindElementByName("Settings");

        // Assert
        dashboardItem.Should().NotBeNull();
        captureItem.Should().NotBeNull();
        statisticsItem.Should().NotBeNull();
        settingsItem.Should().NotBeNull();
    }

    [Fact(Skip = "Requires WinAppDriver and WareHound.UI to be built. Remove Skip to run.")]
    public void MainWindow_Selenium_NavigateToCaptureView()
    {
        // Arrange
        InitializeDriver();

        // Act
        var captureItem = FindElementByName("Capture");
        captureItem?.Click();
        Thread.Sleep(500);

        // Assert
        var deviceSelector = FindElementByClassName("ComboBox");
        deviceSelector.Should().NotBeNull();
    }

    [Fact(Skip = "Requires WinAppDriver and WareHound.UI to be built. Remove Skip to run.")]
    public void CaptureView_Selenium_FilterInput()
    {
        // Arrange
        InitializeDriver();
        var captureItem = FindElementByName("Capture");
        captureItem?.Click();
        Thread.Sleep(500);

        // Act
        var filterBox = FindElementByClassName("TextBox");
        filterBox?.SendKeys("tcp");

        // Assert
        filterBox?.Text.Should().Contain("tcp");
    }

    [Fact(Skip = "Requires WinAppDriver and WareHound.UI to be built. Remove Skip to run.")]
    public void MainWindow_Selenium_TakeScreenshot()
    {
        // Arrange
        InitializeDriver();

        // Act
        TakeScreenshot("MainWindow_Selenium");

        // Assert
        var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
        Directory.Exists(screenshotDir).Should().BeTrue();
    }

    [Fact(Skip = "Requires WinAppDriver and WareHound.UI to be built. Remove Skip to run.")]
    public void MainWindow_Selenium_ElementVisibility()
    {
        // Arrange
        InitializeDriver();

        // Act & Assert
        var dashboard = FindElementByName("Dashboard");
        dashboard?.Displayed.Should().BeTrue();
    }
}
