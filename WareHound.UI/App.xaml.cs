using System.Windows;
using Prism.DryIoc;
using Prism.Ioc;
using WareHound.UI.Infrastructure.DependencyInjection;
using WareHound.UI.Services;
using WareHound.UI.Views;

namespace WareHound.UI;

public partial class App : PrismApplication
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Unhandled exception: {ex?.Message}\n\n{ex?.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show($"UI exception: {args.Exception.Message}\n\n{args.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        
        base.OnStartup(e);
    }

    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.AddApplicationServices();
        containerRegistry.AddViewModels();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var sniffer = Container.Resolve<ISnifferService>();
        sniffer.Dispose();
        base.OnExit(e);
    }
}
