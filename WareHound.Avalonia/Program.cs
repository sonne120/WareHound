using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WareHound.Avalonia.Infrastructure;

namespace WareHound.Avalonia;

class Program
{
    public static IHost? AppHost { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        AppHost = Host.CreateApplicationBuilder(args)
            .ConfigureServices()
            .Build();
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        
        AppHost.Dispose();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // macOS: native popup windows open invisibly for this window (custom chrome),
            // so ComboBox lists couldn't be clicked; render popups inside the main window instead
            .With(new AvaloniaNativePlatformOptions { OverlayPopups = true })
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}

public static class HostBuilderExtensions
{
    public static HostApplicationBuilder ConfigureServices(this HostApplicationBuilder builder)
    {
        builder.Services.AddWareHoundServices();
        return builder;
    }
}
