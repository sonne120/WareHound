using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace WareHound.Avalonia.Controls;

public class ProtocolBadge : TemplatedControl
{
    public static readonly StyledProperty<string> ProtocolProperty =
        AvaloniaProperty.Register<ProtocolBadge, string>(
            nameof(Protocol),
            defaultValue: "TCP",
            coerce: CoerceProtocol);

    public static readonly StyledProperty<IBrush?> BadgeBrushProperty =
        AvaloniaProperty.Register<ProtocolBadge, IBrush?>(nameof(BadgeBrush));

    public string Protocol
    {
        get => GetValue(ProtocolProperty);
        set => SetValue(ProtocolProperty, value);
    }


    public IBrush? BadgeBrush
    {
        get => GetValue(BadgeBrushProperty);
        set => SetValue(BadgeBrushProperty, value);
    }

    private static string CoerceProtocol(AvaloniaObject _, string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
}
