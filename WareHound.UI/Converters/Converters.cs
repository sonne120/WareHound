using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WareHound.UI.Converters;

public class ProtocolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var protocol = value?.ToString()?.ToUpper() ?? "";
        return protocol switch
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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        var invert = parameter?.ToString() == "Invert";
        if (invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isNull = value == null;
        var invert = parameter?.ToString() == "Invert";
        if (invert) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool capturing && capturing ? "Stop" : "Start";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            // Max width of progress bar is approximately 200 pixels
            return Math.Max(0, Math.Min(200, percent * 2));
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns true only if all input boolean values are false.
/// Useful for enabling controls when multiple conditions are all false.
/// </summary>
public class AllFalseConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        foreach (var value in values)
        {
            if (value is bool b && b)
                return false;
        }
        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
