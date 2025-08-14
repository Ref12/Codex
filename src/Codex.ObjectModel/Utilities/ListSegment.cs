using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;

namespace Codex.Utilities
{
    /// <summary>
    /// A segment of a list
    /// </summary>
    /// <typeparam name="T">the list item type</typeparam>
    public struct ListSegment<T> : IReadOnlyList<T>, IList<T>
    {
        /// <summary>
        /// The underlying list
        /// </summary>
        public IReadOnlyList<T> List { get; private set; }

        /// <summary>
        /// The start index
        /// </summary>
        public int Start { get; private set; }

        /// <summary>
        /// The end index
        /// </summary>
        public int End => (Start + Count - 1);

        /// <summary>
        /// The count of items in the segment
        /// </summary>
        public int Count { get; private set; }

        private T[] AsArray => this.ToArray();

        public bool IsEmpty => Count == 0;

        /// <summary>
        /// Constructs a new list segment containing the full list
        /// </summary>
        /// <param name="list">the underlying list</param>
        public ListSegment(IReadOnlyList<T> list)
            : this(list, 0, list.Count)
        {
        }
        /// <summary>
        /// Constructs a new list segment containing the full list
        /// </summary>
        /// <param name="list">the underlying list</param>
        public ListSegment(IReadOnlyList<T> list, Range range)
            : this(list, range.Start, range.Length)
        {
        }

        /// <summary>
        /// Constructs a new list segment specified part of the list
        /// </summary>
        /// <param name="list">the underlying list</param>
        public ListSegment(IReadOnlyList<T> list, int start, int count)
            : this()
        {
            List = list;
            Start = start;
            Count = count;
        }

        /// <summary>
        /// Returns true if this list segment contains the given index
        /// </summary>
        /// <param name="index">the index in the underlying list</param>
        /// <returns>true if the segment contains the index, false otherwise.</returns>
        public bool ContainsIndex(int index)
        {
            if (index < Start || index > End)
            {
                return false;
            }

            return true;
        }

        public ListSegment<T> Unwrap(int maxIterations = 10)
        {
            var list = List;
            var extent = GetExtent();
            while (list is ListSegment<T> segment && maxIterations-- > 0)
            {
                extent = new(extent.Start + segment.Start, extent.Length);
                list = segment.List;
            }

            return new ListSegment<T>(list, extent);
        }

        public Extent GetExtent() => new Extent(Start, Count);

        public ListSegment<T> Slice(int start, int? length = null)
        {
            var sliceExtent = new Extent(Start + start, length ?? (Count - start));
            Contract.Assert(sliceExtent.Length >= 0);

            return new ListSegment<T>(List, sliceExtent);
        }

        #region IReadOnlyList<T> Members

        /// <summary>
        /// Returns the item at the index in the segment relative to the segment start.
        /// </summary>
        /// <param name="index">the segment index</param>
        /// <returns>the item at the index</returns>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }
                return List[Start + index];
            }
        }

        #endregion

        #region IReadOnlyCollection<T> Members

        public bool IsReadOnly => throw new NotImplementedException();

        T IList<T>.this[int index] { get => this[index]; set => throw new NotImplementedException(); }

        #endregion

        public IEnumerable<(T Item, int Index)> WithAbsoluteIndices()
        {
            var end = End;
            for (int i = Start; i <= end; i++)
            {
                yield return (List[i], i);
            }
        }

        public ReadOnlyListEnumerator<IReadOnlyList<T>, T> GetEnumerator()
        {
            return new ReadOnlyListEnumerator<IReadOnlyList<T>, T>(List, Start, Count);
        }

        #region IEnumerable<T> Members

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int IndexOf(T item)
        {
            var end = End;
            for (int i = Start; i <= end; i++)
            {
                if (EqualityComparer<T>.Default.Equals(List[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        public void ForEach<TData>(TData data, Action<T, TData> body)
        {
            foreach (var item in this)
            {
                body(item, data);
            }
        }

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public void Add(T item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            var end = End;
            for (int i = Start; i <= end; i++)
            {
                array[arrayIndex++] = List[i];
            }
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
