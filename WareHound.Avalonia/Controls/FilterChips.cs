using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace WareHound.Avalonia.Controls;

public class ChipRemovedEventArgs : RoutedEventArgs
{
    public ChipRemovedEventArgs(RoutedEvent routedEvent, object? item, object? source)
        : base(routedEvent, source)
    {
        Item = item;
    }

    public object? Item { get; }
}

public class FilterChip : ContentControl
{
    private Button? _removeButton;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_removeButton != null)
        {
            _removeButton.Click -= OnRemoveClick;
        }

        _removeButton = e.NameScope.Find<Button>("PART_RemoveButton");

        if (_removeButton != null)
        {
            _removeButton.Click += OnRemoveClick;
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new ChipRemovedEventArgs(FilterChipsPanel.ChipRemovedEvent, Content ?? DataContext, this));
    }
}

public class FilterChipsPanel : ItemsControl
{
    public static readonly RoutedEvent<ChipRemovedEventArgs> ChipRemovedEvent =
        RoutedEvent.Register<FilterChipsPanel, ChipRemovedEventArgs>(nameof(ChipRemoved), RoutingStrategies.Bubble);

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<FilterChipsPanel, ICommand?>(nameof(RemoveCommand));

    static FilterChipsPanel()
    {
        ItemsPanelProperty.OverrideDefaultValue<FilterChipsPanel>(
            new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Orientation.Horizontal }));
        ChipRemovedEvent.AddClassHandler<FilterChipsPanel>((panel, e) => panel.OnChipRemoved(e));
    }

    public event EventHandler<ChipRemovedEventArgs>? ChipRemoved
    {
        add => AddHandler(ChipRemovedEvent, value);
        remove => RemoveHandler(ChipRemovedEvent, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new FilterChip();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<FilterChip>(item, out recycleKey);

    private void OnChipRemoved(ChipRemovedEventArgs e)
    {
        if (e.Handled) return;

        var command = RemoveCommand;
        if (command?.CanExecute(e.Item) == true)
        {
            command.Execute(e.Item);
        }
    }
}
