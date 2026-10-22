using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Media;

namespace WareHound.Avalonia.Controls;

public class PacketRateGauge : Control
{
    private const double StartAngle = 135;  
    private const double SweepAngle = 270;

    public static readonly DirectProperty<PacketRateGauge, double> ValueProperty =
        AvaloniaProperty.RegisterDirect<PacketRateGauge, double>(
            nameof(Value), o => o.Value, (o, v) => o.Value = v, defaultBindingMode: BindingMode.OneWay);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<PacketRateGauge, double>(nameof(Maximum), 1000, coerce: (_, v) => v > 0 ? v : 1);

    /// <summary>When true, <see cref="Maximum"/> grows to the next round number whenever the value exceeds it.</summary>
    public static readonly StyledProperty<bool> AutoRangeProperty =
        AvaloniaProperty.Register<PacketRateGauge, bool>(nameof(AutoRange), true);

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<PacketRateGauge, string>(nameof(Unit), "pps");

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<PacketRateGauge, double>(nameof(Thickness), 6);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<PacketRateGauge, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<PacketRateGauge, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> MarkerBrushProperty =
        AvaloniaProperty.Register<PacketRateGauge, IBrush?>(nameof(MarkerBrush));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<PacketRateGauge>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<PacketRateGauge>();

    private double _value;

    private Size _cachedSize;
    private Point _center;
    private double _radius;
    private StreamGeometry? _trackGeometry;
    private Pen? _trackPen;
    private Pen? _fillPen;
    private string? _valueText;
    private FormattedText? _valueFormatted;
    private string? _unitText;
    private FormattedText? _unitFormatted;

    static PacketRateGauge()
    {
        AffectsRender<PacketRateGauge>(
            MaximumProperty, UnitProperty, ThicknessProperty, TrackBrushProperty, FillBrushProperty,
            MarkerBrushProperty, ForegroundProperty, FontFamilyProperty);
    }

    /// <summary>Current rate. Values outside the range are clamped for drawing only.</summary>
    public double Value
    {
        get => _value;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
            if (!SetAndRaise(ValueProperty, ref _value, value)) return;

            if (AutoRange && value > Maximum)
            {
                SetCurrentValue(MaximumProperty, NiceCeiling(value));
            }

            InvalidateVisual();
        }
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public bool AutoRange
    {
        get => GetValue(AutoRangeProperty);
        set => SetValue(AutoRangeProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public IBrush? MarkerBrush
    {
        get => GetValue(MarkerBrushProperty);
        set => SetValue(MarkerBrushProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;

        var thickness = Thickness;
        UpdateGeometry(size, thickness);
        if (_radius <= 0) return;

        var track = GetPen(ref _trackPen, TrackBrush, thickness);
        if (track != null && _trackGeometry != null)
        {
            context.DrawGeometry(null, track, _trackGeometry);
        }

        var fraction = Math.Clamp(_value / Maximum, 0, 1);
        if (fraction > 0)
        {
            var fill = GetPen(ref _fillPen, FillBrush, thickness);
            if (fill != null)
            {
                context.DrawGeometry(null, fill, BuildArc(_center, _radius, StartAngle, SweepAngle * fraction));
            }
        }

        if (MarkerBrush is { } marker)
        {
            var angle = (StartAngle + SweepAngle * fraction) * Math.PI / 180;
            var tip = _center + new Vector(Math.Cos(angle), Math.Sin(angle)) * _radius;
            context.DrawEllipse(marker, null, tip, thickness * 0.75, thickness * 0.75);
        }

        DrawText(context);
    }

    private void UpdateGeometry(Size size, double thickness)
    {
        if (size == _cachedSize && _trackGeometry != null) return;

        // The 270° arc spans vertically from (cy - r) to (cy + r·sin 45°).
        var verticalSpan = 1 + Math.Sin(45 * Math.PI / 180);
        _radius = Math.Min(size.Width / 2, size.Height / verticalSpan) - thickness / 2 - 1;
        _center = new Point(size.Width / 2, thickness / 2 + 1 + _radius);
        _trackGeometry = _radius > 0 ? BuildArc(_center, _radius, StartAngle, SweepAngle) : null;
        _cachedSize = size;
        _valueFormatted = null;   // font size depends on the radius
        _unitFormatted = null;
    }

    private void DrawText(DrawingContext context)
    {
        var foreground = Foreground ?? Brushes.Black;
        var valueText = FormatValue(_value);
        if (_valueFormatted == null || valueText != _valueText)
        {
            _valueText = valueText;
            _valueFormatted = new FormattedText(valueText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, weight: FontWeight.SemiBold), Math.Max(9, _radius * 0.42), foreground);
        }

        var unit = Unit;
        if (_unitFormatted == null || unit != _unitText)
        {
            _unitText = unit;
            _unitFormatted = new FormattedText(unit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily), Math.Max(7, _radius * 0.26), foreground);
        }

        context.DrawText(_valueFormatted,
            new Point(_center.X - _valueFormatted.Width / 2, _center.Y - _valueFormatted.Height / 2));
        context.DrawText(_unitFormatted,
            new Point(_center.X - _unitFormatted.Width / 2, _center.Y + _radius * 0.55 - _unitFormatted.Height / 2));
    }

    private static Pen? GetPen(ref Pen? cache, IBrush? brush, double thickness)
    {
        if (brush == null) return null;
        if (cache == null || !ReferenceEquals(cache.Brush, brush) || cache.Thickness != thickness)
        {
            cache = new Pen(brush, thickness) { LineCap = PenLineCap.Round };
        }
        return cache;
    }

    private static StreamGeometry BuildArc(Point center, double radius, double startDeg, double sweepDeg)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        var start = PointOnCircle(center, radius, startDeg);
        var end = PointOnCircle(center, radius, startDeg + sweepDeg);
        ctx.BeginFigure(start, isFilled: false);
        ctx.ArcTo(end, new Size(radius, radius), 0, sweepDeg > 180, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var rad = degrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }

    private static string FormatValue(double v) => v switch
    {
        >= 1000 => v.ToString("N0", CultureInfo.CurrentCulture),
        >= 100 => v.ToString("F0", CultureInfo.CurrentCulture),
        _ => v.ToString("F1", CultureInfo.CurrentCulture)
    };

    /// <summary>Smallest value of the form 1, 2 or 5 × 10^n that is at least <paramref name="v"/>.</summary>
    private static double NiceCeiling(double v)
    {
        if (v <= 0) return 1;
        var exponent = Math.Pow(10, Math.Floor(Math.Log10(v)));
        var fraction = v / exponent;
        var nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * exponent;
    }
}
