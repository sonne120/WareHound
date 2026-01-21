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
        private readonly DispatcherTimer _statusTimer;

        private string _statusText = "Ready";
        private int _packetCount;
        private DateTime _currentTime;
        private bool _isCapturing;
        private NetworkDevice? _selectedDevice;
        private bool _isLoadingDevices;
        private string _deviceLoadError = "";

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

        private string _filterText = "";
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    Publish<FilterChangedEvent, string>(value);
                }
            }
        }

        public MainWindowViewModel(IRegionManager regionManager, ISnifferService snifferService, IEventAggregator eventAggregator, ILoggerService logger)
            : base(eventAggregator, logger)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _snifferService = snifferService ?? throw new ArgumentNullException(nameof(snifferService));

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

        private void OnStatusTimerTick(object? sender, EventArgs e)
        {
            CurrentTime = DateTime.Now;
            IsCapturing = _snifferService.IsCapturing;
            StatusText = IsCapturing ? "Capturing..." : "Ready";
        }

        protected override void OnDispose()
        {
            _statusTimer.Stop();
            //_snifferService.PacketReceived -= OnPacketReceived;
        }
    }
}
