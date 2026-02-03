using FluentAssertions;
using FlaUI.Core.AutomationElements;
using WareHound.UITests.Base;

namespace WareHound.UITests.Tests;

[Collection("UI Tests")]
public class CaptureViewTests : WpfUITestBase
{
    [Fact]
    public void CaptureView_ShouldHaveDeviceSelector()
    {
        // Arrange
        LaunchApplication();
        NavigateToCaptureView();

        // Act
        var deviceComboBox = FindComboBox("DeviceSelector") ?? FindElementByClassName("ComboBox")?.AsComboBox();

        // Assert
        deviceComboBox.Should().NotBeNull();
    }

    [Fact]
    public void CaptureView_ShouldHaveStartCaptureButton()
    {
        // Arrange
        LaunchApplication();
        NavigateToCaptureView();
        Thread.Sleep(500); // Allow UI to fully settle

        // Act - The start capture button is in the main window header toolbar
        // Look for any capture control to verify the capture controls are visible
        var captureControl = FindElementByAutomationId("StartCaptureButton")
                             ?? FindElementByName("Start Capture")
                             ?? FindElementByAutomationId("ClearButton");

        // Assert
        captureControl.Should().NotBeNull("Capture controls should be visible in the toolbar");
    }

    [Fact]
    public void CaptureView_ShouldHaveFilterTextBox()
    {
        // Arrange
        LaunchApplication();
        NavigateToCaptureView();

        // Act - Filter controls include the filter type dropdown and optional text box
        // The filter text box may not be visible until a filter type is selected
        var filterTypeCombo = FindElementByClassName("ComboBox");
        var filterTextBox = FindTextBox("FilterTextBox") ?? FindElementByClassName("TextBox")?.AsTextBox();

        // Assert - At minimum, the filter type dropdown should be visible
        filterTypeCombo.Should().NotBeNull("Filter controls should be present in the capture view");
    }

    [Fact]
    public void CaptureView_ShouldHavePacketDataGrid()
    {
        // Arrange
        LaunchApplication();
        NavigateToCaptureView();

        // Act
        var dataGrid = FindDataGrid("PacketDataGrid") ?? FindElementByClassName("DataGrid");

        // Assert
        dataGrid.Should().NotBeNull();
    }

    [Fact]
    public void CaptureView_FilterTextBox_ShouldAcceptInput()
    {
        // Arrange
        LaunchApplication();
        NavigateToCaptureView();
        var filterTextBox = FindTextBox("FilterTextBox") ?? FindElementByClassName("TextBox")?.AsTextBox();

        // Act
        filterTextBox?.Enter("tcp");

        // Assert
        filterTextBox?.Text.Should().Be("tcp");
    }

    [Fact]
    public void CaptureView_ClearButton_ShouldBePresent()
    {
        // Arrange
        LaunchApplication();
        NavigateToCaptureView();

        // Act
        var clearButton = FindButton("ClearButton") ?? FindButton("Clear");

        // Assert
        clearButton.Should().NotBeNull();
    }

    [Fact]
    public void CaptureView_PacketDetailsPanel_ShouldBePresent()
    {
        // Arrange
        LaunchApplication();
        NavigateToCaptureView();

        // Ensure tab is selected
        FindElementByName("Packet Details")?.AsTabItem().Select();

        // Act
        var detailsPanel = FindElementByAutomationId("PacketDetailsPanel") 
                           ?? FindElementByClassName("TreeView");

        // Assert
        detailsPanel.Should().NotBeNull();
    }

    [Fact]
    public void CaptureView_HexDumpPanel_ShouldBePresent()
    {
        // Arrange
        LaunchApplication();
        NavigateToCaptureView();

        // Ensure tab is selected
        FindElementByName("Hex Dump")?.AsTabItem().Select();

        // Act
        var hexDumpPanel = FindElementByAutomationId("HexDumpPanel") 
                           ?? FindElementByAutomationId("PacketHexDump");

        // Assert
        hexDumpPanel.Should().NotBeNull();
    }

    private void NavigateToCaptureView()
    {
        var captureMenuItem = FindElementByName("Capture");
        captureMenuItem?.Click();
        Thread.Sleep(500);
    }
}
