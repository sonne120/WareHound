using Microsoft.Win32;
using Prism.Commands;
using Prism.Events;
using Prism.Regions;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using WareHound.UI.Infrastructure.Events;
using WareHound.UI.Infrastructure.Services;
using WareHound.UI.Infrastructure.ViewModels;
using WareHound.UI.Models;
using WareHound.UI.Services;

namespace WareHound.UI.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly IRegionManager _regionManager;
        private readonly ISnifferService _snifferService;
        private readonly PcapFileServiceFactory _pcapFactory;
        private readonly DispatcherTimer _statusTimer;

        private string _statusText = "Ready";
        private int _packetCount;
        private DateTime _currentTime;
        private bool _isCapturing;
        private NetworkDevice? _selectedDevice;
        private bool _isLoadingDevices;
        private string _deviceLoadError = "";
        private bool _isSavingOrLoading;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public int PacketCount
        {
            get => _packetCount;
            set => SetProperty(ref _packetCount, value);
        }

        public DateTime CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public bool IsCapturing
        {
            get => _isCapturing;
            set => SetProperty(ref _isCapturing, value);
        }

        public bool IsLoadingDevices
        {
            get => _isLoadingDevices;
            set => SetProperty(ref _isLoadingDevices, value);
        }

        public string DeviceLoadError
        {
            get => _deviceLoadError;
            set
            {
                if (SetProperty(ref _deviceLoadError, value))
                {
                    RaisePropertyChanged(nameof(HasDeviceLoadError));
                }
            }
        }

        public bool HasDeviceLoadError => !string.IsNullOrEmpty(DeviceLoadError);

        public NetworkDevice? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value) && value != null)
                {
                    _snifferService.SelectDevice(value.Index);
                }
            }
        }

        public ObservableCollection<NetworkDevice> Devices => _snifferService.Devices;
        public DelegateCommand<string> NavigateCommand { get; }
        public DelegateCommand StartCaptureCommand { get; }
        public DelegateCommand StopCaptureCommand { get; }
        public DelegateCommand ClearCommand { get; }
        public DelegateCommand RetryLoadDevicesCommand { get; }
        public DelegateCommand ClearFilterCommand { get; }
        public DelegateCommand OpenPcapCommand { get; }
        public DelegateCommand SavePcapCommand { get; }

        public bool IsSavingOrLoading
        {
            get => _isSavingOrLoading;
            set => SetProperty(ref _isSavingOrLoading, value);
        }

        // Filter type options for ComboBox
        public ObservableCollection<FilterTypeOption> FilterTypes { get; } = new()
        {
            new FilterTypeOption { Type = FilterType.All, DisplayName = "All Fields" },
            new FilterTypeOption { Type = FilterType.Protocol, DisplayName = "Protocol" },
            new FilterTypeOption { Type = FilterType.SourceIP, DisplayName = "Source IP" },
            new FilterTypeOption { Type = FilterType.DestIP, DisplayName = "Dest IP" },
            new FilterTypeOption { Type = FilterType.SourcePort, DisplayName = "Source Port" },
            new FilterTypeOption { Type = FilterType.DestPort, DisplayName = "Dest Port" }
        };

        private FilterTypeOption? _selectedFilterType;
        public FilterTypeOption? SelectedFilterType
        {
            get => _selectedFilterType;
            set
            {
                if (SetProperty(ref _selectedFilterType, value))
                {
                    RaisePropertyChanged(nameof(IsFilterTypeSelected));
                    RaisePropertyChanged(nameof(FilterPlaceholder));
                    PublishFilter();
                }
            }
        }

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

        private string _filterText = "";
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    PublishFilter();
                }
            }
        }

        private void PublishFilter()
        {
            var criteria = new FilterCriteria
            {
                Type = SelectedFilterType?.Type ?? FilterType.All,
                Value = FilterText
            };
            Publish<FilterChangedEvent, FilterCriteria>(criteria);
        }

        public MainWindowViewModel(IRegionManager regionManager, ISnifferService snifferService, 
            PcapFileServiceFactory pcapFactory, IEventAggregator eventAggregator, ILoggerService logger)
            : base(eventAggregator, logger)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _snifferService = snifferService ?? throw new ArgumentNullException(nameof(snifferService));
            _pcapFactory = pcapFactory ?? throw new ArgumentNullException(nameof(pcapFactory));

            NavigateCommand = new DelegateCommand<string>(Navigate);
            StartCaptureCommand = new DelegateCommand(StartCapture, CanStartCapture)
                .ObservesProperty(() => IsCapturing)
                .ObservesProperty(() => SelectedDevice)
                .ObservesProperty(() => IsLoadingDevices);
            StopCaptureCommand = new DelegateCommand(StopCapture, CanStopCapture)
                .ObservesProperty(() => IsCapturing);
            ClearCommand = new DelegateCommand(ClearPackets);
            RetryLoadDevicesCommand = new DelegateCommand(async () => await LoadDevicesAsync(), () => !IsLoadingDevices)
                .ObservesProperty(() => IsLoadingDevices);
            ClearFilterCommand = new DelegateCommand(ClearFilter);
            OpenPcapCommand = new DelegateCommand(async () => await OpenPcapAsync(), () => !IsCapturing && !IsSavingOrLoading)
                .ObservesProperty(() => IsCapturing)
                .ObservesProperty(() => IsSavingOrLoading);
            SavePcapCommand = new DelegateCommand(async () => await SavePcapAsync(), () => PacketCount > 0 && !IsCapturing && !IsSavingOrLoading)
                .ObservesProperty(() => PacketCount)
                .ObservesProperty(() => IsCapturing)
                .ObservesProperty(() => IsSavingOrLoading);

            // Subscribe to packet events for counter display
            Subscribe<PacketCapturedEvent, PacketInfo>(OnPacketReceived);

            // Status update timer
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statusTimer.Tick += OnStatusTimerTick;
            _statusTimer.Start();

            CurrentTime = DateTime.Now;

            // Load devices asynchronously after UI is ready
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadDevicesAsync();
        }

        private async Task LoadDevicesAsync()
        {
            IsLoadingDevices = true;
            DeviceLoadError = "";
            Publish<DevicesLoadingEvent, bool>(true);

            try
            {
                await _snifferService.LoadDevicesAsync(TimeSpan.FromSeconds(30));
                
                // Select first device after successful load
                if (Devices.Count > 0 && SelectedDevice == null)
                {
                    SelectedDevice = Devices[0];
                }
                
                Publish<DevicesLoadedEvent>();
            }
            catch (TimeoutException ex)
            {
                DeviceLoadError = "Device loading timed out. Please retry.";
                Publish<DevicesLoadFailedEvent, string>(ex.Message);
            }
            catch (OperationCanceledException)
            {
                // Cancelled - don't show error
            }
            catch (Exception ex)
            {
                DeviceLoadError = $"Failed to load devices: {ex.Message}";
                Publish<DevicesLoadFailedEvent, string>(ex.Message);
            }
            finally
            {
                IsLoadingDevices = false;
                Publish<DevicesLoadingEvent, bool>(false);
            }
        }

        private void ClearPackets()
        {
            Publish<ClearPacketsEvent>();
            PacketCount = 0;
        }

        private void Navigate(string viewName)
        {
            if (string.IsNullOrEmpty(viewName)) return;

            _regionManager.RequestNavigate("ContentRegion", viewName);
        }

        private void StartCapture()
        {
            if (SelectedDevice == null || IsCapturing) return;

            _snifferService.StartCapture();
            
            if (_snifferService.IsCapturing)
            {
                IsCapturing = true;
                Publish<CaptureStateChangedEvent, bool>(true);
            }
        }

        private bool CanStartCapture() => SelectedDevice != null && !IsCapturing && !IsLoadingDevices;

        private void StopCapture()
        {
            if (!IsCapturing) return;

            _snifferService.StopCapture();
            IsCapturing = false;
            Publish<CaptureStateChangedEvent, bool>(false);
        }

        private bool CanStopCapture() => IsCapturing;

        private void OnPacketReceived(PacketInfo packet)
        {
            // Event is already published from UI thread
            PacketCount++;
        }

        private void ClearFilter()
        {
            SelectedFilterType = FilterTypes[0]; // Reset to "All Fields"
            FilterText = "";
        }

        private void OnStatusTimerTick(object? sender, EventArgs e)
        {
            CurrentTime = DateTime.Now;
            IsCapturing = _snifferService.IsCapturing;
            StatusText = IsCapturing ? "Capturing..." : "Ready";
        }

        private async Task OpenPcapAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PCAP Files (*.pcap;*.pcapng;*.cap)|*.pcap;*.pcapng;*.cap|All Files (*.*)|*.*",
                Title = "Open Packet Capture File"
            };

            if (dialog.ShowDialog() != true) return;

            IsSavingOrLoading = true;
            StatusText = "Loading...";

            try
            {
                var service = _pcapFactory.GetService();
                var packets = await service.LoadAsync(dialog.FileName);
                
                // Publish event to load packets into CaptureViewModel
                Publish<PcapLoadedEvent, IList<PacketInfo>>(packets);
                
                PacketCount = packets.Count;
                StatusText = $"Loaded {packets.Count} packets";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open PCAP file:\n{ex.Message}", 
                    "Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
                StatusText = "Load failed";
            }
            finally
            {
                IsSavingOrLoading = false;
            }
        }

        private async Task SavePcapAsync()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PCAP File (*.pcap)|*.pcap|PCAPNG File (*.pcapng)|*.pcapng",
                Title = "Save Packet Capture",
                FileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.pcap"
            };

            if (dialog.ShowDialog() != true) return;

            IsSavingOrLoading = true;
            StatusText = "Saving...";

            try
            {
                // Request packets from CaptureViewModel via event
                var packetsTask = new TaskCompletionSource<IList<PacketInfo>>();
                
                // Subscribe to response
                Subscribe<PcapSaveResponseEvent, IList<PacketInfo>>(packets =>
                {
                    packetsTask.TrySetResult(packets);
                });
                
                // Request packets
                Publish<PcapSaveRequestEvent>();
                
                // Wait for response with timeout
                var packets = await Task.WhenAny(packetsTask.Task, Task.Delay(5000)) == packetsTask.Task
                    ? packetsTask.Task.Result
                    : new List<PacketInfo>();

                if (packets.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "No packets with raw data to save. Packets must have been captured (not loaded from metadata).",
                        "Warning",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var service = _pcapFactory.GetService();
                await service.SaveAsync(dialog.FileName, packets);
                
                StatusText = $"Saved {packets.Count} packets";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save PCAP file:\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                StatusText = "Save failed";
            }
            finally
            {
                IsSavingOrLoading = false;
            }
        }

        protected override void OnDispose()
        {
            _statusTimer.Stop();
            //_snifferService.PacketReceived -= OnPacketReceived;
        }
    }

    public class FilterTypeOption
    {
        public FilterType Type { get; set; }
        public string DisplayName { get; set; } = "";

        public override string ToString() => DisplayName;
    }
}
