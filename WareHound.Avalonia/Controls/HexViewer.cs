using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace WareHound.Avalonia.Controls;

public class HexViewer : Control, ILogicalScrollable
{
    private const int OffsetChars = 8;
    private const double PaddingX = 8;
    private const double PaddingY = 4;
    private const string HexDigits = "0123456789ABCDEF";

    public static readonly StyledProperty<byte[]?> DataProperty =
        AvaloniaProperty.Register<HexViewer, byte[]?>(nameof(Data));

    public static readonly StyledProperty<int> BytesPerRowProperty =
        AvaloniaProperty.Register<HexViewer, int>(nameof(BytesPerRow), 16, coerce: (_, v) => Math.Clamp(v, 1, 64));

    public static readonly StyledProperty<int> SelectedOffsetProperty =
        AvaloniaProperty.Register<HexViewer, int>(nameof(SelectedOffset), -1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<HexViewer>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<HexViewer>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<HexViewer>();

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<HexViewer>();

    public static readonly StyledProperty<IBrush?> OffsetForegroundProperty =
        AvaloniaProperty.Register<HexViewer, IBrush?>(nameof(OffsetForeground));

    public static readonly StyledProperty<IBrush?> AsciiForegroundProperty =
        AvaloniaProperty.Register<HexViewer, IBrush?>(nameof(AsciiForeground));

    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<HexViewer, IBrush?>(nameof(SelectionBrush));

    public static readonly StyledProperty<IBrush?> AlternateRowBrushProperty =
        AvaloniaProperty.Register<HexViewer, IBrush?>(nameof(AlternateRowBrush));

    /// <summary>Read-only text describing the selected byte, e.g. for a status line.</summary>
    public static readonly DirectProperty<HexViewer, string> SelectionDescriptionProperty =
        AvaloniaProperty.RegisterDirect<HexViewer, string>(nameof(SelectionDescription), o => o.SelectionDescription);

    private readonly StringBuilder _rowBuilder = new();
    private string _selectionDescription = "";

    private Typeface _typeface;
    private double _charWidth;
    private double _lineHeight;
    private bool _metricsValid;

    private Vector _offset;
    private Size _extent;
    private Size _viewport;
    private bool _extentChanged;

    static HexViewer()
    {
        FocusableProperty.OverrideDefaultValue<HexViewer>(true);
        AffectsMeasure<HexViewer>(DataProperty, BytesPerRowProperty, FontFamilyProperty, FontSizeProperty);
        AffectsRender<HexViewer>(
            SelectedOffsetProperty, ForegroundProperty, BackgroundProperty, OffsetForegroundProperty,
            AsciiForegroundProperty, SelectionBrushProperty, AlternateRowBrushProperty);
    }

    public HexViewer()
    {
        ClipToBounds = true;
    }

    public byte[]? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public int BytesPerRow
    {
        get => GetValue(BytesPerRowProperty);
        set => SetValue(BytesPerRowProperty, value);
    }

    /// <summary>Index of the selected byte, or -1 when nothing is selected.</summary>
    public int SelectedOffset
    {
        get => GetValue(SelectedOffsetProperty);
        set => SetValue(SelectedOffsetProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? OffsetForeground
    {
        get => GetValue(OffsetForegroundProperty);
        set => SetValue(OffsetForegroundProperty, value);
    }

    public IBrush? AsciiForeground
    {
        get => GetValue(AsciiForegroundProperty);
        set => SetValue(AsciiForegroundProperty, value);
    }

    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public IBrush? AlternateRowBrush
    {
        get => GetValue(AlternateRowBrushProperty);
        set => SetValue(AlternateRowBrushProperty, value);
    }

    public string SelectionDescription
    {
        get => _selectionDescription;
        private set => SetAndRaise(SelectionDescriptionProperty, ref _selectionDescription, value);
    }

    // ---- Layout of one row, in character cells -------------------------------------------
    // |offset  |hex ("XX " per byte, extra gap after every 8th)  |ascii|

    private int RowCount => Data is { Length: > 0 } d ? (d.Length + BytesPerRow - 1) / BytesPerRow : 0;
    private static int HexStart => OffsetChars + 2;
    private static int HexCellChar(int col) => HexStart + col * 3 + col / 8;
    private int AsciiStart => HexStart + BytesPerRow * 3 + (BytesPerRow - 1) / 8 + 1;
    private int TotalChars => AsciiStart + BytesPerRow;

    private void EnsureMetrics()
    {
        if (_metricsValid) return;

        _typeface = new Typeface(FontFamily);
        var probe = new FormattedText("0000000000", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            _typeface, FontSize, Brushes.Black);
        _charWidth = probe.WidthIncludingTrailingWhitespace / 10;
        _lineHeight = Math.Ceiling(probe.Height);
        _metricsValid = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMetrics();

        var extent = new Size(TotalChars * _charWidth + 2 * PaddingX, RowCount * _lineHeight + 2 * PaddingY);
        if (extent != _extent)
        {
            _extent = extent;
            _extentChanged = true;
        }

        var width = double.IsInfinity(availableSize.Width) ? extent.Width : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? extent.Height : availableSize.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var changed = _extentChanged || _viewport != finalSize;
        _extentChanged = false;
        _viewport = finalSize;

        var clamped = ClampOffset(_offset);
        if (clamped != _offset)
        {
            _offset = clamped;
            changed = true;
        }

        if (changed)
        {
            RaiseScrollInvalidated(EventArgs.Empty);
        }

        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        EnsureMetrics();

        var bounds = new Rect(Bounds.Size);
        if (Background is { } background)
        {
            context.FillRectangle(background, bounds);
        }

        var data = Data;
        if (data == null || data.Length == 0 || _lineHeight <= 0) return;

        var bytesPerRow = BytesPerRow;
        var rows = RowCount;
        var firstRow = Math.Max(0, (int)Math.Floor((_offset.Y - PaddingY) / _lineHeight));
        var lastRow = Math.Min(rows - 1, (int)Math.Ceiling((_offset.Y - PaddingY + bounds.Height) / _lineHeight));
        var x0 = PaddingX - _offset.X;
        var selected = SelectedOffset;
        var foreground = Foreground ?? Brushes.Black;

        for (var row = firstRow; row <= lastRow; row++)
        {
            var y = PaddingY + row * _lineHeight - _offset.Y;
            var start = row * bytesPerRow;
            var count = Math.Min(bytesPerRow, data.Length - start);

            if ((row & 1) == 1 && AlternateRowBrush is { } alternate)
            {
                context.FillRectangle(alternate, new Rect(0, y, bounds.Width, _lineHeight));
            }

            if (selected >= start && selected < start + count && SelectionBrush is { } selection)
            {
                var col = selected - start;
                context.FillRectangle(selection,
                    new Rect(x0 + HexCellChar(col) * _charWidth - 1, y, _charWidth * 2 + 2, _lineHeight), 2);
                context.FillRectangle(selection,
                    new Rect(x0 + (AsciiStart + col) * _charWidth, y, _charWidth, _lineHeight), 2);
            }

            BuildRow(data, start, count, bytesPerRow);
            var text = new FormattedText(_rowBuilder.ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _typeface, FontSize, foreground);
            if (OffsetForeground is { } offsetBrush)
            {
                text.SetForegroundBrush(offsetBrush, 0, OffsetChars);
            }
            if (AsciiForeground is { } asciiBrush)
            {
                text.SetForegroundBrush(asciiBrush, AsciiStart, count);
            }

            context.DrawText(text, new Point(x0, y));
        }
    }

    private void BuildRow(byte[] data, int start, int count, int bytesPerRow)
    {
        var sb = _rowBuilder;
        sb.Clear();
        sb.Append(start.ToString("X8")).Append("  ");

        for (var col = 0; col < bytesPerRow; col++)
        {
            if (col > 0 && col % 8 == 0) sb.Append(' ');

            if (col < count)
            {
                var b = data[start + col];
                sb.Append(HexDigits[b >> 4]).Append(HexDigits[b & 0xF]).Append(' ');
            }
            else
            {
                sb.Append("   ");
            }
        }

        sb.Append(' ');

        for (var col = 0; col < count; col++)
        {
            var b = data[start + col];
            sb.Append(b >= 32 && b < 127 ? (char)b : '.');
        }
    }

    // ---- Input ---------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();
        var hit = HitTestByte(e.GetPosition(this));
        if (hit >= 0)
        {
            SetCurrentValue(SelectedOffsetProperty, hit);
            e.Handled = true;
        }
    }

    private int HitTestByte(Point p)
    {
        var data = Data;
        if (data == null || data.Length == 0 || _charWidth <= 0) return -1;

        var row = (int)Math.Floor((p.Y + _offset.Y - PaddingY) / _lineHeight);
        if (row < 0 || row >= RowCount) return -1;

        var ch = (p.X + _offset.X - PaddingX) / _charWidth;
        if (ch < HexStart) return -1;

        var bytesPerRow = BytesPerRow;
        var col = -1;

        if (ch >= AsciiStart)
        {
            col = (int)(ch - AsciiStart);
            if (col >= bytesPerRow) return -1;
        }
        else
        {
            for (var c = 0; c < bytesPerRow; c++)
            {
                var cell = HexCellChar(c);
                if (ch >= cell && ch < cell + 3)
                {
                    col = c;
                    break;
                }
            }
        }

        if (col < 0) return -1;
        var offset = row * bytesPerRow + col;
        return offset < data.Length ? offset : -1;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var data = Data;
        if (e.Handled || data == null || data.Length == 0) return;

        var bytesPerRow = BytesPerRow;
        var visibleRows = Math.Max(1, (int)(_viewport.Height / Math.Max(1, _lineHeight)));
        var current = SelectedOffset;
        int next;

        switch (e.Key)
        {
            case Key.Left: next = current - 1; break;
            case Key.Right: next = current + 1; break;
            case Key.Up: next = current - bytesPerRow; break;
            case Key.Down: next = current + bytesPerRow; break;
            case Key.PageUp: next = current - bytesPerRow * visibleRows; break;
            case Key.PageDown: next = current + bytesPerRow * visibleRows; break;
            case Key.Home: next = 0; break;
            case Key.End: next = data.Length - 1; break;
            default: return;
        }

        if (current < 0) next = 0;
        next = Math.Clamp(next, 0, data.Length - 1);

        SetCurrentValue(SelectedOffsetProperty, next);
        ScrollRowIntoView(next / bytesPerRow);
        e.Handled = true;
    }

    private void ScrollRowIntoView(int row)
    {
        var top = PaddingY + row * _lineHeight;
        var bottom = top + _lineHeight;
        var y = _offset.Y;

        if (top < y) y = top - PaddingY;
        else if (bottom > y + _viewport.Height) y = bottom - _viewport.Height + PaddingY;

        if (y != _offset.Y)
        {
            Offset = new Vector(_offset.X, y);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataProperty)
        {
            _offset = default;
            SetCurrentValue(SelectedOffsetProperty, -1);
            UpdateSelectionDescription();
            InvalidateVisual();
        }
        else if (change.Property == SelectedOffsetProperty)
        {
            UpdateSelectionDescription();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == BytesPerRowProperty)
        {
            _metricsValid = false;
            InvalidateVisual();
        }
    }

    private void UpdateSelectionDescription()
    {
        var data = Data;
        var i = SelectedOffset;

        if (data != null && i >= 0 && i < data.Length)
        {
            var b = data[i];
            var printable = b >= 32 && b < 127 ? $"  '{(char)b}'" : "";
            SelectionDescription = $"Offset 0x{i:X4} ({i})   Byte 0x{b:X2} ({b}){printable}";
        }
        else
        {
            SelectionDescription = data is { Length: > 0 } ? $"{data.Length} bytes" : "";
        }
    }

    // ---- ILogicalScrollable ----------------------------------------------------------------

    public Vector Offset
    {
        get => _offset;
        set
        {
            var clamped = ClampOffset(value);
            if (clamped == _offset) return;
            _offset = clamped;
            InvalidateVisual();
            RaiseScrollInvalidated(EventArgs.Empty);
        }
    }

    public Size Extent => _extent;
    public Size Viewport => _viewport;
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public bool IsLogicalScrollEnabled => true;
    public Size ScrollSize => new(_charWidth * 4, _lineHeight * 3);
    public Size PageScrollSize => _viewport;

    public event EventHandler? ScrollInvalidated;

    public bool BringIntoView(Control target, Rect targetRect) => false;

    public Control? GetControlInDirection(NavigationDirection direction, Control? from) => null;

    public void RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

    private Vector ClampOffset(Vector v)
    {
        var maxX = Math.Max(0, _extent.Width - _viewport.Width);
        var maxY = Math.Max(0, _extent.Height - _viewport.Height);
        return new Vector(Math.Clamp(v.X, 0, maxX), Math.Clamp(v.Y, 0, maxY));
    }
}
