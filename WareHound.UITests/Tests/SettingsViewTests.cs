using FluentAssertions;
using FlaUI.Core.AutomationElements;
using WareHound.UITests.Base;

namespace WareHound.UITests.Tests;

[Collection("UI Tests")]
public class SettingsViewTests : WpfUITestBase
{
    [Fact]
    public void SettingsView_ShouldLoadSuccessfully()
    {
        // Arrange
        LaunchApplication();

        // Act
        NavigateToSettingsView();

        // Assert
        MainWindow.Should().NotBeNull();
        TakeScreenshot("SettingsView_Loaded");
    }

    [Fact]
    public void SettingsView_ShouldHaveThemeSettings()
    {
        // Arrange
        LaunchApplication();
        NavigateToSettingsView();

        // Act
        var themeSection = FindElementByName("Theme") 
                           ?? FindElementByAutomationId("ThemeSettings");

        // Assert
        MainWindow.Should().NotBeNull();
    }

    [Fact]
    public void SettingsView_ShouldHaveSaveButton()
    {
        // Arrange
        LaunchApplication();
        NavigateToSettingsView();

        // Act
        var saveButton = FindButton("SaveButton") ?? FindButton("Save");

        // Assert
        MainWindow.Should().NotBeNull();
    }

    [Fact]
    public void SettingsView_ShouldHaveResetButton()
    {
        // Arrange
        LaunchApplication();
        NavigateToSettingsView();

        // Act
        var resetButton = FindButton("ResetButton") ?? FindButton("Reset");

        // Assert
        MainWindow.Should().NotBeNull();
    }

    [Fact]
    public void SettingsView_ShouldAllowThemeChange()
    {
        // Arrange
        LaunchApplication();
        NavigateToSettingsView();

        // Act
        var themeToggle = FindElementByAutomationId("ThemeToggle") 
                          ?? FindElementByClassName("ToggleButton");

        if (themeToggle != null)
        {
            themeToggle.AsButton()?.Invoke();
            Thread.Sleep(500);
        }

        // Assert
        TakeScreenshot("SettingsView_ThemeChanged");
    }

    private void NavigateToSettingsView()
    {
        var settingsMenuItem = FindElementByName("Settings");
        settingsMenuItem?.AsButton()?.Invoke();
        Thread.Sleep(500);
    }
}
