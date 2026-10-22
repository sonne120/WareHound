using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace WareHound.Avalonia.Controls;

public class TrafficSparkline : Control
{
    public static readonly StyledProperty<IEnumerable<double>?> ValuesProperty =
        AvaloniaProperty.Register<TrafficSparkline, IEnumerable<double>?>(nameof(Values));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<TrafficSparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<TrafficSparkline, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<TrafficSparkline, double>(nameof(StrokeThickness), 1.5);

    public static readonly StyledProperty<bool> ShowLastPointProperty =
        AvaloniaProperty.Register<TrafficSparkline, bool>(nameof(ShowLastPoint), true);

    private readonly List<double> _buffer = new();
    private INotifyCollectionChanged? _subscribed;
    private Pen? _pen;

    static TrafficSparkline()
    {
        AffectsRender<TrafficSparkline>(ValuesProperty, StrokeProperty, FillProperty, StrokeThicknessProperty, ShowLastPointProperty);
    }

    public IEnumerable<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool ShowLastPoint
    {
        get => GetValue(ShowLastPointProperty);
        set => SetValue(ShowLastPointProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty)
        {
            Unsubscribe();
            if (this.GetVisualRoot() != null) Subscribe();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (Values is INotifyCollectionChanged incc && !ReferenceEquals(incc, _subscribed))
        {
            Unsubscribe();
            _subscribed = incc;
            incc.CollectionChanged += OnValuesChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed == null) return;
        _subscribed.CollectionChanged -= OnValuesChanged;
        _subscribed = null;
    }

    private void OnValuesChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var values = Values;
        if (values == null) return;

        _buffer.Clear();
        _buffer.AddRange(values);
        var count = _buffer.Count;
        if (count == 0) return;

        var width = Bounds.Width;
        var height = Bounds.Height;
        var thickness = StrokeThickness;
        var pad = thickness + (ShowLastPoint ? 2 : 0);
        if (width <= 2 * pad || height <= 2 * pad) return;

        var max = 0.0;
        foreach (var v in _buffer) if (v > max) max = v;
        if (max <= 0) max = 1;

        var stepX = count > 1 ? (width - 2 * pad) / (count - 1) : 0;
        var usableHeight = height - 2 * pad;
        var baseline = height - pad;

        Point PointAt(int i) => new(pad + i * stepX, pad + usableHeight * (1 - Math.Clamp(_buffer[i], 0, max) / max));

        var first = PointAt(0);
        var last = PointAt(count - 1);

        if (Fill is { } fill)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(first.X, baseline), isFilled: true);
                for (var i = 0; i < count; i++) ctx.LineTo(PointAt(i));
                ctx.LineTo(new Point(last.X, baseline));
                ctx.EndFigure(true);
            }
            context.DrawGeometry(fill, null, area);
        }

        if (Stroke is { } stroke)
        {
            if (_pen == null || !ReferenceEquals(_pen.Brush, stroke) || _pen.Thickness != thickness)
            {
                _pen = new Pen(stroke, thickness) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
            }

            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                ctx.BeginFigure(first, isFilled: false);
                for (var i = 1; i < count; i++) ctx.LineTo(PointAt(i));
                ctx.EndFigure(false);
            }
            context.DrawGeometry(null, _pen, line);

            if (ShowLastPoint)
            {
                context.DrawEllipse(stroke, null, last, thickness + 1, thickness + 1);
            }
        }
    }
}
