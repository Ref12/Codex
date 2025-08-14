using System.Collections;
using System.Diagnostics.ContractsLight;

namespace Codex.Utilities
{
    /// <summary>
    /// Allocation-free enumerable for a <see cref="IReadOnlyList{T}"/>.
    /// </summary>
    public readonly struct ReadOnlyListEnumerable<TList, T>
        where TList : IReadOnlyList<T>
    {
        private readonly TList m_array;
        private readonly int m_start;
        private readonly int m_length;

        /// <nodoc/>
        public ReadOnlyListEnumerable(TList array, int start, int length)
        {
            m_array = array;
            m_start = start;
            m_length = length;
        }

        /// <nodoc/>
        public ReadOnlyListEnumerator<TList, T> GetEnumerator()
        {
            return new ReadOnlyListEnumerator<TList, T>(m_array, m_start, m_length);
        }

        public bool Any(Func<T, bool> predicate)
        {
            foreach (var item in this)
            {
                if (predicate(item)) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Allocation-free enumerator for a <see cref="IReadOnlyList{T}"/>.
    /// </summary>
    public struct ReadOnlyListEnumerator<TList, T> : IEnumerator<T>
        where TList : IReadOnlyList<T>
    {
        private readonly TList m_array;
        private readonly int m_start;
        private readonly int m_endExclusive;
        private int? m_index;

        /// <nodoc/>
        public ReadOnlyListEnumerator(TList array, int start,  int length)
        {
            Contract.Assert(length == 0 || array.Count >= start + length);
            Contract.Assert(length >= 0);
            Contract.Assert(start >= 0);
            m_array = array;
            m_index = null;
            m_start = start;
            m_endExclusive = start + length;
        }

        /// <nodoc/>
        public T Current => m_array[m_index.Value];

        /// <nodoc/>
        public bool MoveNext()
        {
            m_index ??= m_start - 1;
            if (m_index + 1 == m_endExclusive)
            {
                return false;
            }

            m_index++;
            return true;
        }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public void Reset()
        {
        }
    }
}
