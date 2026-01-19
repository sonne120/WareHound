using Prism.Mvvm;

namespace WareHound.UI.ViewModels
{
    public class SettingsViewModel : BindableBase
    {

        private bool _darkModeEnabled;
        private int _maxPacketBuffer = 10000;
        private bool _autoScroll = true;
        private string _captureFilter = "";

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
            set => SetProperty(ref _autoScroll, value);
        }

        public string CaptureFilter
        {
            get => _captureFilter;
            set => SetProperty(ref _captureFilter, value);
        }



        public SettingsViewModel()
        {
            
        }
    }
}
