using Codex.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codex.Lucene
{
    /// <summary>
    /// Caches a specific number of values
    /// </summary>
    public record VolatileMap<TKey, TValue>(int Limit)
    {
        private readonly ConcurrentDictionary<TKey, Entry> _cachedValues = new();

        private LinkedList<TKey> _list = new();

        private record Entry(LinkedListNode<TKey> Node, TValue Value)
        {
            public TKey Key => Node.Value;
        }

        /// <summary>
        /// Returns the number of elements in the set.
        /// </summary>
        public int Count => _cachedValues.Count;

        public void Add(TKey key, TValue value)
        {
            Invalidate(key);

            var node = _list.AddLast(key);
            _cachedValues[key] = new Entry(node, value);
            CleanStaleItems();
        }

        public bool TryGetMostRecent(out TKey lastKey)
        {
            var last = _list.Last;
            lastKey = last != null ? last.Value : default(TKey);
            return last != null;
        }

        public IEnumerable<(TKey Key, TValue Value)> EnumerateMruEntries()
        {
            return EnumerateMruKeys()
                .Select(k => _cachedValues.GetOrDefault(k))
                .Where(e => e != null)
                .Select(e => (e.Key, e.Value));
        }

        public IEnumerable<TKey> EnumerateMruKeys()
        {
            if (_list.Count == 0)
            {
                yield break;
            }

            var current = _list.Last;
            while (current != null)
            {
                yield return current.Value;
                current = current.Previous;
            }
        }

        public void Clear()
        {
            _cachedValues.Clear();
            _list.Clear();
        }

        public int CleanStaleItems(int? limit = null)
        {
            int removed = 0;
            while (_list.Count > (limit ?? Limit))
            {
                removed++;
                var first = _list.First;
                Invalidate(first.Value);
            }

            return removed;
        }

        /// <summary>
        /// Removes the item from the pin cache.
        /// </summary>
        public void Invalidate(TKey key)
        {
            if (_cachedValues.TryRemove(key, out var entry))
            {
                _list.Remove(entry.Node);
            }
        }

        public TValue GetOrAdd<TData>(TKey key, TData data, Func<TKey, TData, TValue> createValue)
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                value = createValue(key, data);
                Add(key, value);
                return value;
            }
        }

        /// <summary>
        /// Gets whether the set contains the value and the value has not expired.
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value, bool touch = true)
        {
            if (_cachedValues.TryGetValue(key, out var entry))
            {
                if (touch)
                {
                    _list.Remove(entry.Node);
                    _list.AddLast(entry.Node);
                }
                value = entry.Value;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }
    }
}
