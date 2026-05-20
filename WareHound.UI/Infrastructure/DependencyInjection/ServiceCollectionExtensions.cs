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
            containerRegistry.RegisterSingleton<ILoggerService, DebugLoggerService>();

            containerRegistry.RegisterSingleton<ISnifferInterop, SnifferInterop>();


            containerRegistry.RegisterSingleton<ISnifferService, SnifferService>();
            containerRegistry.RegisterSingleton<IPacketCollectionService, PacketCollectionService>();


            containerRegistry.Register<NativePcapFileService>();
            containerRegistry.Register<SharpPcapFileService>();


            containerRegistry.RegisterSingleton<PcapFileServiceFactory>();


            containerRegistry.RegisterSingleton<ICaptureSessionFacade, CaptureSessionFacade>();

            containerRegistry.RegisterSingleton<IStatisticsChannel, StatisticsChannel>();


            containerRegistry.Register<MainWindow>();
        }

        public static void AddViewModels(this IContainerRegistry containerRegistry)
        {

            containerRegistry.RegisterForNavigation<CaptureView, CaptureViewModel>();
            containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();
            containerRegistry.RegisterForNavigation<StatisticsView, StatisticsViewModel>();
            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();
            containerRegistry.RegisterForNavigation<LogView, LogViewModel>();
        }
    }
}
