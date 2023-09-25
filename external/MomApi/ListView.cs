using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Quantmom.Api
{
    public class ListView<T> : IList<T>, IReadOnlyList<T> where T: notnull
    {
        private ImmutableList<T> _values = null!;
        private List<T> _view = null!;
        private readonly IEqualityComparer<T> _comparer;
        private void UpdateView() => _view = _values.ToList();
        public ListView(IEqualityComparer<T>? comparer = null)
        {
            Clear();
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public int Count => _view.Count;

        public bool IsReadOnly => ((IList<T>)_view).IsReadOnly;

        public T this[int index]
        {
            get {
                if (index >= 0 && index < _view.Count)
                {
                    return _view[index];
                }
                return default!;
            }
            set {
                if (index >= 0 && index < _values.Count)
                {
                    _values = _values.SetItem(index, value);
                }
                else
                {
                    var count = index - _values.Count + 1;
                    if (count > 0)
                    {
                        var list = Enumerable.Repeat(default(T)!, count - 1).ToList();
                        list.Add(value);
                        _values = _values.AddRange(list);
                    }
                    else
                    {
                        throw new IndexOutOfRangeException();
                    }
                }
                UpdateView();
            }
        }

        public void Add(T value)
        {
            _values = _values.Add(value);
            UpdateView();
        }

        public void AddRange(IEnumerable<T> values)
        {
            _values = _values.AddRange(values);
            UpdateView();
        }

        public void Remove(T value)
        {
            if (Exists(value))
            {
                _values = _values.Remove(value, _comparer);
                UpdateView();
            }
        }

        public void Clear()
        {
            _values = ImmutableList<T>.Empty;
            UpdateView();
        }

        public bool Exists(T value)
        {
            return _view.Exists(n => _comparer.Equals(value, n));
        }

        public int IndexOf(T item) => _view.IndexOf(item);

        public void Insert(int index, T item)
        {
            _values = _values.Insert(index, item);
            UpdateView();
        }

        public void RemoveAt(int index)
        {
            _values = _values.RemoveAt(index);
            UpdateView();
        }

        public bool Contains(T item)
        {
            return Exists(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _view.CopyTo(array, arrayIndex);
        }

        bool ICollection<T>.Remove(T item)
        {
            if (Exists(item))
            {
                Remove(item);
                return true;
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _view.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _view.GetEnumerator();
        }
    }
}
