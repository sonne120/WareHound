using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using WareHound.Avalonia.Infrastructure.Events;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly DispatcherTimer _statusTimer;

    [Reactive] public string StatusText { get; set; } = "Ready";
    [Reactive] public int PacketCount { get; set; }
    [Reactive] public DateTime CurrentTime { get; set; } = DateTime.Now;
    [Reactive] public int SelectedViewIndex { get; set; }
    [Reactive] public bool IsSidebarExpanded { get; set; } = true;
    [Reactive] public string FilterText { get; set; } = "";
    [Reactive] public FilterTypeOption? SelectedFilterType { get; set; }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; } = new()
    {
        new MenuItemViewModel { IconKey = "CaptureIcon", Title = "Capture", ViewIndex = 0 },
        new MenuItemViewModel { IconKey = "DashboardIcon", Title = "Dashboard", ViewIndex = 1 },
        new MenuItemViewModel { IconKey = "StatisticsIcon", Title = "Statistics", ViewIndex = 2 },
        new MenuItemViewModel { IconKey = "LogIcon", Title = "Logs", ViewIndex = 3 },
        new MenuItemViewModel { IconKey = "SettingsIcon", Title = "Settings", ViewIndex = 4, IconSize = 20 }
    };

    public ObservableCollection<FilterTypeOption> FilterTypes { get; } = new()
    {
        new FilterTypeOption { Type = FilterType.All, DisplayName = "All Fields" },
        new FilterTypeOption { Type = FilterType.Protocol, DisplayName = "Protocol" },
        new FilterTypeOption { Type = FilterType.SourceIP, DisplayName = "Source IP" },
        new FilterTypeOption { Type = FilterType.DestIP, DisplayName = "Dest IP" },
        new FilterTypeOption { Type = FilterType.SourcePort, DisplayName = "Source Port" },
        new FilterTypeOption { Type = FilterType.DestPort, DisplayName = "Dest Port" }
    };

    public CaptureViewModel CaptureViewModel { get; }
    public DashboardViewModel DashboardViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }
    public LogViewModel LogViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

    public ObservableCollection<NetworkDevice> Devices => CaptureViewModel.Devices;

    public NetworkDevice? SelectedDevice
    {
        get => CaptureViewModel.SelectedDevice;
        set
        {
            if (CaptureViewModel.SelectedDevice != value)
            {
                CaptureViewModel.SelectedDevice = value;
                this.RaisePropertyChanged();
            }
        }
    }

    public bool IsCapturing => CaptureViewModel.IsCapturing;
    public bool IsLoadingDevices => CaptureViewModel.IsLoadingDevices;
    public string DeviceLoadError => CaptureViewModel.ErrorMessage;
    public bool HasDeviceLoadError => !string.IsNullOrEmpty(DeviceLoadError);
    public bool IsFilterTypeSelected => SelectedFilterType != null && SelectedFilterType.Type != FilterType.All;

    public string FilterPlaceholder => SelectedFilterType?.Type switch
    {
        FilterType.Protocol => "e.g. TCP, UDP, HTTP",
        FilterType.SourceIP => "e.g. 192.168.1.1",
        FilterType.DestIP => "e.g. 10.0.0.1",
        FilterType.SourcePort => "e.g. 443, 80",
        FilterType.DestPort => "e.g. 8080",
        _ => "Enter filter value..."
    };

    public ReactiveCommand<int, Unit> NavigateCommand { get; }
    public ReactiveCommand<Unit, Unit> StartCaptureCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCaptureCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSidebarCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFilterCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenPcapCommand { get; }
    public ReactiveCommand<Unit, Unit> SavePcapCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveToDashboardCommand { get; }

    public MainWindowViewModel(
        CaptureViewModel captureViewModel,
        DashboardViewModel dashboardViewModel,
        StatisticsViewModel statisticsViewModel,
        LogViewModel logViewModel,
        SettingsViewModel settingsViewModel)
    {
        CaptureViewModel = captureViewModel;
        DashboardViewModel = dashboardViewModel;
        StatisticsViewModel = statisticsViewModel;
        LogViewModel = logViewModel;
        SettingsViewModel = settingsViewModel;

        SelectedFilterType = FilterTypes[0];

        CaptureViewModel.PropertyChanged += (s, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CaptureViewModel.IsCapturing):
                    this.RaisePropertyChanged(nameof(IsCapturing));
                    StatusText = CaptureViewModel.IsCapturing ? "Capturing..." : "Ready";

                    if (CaptureViewModel.IsCapturing)
                    {
                        StatisticsViewModel.OnCaptureStarted();
                    }
                    else
                    {
                        StatisticsViewModel.OnCaptureStopped();
                    }
                    break;
                case nameof(CaptureViewModel.IsLoadingDevices):
                    this.RaisePropertyChanged(nameof(IsLoadingDevices));
                    break;
                case nameof(CaptureViewModel.SelectedDevice):
                    this.RaisePropertyChanged(nameof(SelectedDevice));
                    break;
                case nameof(CaptureViewModel.ErrorMessage):
                    this.RaisePropertyChanged(nameof(DeviceLoadError));
                    this.RaisePropertyChanged(nameof(HasDeviceLoadError));
                    break;
            }
        };

        // Subscribe to batch events for statistics (bulk operation friendly)
        CaptureViewModel.PacketBatchReceived += batch =>
        {
            StatisticsViewModel.OnPacketsCaptured(batch);
        };

        CaptureViewModel.SaveToDashboardRequested += (name, packets) =>
        {
            DashboardViewModel.SaveCollection(name, packets);
        };

        this.WhenAnyValue(x => x.SelectedFilterType)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsFilterTypeSelected));
                this.RaisePropertyChanged(nameof(FilterPlaceholder));
                ApplyFilter();
            })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.FilterText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter())
            .DisposeWith(Disposables);

        NavigateCommand = ReactiveCommand.Create<int>(Navigate);

        var canStartCapture = this.WhenAnyValue(
            x => x.SelectedDevice,
            x => x.IsCapturing,
            x => x.IsLoadingDevices,
            (device, capturing, loading) => device != null && !capturing && !loading);
        StartCaptureCommand = ReactiveCommand.Create(StartCapture, canStartCapture);

        var canStopCapture = this.WhenAnyValue(x => x.IsCapturing);
        StopCaptureCommand = ReactiveCommand.Create(StopCapture, canStopCapture);

        ClearCommand = ReactiveCommand.Create(Clear);
        ToggleSidebarCommand = ReactiveCommand.Create(ToggleSidebar);
        ClearFilterCommand = ReactiveCommand.Create(ClearFilter);
        OpenPcapCommand = ReactiveCommand.Create(OpenPcap);
        SavePcapCommand = ReactiveCommand.Create(SavePcap);

        var hasPackets = this.WhenAnyValue(x => x.PacketCount, count => count > 0);
        SaveToDashboardCommand = ReactiveCommand.Create(SaveToDashboard, hasPackets);

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += OnStatusTimerTick;
        _statusTimer.Start();
    }

    private void Navigate(int viewIndex)
    {
        SelectedViewIndex = viewIndex;
    }

    private void StartCapture()
    {
        if (SelectedDevice == null) return;
        CaptureViewModel.StartCapture();
    }

    private void StopCapture()
    {
        CaptureViewModel.StopCapture();
    }

    private void Clear()
    {
        PacketCount = 0;
        Publish(new ClearPacketsEvent());
        StatisticsViewModel.ClearCommand.Execute().Subscribe();
    }

    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    private void ClearFilter()
    {
        SelectedFilterType = FilterTypes[0];
        FilterText = "";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var type = SelectedFilterType?.Type ?? FilterType.All;
        Publish(new FilterChangedEvent(new FilterCriteria(type, FilterText)));
    }

    private void OpenPcap()
    {
    }

    private void SavePcap()
    {
    }

    private void SaveToDashboard()
    {
        if (CaptureViewModel.Packets.Count == 0) return;

        var name = $"Capture {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        DashboardViewModel.SaveCollection(name, CaptureViewModel.Packets);
    }

    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        CurrentTime = DateTime.Now;
        PacketCount = CaptureViewModel.Packets.Count;
    }
}

public class MenuItemViewModel : ViewModelBase
{
    [Reactive] public string IconKey { get; set; } = "";
    [Reactive] public string Title { get; set; } = "";
    [Reactive] public int ViewIndex { get; set; }
    [Reactive] public int IconSize { get; set; } = 18;
}

public class FilterTypeOption
{
    public FilterType Type { get; set; }
    public string DisplayName { get; set; } = "";
    public override string ToString() => DisplayName;
}
