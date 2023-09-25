using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Quantmom.Api
{
    public class DictView<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private ImmutableDictionary<TKey, TValue> _values;
        private Dictionary<TKey, TValue> _view = null!;

        private void UpdateView() => _view = _values.ToDictionary(n => n.Key, n => n.Value, _values.KeyComparer);
        public DictView()
        {
            _values = ImmutableDictionary<TKey, TValue>.Empty;
            UpdateView();
        }

        public DictView(IEqualityComparer<TKey> comparer)
        {
            _values = ImmutableDictionary<TKey, TValue>.Empty.WithComparers(comparer);
            UpdateView();
        }

        public ICollection<TKey> Keys => _view.Keys;

        public ICollection<TValue> Values => _view.Values;

        public int Count => _view.Count;

        public bool IsReadOnly => ((IDictionary<TKey, TValue>)_view).IsReadOnly;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _view.Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _view.Values;

        public TValue this[TKey key]
        {
            get => _view[key];
            set
            {
                _values = _values.SetItem(key, value);
                UpdateView();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key, TValue value)
        {
            _values = _values.Add(key, value);
            UpdateView();
        }

        public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> pairs)
        {
            _values = _values.AddRange(pairs);
            UpdateView();
        }

        public void Remove(TKey key)
        {
            _values = _values.Remove(key);
            UpdateView();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            return _view.ContainsKey(key);
        }

        bool IDictionary<TKey, TValue>.Remove(TKey key)
        {
            if (!ContainsKey(key))
                return false;
            Remove(key);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            return _view.TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _values = _values.Clear();
            UpdateView();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)_view).Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)_view).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (ContainsKey(item.Key))
            {
                Remove(item.Key);
                return true;
            }
            return false;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _view.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _view.GetEnumerator();
        }

        public static implicit operator Dictionary<TKey, TValue>(DictView<TKey, TValue> v) => v._view;    
    }
}
