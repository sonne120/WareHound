using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WareHound.UI.Infrastructure.Collections
{
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification;

        public void AddRange(IEnumerable<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));

            _suppressNotification = true;

            foreach (T item in list)
            {
                Add(item);
            }

            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnPropertyChanged(e);
        }
    }
}
