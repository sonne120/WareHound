using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Prism.Events;
using WareHound.UI.Infrastructure.Events;
using WareHound.UI.Infrastructure.Services;
using WareHound.UI.Models;
using WareHound.UI.Services;
using WareHound.UI.ViewModels;

namespace WareHound.Tests.ViewModels;

public class CaptureViewModelTests
{
    private readonly Mock<ISnifferService> _mockSnifferService;
    private readonly Mock<IPacketCollectionService> _mockCollectionService;
    private readonly Mock<IEventAggregator> _mockEventAggregator;
    private readonly Mock<ILoggerService> _mockLoggerService;
    private readonly Mock<CaptureStateChangedEvent> _mockCaptureStateEvent;
    private readonly Mock<ClearPacketsEvent> _mockClearPacketsEvent;
    private readonly Mock<FilterChangedEvent> _mockFilterChangedEvent;
    private readonly Mock<AutoScrollChangedEvent> _mockAutoScrollChangedEvent;
    private readonly Mock<ShowMacAddressesChangedEvent> _mockShowMacAddressesChangedEvent;
    private readonly Mock<TimeFormatChangedEvent> _mockTimeFormatChangedEvent;
    private readonly Mock<PacketCapturedEvent> _mockPacketCapturedEvent;

    public CaptureViewModelTests()
    {
        _mockSnifferService = new Mock<ISnifferService>();
        _mockCollectionService = new Mock<IPacketCollectionService>();
        _mockEventAggregator = new Mock<IEventAggregator>();
        _mockLoggerService = new Mock<ILoggerService>();
        _mockCaptureStateEvent = new Mock<CaptureStateChangedEvent>();
        _mockClearPacketsEvent = new Mock<ClearPacketsEvent>();
        _mockFilterChangedEvent = new Mock<FilterChangedEvent>();
        _mockAutoScrollChangedEvent = new Mock<AutoScrollChangedEvent>();
        _mockShowMacAddressesChangedEvent = new Mock<ShowMacAddressesChangedEvent>();
        _mockTimeFormatChangedEvent = new Mock<TimeFormatChangedEvent>();
        _mockPacketCapturedEvent = new Mock<PacketCapturedEvent>();

        _mockSnifferService
            .Setup(s => s.Devices)
            .Returns(new ObservableCollection<NetworkDevice>());

        // Setup all event aggregator subscriptions
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<CaptureStateChangedEvent>())
            .Returns(_mockCaptureStateEvent.Object);
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<ClearPacketsEvent>())
            .Returns(_mockClearPacketsEvent.Object);
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<FilterChangedEvent>())
            .Returns(_mockFilterChangedEvent.Object);
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<AutoScrollChangedEvent>())
            .Returns(_mockAutoScrollChangedEvent.Object);
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<ShowMacAddressesChangedEvent>())
            .Returns(_mockShowMacAddressesChangedEvent.Object);
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<TimeFormatChangedEvent>())
            .Returns(_mockTimeFormatChangedEvent.Object);
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<PacketCapturedEvent>())
            .Returns(_mockPacketCapturedEvent.Object);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSnifferServiceIsNull()
    {
        // Arrange & Act
        var action = () => new CaptureViewModel(
            null!, 
            _mockCollectionService.Object, 
            _mockEventAggregator.Object,
            _mockLoggerService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("snifferService");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenCollectionServiceIsNull()
    {
        // Arrange & Act
        var action = () => new CaptureViewModel(
            _mockSnifferService.Object, 
            null!, 
            _mockEventAggregator.Object,
            _mockLoggerService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("collectionService");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenEventAggregatorIsNull()
    {
        // Arrange & Act
        var action = () => new CaptureViewModel(
            _mockSnifferService.Object, 
            _mockCollectionService.Object, 
            null!,
            _mockLoggerService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("eventAggregator");
    }

    [Fact]
    public void Constructor_ShouldInitializeCommands()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.ToggleCaptureCommand.Should().NotBeNull();
        viewModel.ClearCommand.Should().NotBeNull();
        viewModel.SaveToDashboardCommand.Should().NotBeNull();
        viewModel.CopyCommand.Should().NotBeNull();
    }

    [Fact]
    public void Devices_ShouldReturnDevicesFromSnifferService()
    {
        // Arrange
        var devices = new ObservableCollection<NetworkDevice>
        {
            new() { Index = 0, Name = "eth0" },
            new() { Index = 1, Name = "wlan0" }
        };
        _mockSnifferService.Setup(s => s.Devices).Returns(devices);

        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Devices.Should().BeSameAs(devices);
    }

    [Fact]
    public void Packets_ShouldBeInitializedAsEmptyCollection()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Packets.Should().NotBeNull();
        viewModel.Packets.Should().BeEmpty();
    }

    [Fact]
    public void SelectedDevice_ShouldNotifyPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChanged = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.SelectedDevice))
                propertyChanged = true;
        };
        var device = new NetworkDevice { Index = 0, Name = "eth0" };

        // Act
        viewModel.SelectedDevice = device;

        // Assert
        propertyChanged.Should().BeTrue();
        viewModel.SelectedDevice.Should().Be(device);
    }

    [Fact]
    public void FilterText_ShouldNotifyPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChanged = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.FilterText))
                propertyChanged = true;
        };

        // Act
        viewModel.FilterText = "tcp";

        // Assert
        propertyChanged.Should().BeTrue();
        viewModel.FilterText.Should().Be("tcp");
    }

    [Fact]
    public void IsCapturing_ShouldNotifyPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChanged = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.IsCapturing))
                propertyChanged = true;
        };

        // Act
        viewModel.IsCapturing = true;

        // Assert
        propertyChanged.Should().BeTrue();
        viewModel.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public void PacketHexDump_ShouldNotifyPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChanged = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.PacketHexDump))
                propertyChanged = true;
        };

        // Act
        viewModel.PacketHexDump = "00 01 02 03";

        // Assert
        propertyChanged.Should().BeTrue();
        viewModel.PacketHexDump.Should().Be("00 01 02 03");
    }

    [Fact]
    public void SelectedPacket_ShouldNotifyPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChanged = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.SelectedPacket))
                propertyChanged = true;
        };
        var packet = new PacketInfo { Number = 1 };

        // Act
        viewModel.SelectedPacket = packet;

        // Assert
        propertyChanged.Should().BeTrue();
        viewModel.SelectedPacket.Should().Be(packet);
    }

    [Fact]
    public void Dispose_ShouldNotThrowException()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var action = () => viewModel.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    private CaptureViewModel CreateViewModel()
    {
        return new CaptureViewModel(
            _mockSnifferService.Object,
            _mockCollectionService.Object,
            _mockEventAggregator.Object,
            _mockLoggerService.Object);
    }
}
