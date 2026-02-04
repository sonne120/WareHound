using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        PacketGrid.DoubleTapped += PacketGrid_DoubleTapped;
    }

    private void PacketGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is PacketInfo packet)
        {
            e.Row.Background = GetProtocolColor(packet.Protocol);
            e.Row.Foreground = new SolidColorBrush(Color.Parse("#1E1E1E"));
        }
    }

    private static IBrush GetProtocolColor(string? protocol)
    {
        var upperProtocol = protocol?.ToUpperInvariant() ?? "";
        return upperProtocol switch
        {
            "TCP" => new SolidColorBrush(Color.FromRgb(230, 230, 250)),
            "TLS" => new SolidColorBrush(Color.FromRgb(200, 230, 200)),
            "HTTP" => new SolidColorBrush(Color.FromRgb(225, 245, 254)),
            "UDP" => new SolidColorBrush(Color.FromRgb(218, 232, 252)),
            "DNS" => new SolidColorBrush(Color.FromRgb(232, 245, 233)),
            "DHCP" => new SolidColorBrush(Color.FromRgb(255, 253, 231)),
            "ICMP" => new SolidColorBrush(Color.FromRgb(252, 228, 236)),
            "ARP" => new SolidColorBrush(Color.FromRgb(255, 243, 224)),
            _ => new SolidColorBrush(Colors.Transparent)
        };
    }

    private async void PacketGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control control)
        {
            var row = control.FindAncestorOfType<DataGridRow>();
            if (row?.DataContext is PacketInfo packet)
            {
                var parentWindow = this.FindAncestorOfType<Window>();
                var detailWindow = new PacketDetailWindow(packet);
                if (parentWindow != null)
                {
                    await detailWindow.ShowDialog(parentWindow);
                }
                else
                {
                    detailWindow.Show();
                }
            }
        }
    }
}
