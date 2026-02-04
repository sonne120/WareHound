using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WareHound.Avalonia.Converters;

public class ProtocolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var protocol = value?.ToString()?.ToUpper() ?? "";
        return protocol switch
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

            _ => new SolidColorBrush(Colors.Transparent)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        var invert = parameter?.ToString() == "Invert";
        if (invert) boolValue = !boolValue;
        return boolValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value == null;
        var invert = parameter?.ToString() == "Invert";
        if (invert) isNull = !isNull;
        return !isNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool capturing && capturing ? "Stop" : "Start";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b ? false : true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return Math.Max(0, Math.Min(200, percent * 2));
        }
        return 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class AllFalseConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        foreach (var value in values)
        {
            if (value is bool b && b)
                return false;
        }
        return true;
    }
}

public class IntToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out int expected))
        {
            return index == expected;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr && int.TryParse(paramStr, out int index))
        {
            return index;
        }
        return AvaloniaProperty.UnsetValue;
    }
}

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out int expected))
        {
            return index == expected;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PercentageToWidthConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 &&
            values[0] is double percentage &&
            values[1] is double containerWidth)
        {
            return Math.Max(0, (percentage / 100.0) * containerWidth);
        }
        return 0.0;
    }
}

public class StringToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && hex.StartsWith("#") && hex.Length >= 7)
        {
            try
            {
                return Color.Parse(hex);
            }
            catch
            {
                return Colors.Gray;
            }
        }
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LessThanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double val && parameter is string paramStr && double.TryParse(paramStr, out double threshold))
        {
            return val < threshold;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = 0;
        if (value is int i) count = i;
        else if (value is long l) count = (int)l;

        return count == 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
