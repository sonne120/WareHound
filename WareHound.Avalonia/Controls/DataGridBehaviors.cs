using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WareHound.Avalonia.Controls;


public sealed class DataGridBehaviors
{
    private DataGridBehaviors() { }

    public static readonly AttachedProperty<bool> AutoScrollToEndProperty =
        AvaloniaProperty.RegisterAttached<DataGridBehaviors, DataGrid, bool>("AutoScrollToEnd");

    private static readonly AttachedProperty<AutoScrollState?> StateProperty =
        AvaloniaProperty.RegisterAttached<DataGridBehaviors, DataGrid, AutoScrollState?>("AutoScrollState");

    static DataGridBehaviors()
    {
        AutoScrollToEndProperty.Changed.AddClassHandler<DataGrid>((grid, e) => OnAutoScrollChanged(grid, e.GetNewValue<bool>()));
        DataGrid.ItemsSourceProperty.Changed.AddClassHandler<DataGrid>((grid, _) => grid.GetValue(StateProperty)?.Rebind());
    }

    public static bool GetAutoScrollToEnd(DataGrid grid) => grid.GetValue(AutoScrollToEndProperty);

    public static void SetAutoScrollToEnd(DataGrid grid, bool value) => grid.SetValue(AutoScrollToEndProperty, value);

    private static void OnAutoScrollChanged(DataGrid grid, bool enabled)
    {
        var state = grid.GetValue(StateProperty);

        if (enabled)
        {
            if (state == null)
            {
                state = new AutoScrollState(grid);
                grid.SetValue(StateProperty, state);
            }

            state.Rebind();
            state.RequestScroll();
        }
        else
        {
            state?.Dispose();
            grid.SetValue(StateProperty, null);
        }
    }

    private sealed class AutoScrollState : IDisposable
    {
        private static readonly TimeSpan Throttle = TimeSpan.FromMilliseconds(100);

        private readonly DataGrid _grid;
        private INotifyCollectionChanged? _source;
        private bool _scheduled;
        private DateTime _lastScroll = DateTime.MinValue;
        private object? _lastItem;

        public AutoScrollState(DataGrid grid)
        {
            _grid = grid;
            grid.AttachedToVisualTree += OnAttached;
            grid.DetachedFromVisualTree += OnDetached;
        }

        public void Rebind()
        {
            Unbind();
            _lastItem = null;
            _source = _grid.ItemsSource as INotifyCollectionChanged;
            if (_source != null)
            {
                _source.CollectionChanged += OnCollectionChanged;
            }
        }

        private void Unbind()
        {
            if (_source == null) return;
            _source.CollectionChanged -= OnCollectionChanged;
            _source = null;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Move) return;
            RequestScroll();
        }

        public void RequestScroll()
        {
            if (_scheduled) return;
            _scheduled = true;

            var wait = Throttle - (DateTime.UtcNow - _lastScroll);
            if (wait <= TimeSpan.Zero)
            {
                Dispatcher.UIThread.Post(ScrollNow, DispatcherPriority.Background);
            }
            else
            {
                DispatcherTimer.RunOnce(ScrollNow, wait, DispatcherPriority.Background);
            }
        }

        private void ScrollNow()
        {
            _scheduled = false;
            _lastScroll = DateTime.UtcNow;

            if (_source == null || _grid.GetVisualRoot() == null) return;

            var last = LastItem(_grid.ItemsSource);
            if (last == null || ReferenceEquals(last, _lastItem)) return;

            _lastItem = last;
            _grid.ScrollIntoView(last, null);
        }

        private static object? LastItem(IEnumerable? items)
        {
            if (items is IList list)
            {
                return list.Count > 0 ? list[list.Count - 1] : null;
            }

            object? last = null;
            if (items != null)
            {
                foreach (var item in items) last = item;
            }
            return last;
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => Rebind();

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Unbind();

        public void Dispose()
        {
            Unbind();
            _grid.AttachedToVisualTree -= OnAttached;
            _grid.DetachedFromVisualTree -= OnDetached;
        }
    }
}
