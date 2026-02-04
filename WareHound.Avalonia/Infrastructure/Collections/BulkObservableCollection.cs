using System.Collections.ObjectModel;
using System.Collections.Specialized;

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
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }
}
