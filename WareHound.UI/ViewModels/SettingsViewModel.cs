using Prism.Events;
using Prism.Mvvm;
using WareHound.UI.Infrastructure.Events;

namespace WareHound.UI.ViewModels
{
    public class SettingsViewModel : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;

        private bool _darkModeEnabled;
        private int _maxPacketBuffer = 10000;
        private bool _autoScroll = true;
        private bool _showMacAddresses = true;
        private string _captureFilter = "";
        private int _selectedTimeFormatIndex = 0;
        private int _selectedThemeIndex = 0;

        public string[] TimeFormats { get; } = { "Relative", "Absolute", "Delta" };
        public string[] Themes { get; } = { "Light", "Dark" };

        public bool DarkModeEnabled
        {
            get => _darkModeEnabled;
            set => SetProperty(ref _darkModeEnabled, value);
        }
        public int MaxPacketBuffer
        {
            get => _maxPacketBuffer;
            set => SetProperty(ref _maxPacketBuffer, value);
        }

        public bool AutoScroll
        {
            get => _autoScroll;
            set
            {
                if (SetProperty(ref _autoScroll, value))
                {
                    _eventAggregator.GetEvent<AutoScrollChangedEvent>().Publish(value);
                }
            }
        }

        public bool ShowMacAddresses
        {
            get => _showMacAddresses;
            set
            {
                if (SetProperty(ref _showMacAddresses, value))
                {
                    _eventAggregator.GetEvent<ShowMacAddressesChangedEvent>().Publish(value);
                }
            }
        }
        public int SelectedTimeFormatIndex
        {
            get => _selectedTimeFormatIndex;
            set
            {
                if (SetProperty(ref _selectedTimeFormatIndex, value))
                {
                    var format = (TimeFormatType)value;
                    _eventAggregator.GetEvent<TimeFormatChangedEvent>().Publish(format);
                }
            }
        }

        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set
            {
                if (SetProperty(ref _selectedThemeIndex, value))
                {
                    _darkModeEnabled = value == 1;
                    _eventAggregator.GetEvent<ThemeChangedEvent>().Publish(_darkModeEnabled);
                }
            }
        }
        public string CaptureFilter
        {
            get => _captureFilter;
            set => SetProperty(ref _captureFilter, value);
        }
        public SettingsViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }
    }
}
