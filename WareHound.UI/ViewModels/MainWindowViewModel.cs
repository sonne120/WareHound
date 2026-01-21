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
                .ObservesProperty(() => SelectedDevice);
            StopCaptureCommand = new DelegateCommand(StopCapture, CanStopCapture)
                .ObservesProperty(() => IsCapturing);
            ClearCommand = new DelegateCommand(ClearPackets);

            // Subscribe to packet events
            //_snifferService.PacketReceived += OnPacketReceived;

            // Status update timer
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statusTimer.Tick += OnStatusTimerTick;
            _statusTimer.Start();

            CurrentTime = DateTime.Now;

            // Select first device 
            if (Devices.Count > 0)
                SelectedDevice = Devices[0];
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

        private bool CanStartCapture() => SelectedDevice != null && !IsCapturing;

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
            System.Windows.Application.Current.Dispatcher.Invoke(() => PacketCount++);
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
