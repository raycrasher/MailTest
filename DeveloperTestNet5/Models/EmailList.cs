using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DeveloperTestNet5.Models
{
    class EmailComparer : IComparer<Email>
    {
        public int Compare(Email x, Email y)
        {
            if (x.DateTime == null && y.DateTime == null)
                return 0;
            if (x.DateTime == null)
                return -1;
            if (y.DateTime == null)
                return 1;
            return DateTime.Compare(x.DateTime.Value, y.DateTime.Value);
        }
    }

    public class EmailList : ICollection<Email>, INotifyCollectionChanged
    {

        private object _lockObject = new();
        private List<Email> _emails = new();

        public int Count => _emails.Count;

        public bool IsReadOnly => false;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void Add(Email item)
        {
            lock (_lockObject)
            {
                Application.Current?.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
                    }));
            }
        }

        public void AddRange(IEnumerable<Email> emails)
        {
            lock (_lockObject)
            {
                var emailArray = emails.ToArray();
                foreach(var email in emailArray)
                {
                    _emails.Add(email);
                }
                Application.Current?.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, emailArray));
                    }));
            }
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _emails.Clear();
                Application.Current?.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    }));
            }
        }

        public bool Contains(Email item)
        {
            return _emails.Contains(item);
        }

        public void CopyTo(Email[] array, int arrayIndex)
        {
            _emails.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Email> GetEnumerator() => _emails.GetEnumerator();

        public bool Remove(Email item)
        {
            lock (_lockObject)
            {
                var result = _emails.Remove(item);
                Application.Current?.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                    }));
                return result;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => _emails.GetEnumerator();
    }
}
