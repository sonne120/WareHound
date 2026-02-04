using System;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WareHound.Avalonia.Models;
using WareHound.Avalonia.ViewModels;

namespace WareHound.Avalonia.Views;

public partial class CaptureView : UserControl
{
    public static FuncValueConverter<bool, string> CaptureStatusConverter { get; } =
        new(isCapturing => isCapturing ? "● Capturing" : "○ Idle");

    private CaptureViewModel? _viewModel;
    private DispatcherTimer? _scrollTimer;
    private int _lastKnownCount;

    public CaptureView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        PacketGrid.DoubleTapped += PacketGrid_DoubleTapped;

        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _scrollTimer.Tick += OnScrollTimerTick;
        _scrollTimer.Start();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as CaptureViewModel;
        _lastKnownCount = 0;
    }

    private void OnScrollTimerTick(object? sender, EventArgs e)
    {
        if (_viewModel?.AutoScroll != true || _viewModel.FilteredPackets.Count == 0)
            return;

        var currentCount = _viewModel.FilteredPackets.Count;
        if (currentCount > _lastKnownCount)
        {
            _lastKnownCount = currentCount;
            var lastItem = _viewModel.FilteredPackets[^1];
            PacketGrid.ScrollIntoView(lastItem, null);
        }
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
            // Transport layer
            "TCP" => new SolidColorBrush(Color.FromRgb(230, 230, 250)),      // Lavender
            "UDP" => new SolidColorBrush(Color.FromRgb(218, 232, 252)),      // Light Blue

            // Web protocols
            "HTTP" => new SolidColorBrush(Color.FromRgb(225, 245, 254)),     // Cyan tint
            "HTTPS" => new SolidColorBrush(Color.FromRgb(200, 230, 200)),    // Light Green
            "TLS" => new SolidColorBrush(Color.FromRgb(200, 230, 200)),      // Light Green
            "QUIC" => new SolidColorBrush(Color.FromRgb(209, 250, 229)),     // Mint

            // DNS/DHCP
            "DNS" => new SolidColorBrush(Color.FromRgb(232, 245, 233)),      // Pale Green
            "DHCP" => new SolidColorBrush(Color.FromRgb(255, 253, 231)),     // Light Yellow
            "NTP" => new SolidColorBrush(Color.FromRgb(254, 249, 195)),      // Cream

            // Network layer
            "ICMP" => new SolidColorBrush(Color.FromRgb(252, 228, 236)),     // Pink
            "ICMPV6" => new SolidColorBrush(Color.FromRgb(252, 228, 236)),   // Pink
            "ARP" => new SolidColorBrush(Color.FromRgb(255, 243, 224)),      // Peach

            // Remote access
            "SSH" => new SolidColorBrush(Color.FromRgb(224, 231, 255)),      // Indigo tint
            "RDP" => new SolidColorBrush(Color.FromRgb(237, 233, 254)),      // Violet tint
            "TELNET" => new SolidColorBrush(Color.FromRgb(243, 232, 255)),   // Purple tint

            // File transfer
            "FTP" => new SolidColorBrush(Color.FromRgb(254, 243, 199)),      // Amber tint
            "FTP-DATA" => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // Amber tint
            "TFTP" => new SolidColorBrush(Color.FromRgb(254, 243, 199)),     // Amber tint
            "SMB" => new SolidColorBrush(Color.FromRgb(254, 226, 226)),      // Red tint

            // Email
            "SMTP" => new SolidColorBrush(Color.FromRgb(254, 215, 170)),     // Orange tint
            "POP3" => new SolidColorBrush(Color.FromRgb(254, 215, 170)),     // Orange tint
            "IMAP" => new SolidColorBrush(Color.FromRgb(254, 215, 170)),     // Orange tint

            // Database
            "MYSQL" => new SolidColorBrush(Color.FromRgb(219, 234, 254)),    // Blue tint
            "POSTGRESQL" => new SolidColorBrush(Color.FromRgb(219, 234, 254)), // Blue tint
            "MSSQL" => new SolidColorBrush(Color.FromRgb(219, 234, 254)),    // Blue tint
            "REDIS" => new SolidColorBrush(Color.FromRgb(254, 202, 202)),    // Red light
            "MONGODB" => new SolidColorBrush(Color.FromRgb(209, 250, 229)),  // Mint

            // Directory/Auth
            "LDAP" => new SolidColorBrush(Color.FromRgb(229, 231, 235)),     // Gray tint
            "KERBEROS" => new SolidColorBrush(Color.FromRgb(229, 231, 235)), // Gray tint

            // IoT/Messaging
            "MQTT" => new SolidColorBrush(Color.FromRgb(187, 247, 208)),     // Emerald tint
            "SNMP" => new SolidColorBrush(Color.FromRgb(254, 240, 138)),     // Yellow
            "SYSLOG" => new SolidColorBrush(Color.FromRgb(254, 240, 138)),   // Yellow

            // VoIP/Media
            "SIP" => new SolidColorBrush(Color.FromRgb(251, 207, 232)),      // Pink tint
            "RTP" => new SolidColorBrush(Color.FromRgb(251, 207, 232)),      // Pink tint

            // VPN
            "OPENVPN" => new SolidColorBrush(Color.FromRgb(191, 219, 254)),  // Blue light
            "WIREGUARD" => new SolidColorBrush(Color.FromRgb(191, 219, 254)), // Blue light

            // Local network
            "NETBIOS" => new SolidColorBrush(Color.FromRgb(229, 231, 235)),  // Gray tint
            "MDNS" => new SolidColorBrush(Color.FromRgb(232, 245, 233)),     // Pale Green
            "LLMNR" => new SolidColorBrush(Color.FromRgb(232, 245, 233)),    // Pale Green

            // gRPC/WebSocket
            "GRPC" => new SolidColorBrush(Color.FromRgb(209, 250, 229)),     // Mint
            "WEBSOCKET" => new SolidColorBrush(Color.FromRgb(209, 250, 229)), // Mint

            // RADIUS
            "RADIUS" => new SolidColorBrush(Color.FromRgb(229, 231, 235)),   // Gray tint

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
