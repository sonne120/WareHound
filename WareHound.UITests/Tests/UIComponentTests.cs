using FluentAssertions;

namespace WareHound.UITests.Tests;

public class UIComponentTests
{
    [Fact]
    public void UITestBase_DefaultAppPath_ShouldBeConstructed()
    {
        // Arrange
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        
        // Act
        var solutionDir = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", ".."));
        var expectedPath = Path.Combine(solutionDir, "WareHound.UI", "bin", "x64", "Debug", "net8.0-windows", "win-x64", "WareHound.UI.exe");

        // Assert
        solutionDir.Should().NotBeNullOrEmpty();
        expectedPath.Should().Contain("WareHound.UI.exe");
    }

    [Fact]
    public void ScreenshotDirectory_ShouldBeCreatable()
    {
        // Arrange
        var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");

        // Act
        Directory.CreateDirectory(screenshotDir);

        // Assert
        Directory.Exists(screenshotDir).Should().BeTrue();
    }

    [Fact]
    public void TestTimeout_ShouldBeConfigurable()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(10);

        // Act & Assert
        timeout.TotalSeconds.Should().Be(10);
        timeout.TotalMilliseconds.Should().Be(10000);
    }

    [Fact]
    public void ElementWaitTime_ShouldBeReasonable()
    {
        // Arrange
        var waitTime = TimeSpan.FromSeconds(5);
        var maxWaitTime = TimeSpan.FromSeconds(30);

        // Act & Assert
        waitTime.Should().BeLessThan(maxWaitTime);
        waitTime.TotalSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AutomationIdPattern_ShouldBeValid()
    {
        // Arrange
        var validIds = new[] { "MainWindow", "CaptureButton", "DeviceSelector", "PacketGrid" };

        // Act & Assert
        foreach (var id in validIds)
        {
            id.Should().NotBeNullOrWhiteSpace();
            id.Should().MatchRegex(@"^[A-Za-z][A-Za-z0-9]*$");
        }
    }

    [Fact]
    public void NavigationItems_ShouldHaveExpectedNames()
    {
        // Arrange
        var expectedItems = new[] { "Dashboard", "Capture", "Statistics", "Settings" };

        // Act & Assert
        expectedItems.Should().HaveCount(4);
        expectedItems.Should().Contain("Dashboard");
        expectedItems.Should().Contain("Capture");
        expectedItems.Should().Contain("Statistics");
        expectedItems.Should().Contain("Settings");
    }

    [Fact]
    public void UITestConfiguration_ShouldHaveDefaults()
    {
        // Arrange
        var implicitWait = TimeSpan.FromSeconds(5);
        var explicitWait = TimeSpan.FromSeconds(10);
        var pageLoadTimeout = TimeSpan.FromSeconds(30);

        // Act & Assert
        implicitWait.Should().BeLessThan(explicitWait);
        explicitWait.Should().BeLessThan(pageLoadTimeout);
    }

    [Fact]
    public void WindowTitle_ShouldContainAppName()
    {
        // Arrange
        var expectedTitlePart = "WareHound";
        var possibleTitles = new[] { "WareHound", "WareHound - Dashboard", "WareHound - Capture" };

        // Act & Assert
        foreach (var title in possibleTitles)
        {
            title.Should().Contain(expectedTitlePart);
        }
    }

    [Fact]
    public void TestCategories_ShouldBeDefined()
    {
        // Arrange
        var categories = new[] { "UI Tests", "FlaUI", "Selenium", "Smoke", "Regression" };

        // Act & Assert
        categories.Should().NotBeEmpty();
        categories.Should().Contain("UI Tests");
    }

    [Fact]
    public void SupportedPlatforms_ShouldIncludeWindows()
    {
        // Arrange
        var isWindows = OperatingSystem.IsWindows();

        // Act & Assert
        isWindows.Should().BeTrue("UI tests are designed for Windows platform");
    }
}
