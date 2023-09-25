using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using QuantConnect.Securities;

namespace QuantConnect
{
    public class ConcurrentDictView<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> _values;
        private Dictionary<TKey, TValue> _view;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateView() => _view = _values.ToDictionary(
            n => n.Key, 
            n => n.Value);

        public ConcurrentDictView()
        {
            _values = new ConcurrentDictionary<TKey, TValue>();
            UpdateView();
        }

        public ICollection<TKey> Keys => _values.Keys;

        public ICollection<TValue> Values => _values.Values;

        public int Count => _values.Count;

        public TValue this[TKey key]
        {
            get => _values[key];
            set
            {
                _values[key] = value;
                UpdateView();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            return _view.ContainsKey(key);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly { get; }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            throw new System.NotImplementedException();
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new System.NotImplementedException();
        }

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
        {
            throw new System.NotImplementedException();
        }

        bool IDictionary<TKey, TValue>.Remove(TKey key)
        {
            throw new System.NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            return _view.TryGetValue(key, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue value)
        {
            if (_values.TryAdd(key, value))
            {
                UpdateView();
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(TKey key, out TValue value)
        {
            if (_values.TryRemove(key, out value))
            {
                UpdateView();
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _values.Clear();
            UpdateView();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)_values).Contains(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)_values).CopyTo(array, arrayIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IOrderedEnumerable<KeyValuePair<TKey, TValue>> ItemOrderBy(
            Func<KeyValuePair<TKey, TValue>, string> keySelector)
        {
            return _values.OrderBy(keySelector);
        }
    }
}
