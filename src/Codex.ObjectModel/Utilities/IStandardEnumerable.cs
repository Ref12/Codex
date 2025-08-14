using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Codex.Utilities
{
    public interface IStandardEnumerable<T> : IEnumerable<T>
    {
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public interface IEnumerable<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    { 
    }

    public interface IStandardReadOnlyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, 
        IStandardEnumerable<KeyValuePair<TKey, TValue>>,
        IEnumerable<TKey, TValue>
    {
        bool IReadOnlyDictionary<TKey, TValue>.ContainsKey(TKey key)
        {
            return TryGetValue(key, out _);
        }

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => this.Select(e => e.Key);

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => this.Select(e => e.Value);

        TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value)) return value;
                throw new KeyNotFoundException(key?.ToString());
            }
        }
    }
}
