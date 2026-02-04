using Microsoft.Extensions.DependencyInjection;
using WareHound.Avalonia.IPC;
using WareHound.Avalonia.Services;
using WareHound.Avalonia.ViewModels;

namespace WareHound.Avalonia.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWareHoundServices(this IServiceCollection services)
    {
        // Register core services
        services.AddSingleton<ILoggerService, DebugLoggerService>();

        // Register interop services
        services.AddSingleton<ISnifferInterop, SnifferInterop>();

        // Register sniffer service (C++ Native via Named Pipes)
        services.AddSingleton<ISnifferService, NativeSnifferService>();

        // Register file services
        services.AddSingleton<SharpPcapFileService>();
        services.AddSingleton<IPcapFileService>(sp => sp.GetRequiredService<SharpPcapFileService>());
        services.AddSingleton<PcapFileServiceFactory>();

        // Register facade (unified API for capture operations)
        services.AddSingleton<ICaptureSessionFacade, CaptureSessionFacade>();

        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<CaptureViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<LogViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
