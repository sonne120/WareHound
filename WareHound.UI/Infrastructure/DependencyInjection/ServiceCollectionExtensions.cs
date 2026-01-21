using Prism.Ioc;
using WareHound.UI.IPC;
using WareHound.UI.Infrastructure.Services;
using WareHound.UI.Services;
using WareHound.UI.ViewModels;
using WareHound.UI.Views;

namespace WareHound.UI.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static void AddApplicationServices(this IContainerRegistry containerRegistry)
        {
            // Register Infrastructure services
            containerRegistry.RegisterSingleton<ILoggerService, DebugLoggerService>();

            // Register IPC/Interop services
            containerRegistry.RegisterSingleton<ISnifferInterop, SnifferInterop>();

            // Register Application Services
            containerRegistry.RegisterSingleton<ISnifferService, SnifferService>();
            containerRegistry.RegisterSingleton<IPacketCollectionService, PacketCollectionService>();
            
            // Register PCAP file services (both backends available)
            containerRegistry.Register<NativePcapFileService>();
            containerRegistry.Register<SharpPcapFileService>();
            
            // Register factory for selecting PCAP backend based on settings
            containerRegistry.RegisterSingleton<PcapFileServiceFactory>();

            // Register Views
            containerRegistry.Register<MainWindow>();
        }

        public static void AddViewModels(this IContainerRegistry containerRegistry)
        {
            // Register ViewModels for navigation
            containerRegistry.RegisterForNavigation<CaptureView, CaptureViewModel>();
            containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();
            containerRegistry.RegisterForNavigation<StatisticsView, StatisticsViewModel>();
            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();
            containerRegistry.RegisterForNavigation<LogView, LogViewModel>();
        }
    }
}
