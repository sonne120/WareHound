using Avalonia.Controls;
using Avalonia.Media;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
    }

    private void LogGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is LogEntry logEntry)
        {
            e.Row.Foreground = logEntry.Level switch
            {
                LogLevel.Error => new SolidColorBrush(Color.Parse("#E74C3C")),
                LogLevel.Warning => new SolidColorBrush(Color.Parse("#F39C12")),
                LogLevel.Debug => new SolidColorBrush(Color.Parse("#999999")),
                _ => new SolidColorBrush(Color.Parse("#333333"))
            };

            e.Row.FontWeight = logEntry.Level == LogLevel.Error
                ? FontWeight.Medium
                : FontWeight.Normal;
        }
    }
}
