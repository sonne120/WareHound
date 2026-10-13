using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WareHound.Avalonia.Infrastructure.Collections;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;
    
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) return;

        _suppressNotification = true;
        try
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
            NotifyReset();
        }
    }
    
    public void TrimExcess(int maxCount)
    {
        if (Count <= maxCount) return;

        _suppressNotification = true;
        try
        {
            while (Count > maxCount)
            {
                Items.RemoveAt(0);
            }
        }
        finally
        {
            _suppressNotification = false;
            NotifyReset();
        }
    }
    
    public void ReplaceAll(IEnumerable<T> items)
    {
        _suppressNotification = true;
        try
        {
            Items.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }
            }
        }
        finally
        {
            _suppressNotification = false;
            NotifyReset();
        }
    }

    // Items.* bypasses ObservableCollection's own notifications, so raise Count/indexer changes too
    // (otherwise bindings such as WhenAnyValue(x => x.Packets.Count) never update)
    private void NotifyReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }
}
