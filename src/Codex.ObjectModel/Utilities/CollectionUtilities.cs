using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics.ContractsLight;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Codex.Sdk.Utilities;
using static System.Net.Mime.MediaTypeNames;

namespace Codex.Utilities
{
    public static partial class CollectionUtilities
    {
        public static ArrayPoolLease<T> Lease<T>(this ArrayPool<T> pool, int length, out ArraySegment<T> array)
        {
            array = new(pool.Rent(length), 0, length);
            return new(array.Array, pool);
        }

        public static IEnumerable<T> Distinct<T>(this IEnumerable<T> items, HashSet<T> visited)
        {
            return DistinctBy(items, static t => t, visited);
        }

        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> keySelector, HashSet<TKey> visited)
        {
            foreach (var item in items)
            {
                var key = keySelector(item);
                if (visited.Add(key))
                {
                    yield return item;
                }
            }
        }

        public static ref readonly T IndexOfRef<T>(this ImmutableSortedSet<T>.Builder set, T item)
        {
            var index = set.IndexOf(item);
            if (index < 0)
            {
                index = ~index;
                index = Math.Max(0, index - 1);
            }

            if (index >= set.Count)
            {
                index = set.Count - 1;
            }

            return ref set.ItemRef(index);
        }

        public static ImmutableSortedSet<T>.Builder WithRange<T>(this ImmutableSortedSet<T>.Builder set, IEnumerable<T> items)
        {
            set.UnionWith(items);
            return set;
        }

        public static Iterator<TEnumerator, T> GetIterator<TEnumerator, T>(this TEnumerator enumerator, Func<TEnumerator, T> typeHint)
            where TEnumerator : IEnumerator<T>
        {
            return new Iterator<TEnumerator, T>(enumerator);
        }

        public static IIterator<T> GetIterator<T>(this IEnumerator<T> enumerator)
        {
            return new Iterator<IEnumerator<T>, T>(enumerator);
        }

        public static IIterator<T> GetIterator<T>(this IEnumerable<T> items, bool moveNext = true)
        {
            return new Iterator<IEnumerator<T>, T>(items.GetEnumerator(), moveNext);
        }

        public static void Set(this ref BitVector32 bitArray, int index, bool value)
        {
            bitArray[(1 << index)] = value;
        }

        public static bool Get(this ref BitVector32 bitArray, int index)
        {
            return bitArray[(1 << index)];
        }

        public static T Get<T>(this IReadOnlyList<T> list, int index)
        {
            return list[index];
        }

        public static T GetLast<T>(this IReadOnlyList<T> list)
        {
            return list[list.Count - 1];
        }

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> list)
        {
            return list ?? Array.Empty<T>();
        }

        public static bool IsNullOrEmpty<T>(this IReadOnlyList<T> list) => (list?.Count ?? 0) == 0;

        public static T GetOrDefault<T>(this IReadOnlyList<T> list, int index, T defaultValue = default)
        {
            if (unchecked((uint)index < (uint)list.Count))
            {
                return list[index];
            }

            return defaultValue;
        }

        public static void Add<TKey, TValue>(this IDictionary<TKey, TValue> map, IEnumerable<KeyValuePair<TKey, TValue>> entries)
        {
            foreach (var entry in entries)
            {
                map.Add(entry.Key, entry.Value);
            }
        }

        public static void Add<T>(this List<T> list, IEnumerable<T> values)
        {
            list.AddRange(values);
        }

        public static byte[] ToByteArray(this BitArray bits)
        {
            byte[] bytes = new byte[(bits.Length + 7) / 8];
            bits.CopyTo(bytes, 0);
            return bytes;
        }

        public static TResult[] SelectArray<T, TResult>(this IReadOnlyCollection<T> items, Func<T, TResult> selector)
        {
            TResult[] results = new TResult[items.Count];
            int i = 0;
            foreach (var item in items)
            {
                results[i] = selector(item);
                i++;
            }

            return results;
        }

        public static TResult[] SelectArray<T, TResult>(this IReadOnlyCollection<T> items, Func<T, int, TResult> selector)
        {
            TResult[] results = new TResult[items.Count];
            int i = 0;
            foreach (var item in items)
            {
                results[i] = selector(item, i);
                i++;
            }

            return results;
        }

        public static TResult[] SelectManyArray<T, TResult>(this IReadOnlyCollection<T> items, int expansionFactor, SpanAction<TResult, T> selector)
        {
            TResult[] results = new TResult[items.Count * expansionFactor];
            int i = 0;
            foreach (var item in items)
            {
                selector(results.AsSpan(i, expansionFactor), item);
                i += expansionFactor;
            }

            return results;
        }

        public static IEnumerable<T> ExceptBy<TKey, T>(this IEnumerable<T> first, IEnumerable<T> second, Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
        {
            return first.ExceptBy(second.Select(keySelector), keySelector, comparer ?? EqualityComparer<TKey>.Default);
        }

        public static Queue<T> ToQueue<T>(this IEnumerable<T> items) => new(items);

        public static Span<T> AsSpan<T>(this T[,] array)
        {
            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(array)), array.Length);
        }

        public static ListSegment<T> AsSegment<T>(this IReadOnlyList<T> list, int start, int length)
        {
            return new ListSegment<T>(list, start, length);
        }

        public static ListSegment<T> AsSegment<T>(this IReadOnlyList<T> list, int start)
        {
            return new ListSegment<T>(list, start, list.Count - start);
        }

        public static ListSegment<T> AsSegment<T>(this IReadOnlyList<T> list)
        {
            return new ListSegment<T>(list);
        }

        public static IReadOnlyList<TResult> SelectList<T, TResult>(this IReadOnlyList<T> items, Func<T, TResult> selector)
        {
            return SelectList(items, static (item, index, selector) => selector(item), selector);
        }

        public static IReadOnlyList<TResult> SelectManyList<T, TResult>(this IReadOnlyList<T> items, int expansionFactor, Func<(T Item, int SubIndex), TResult> selector)
        {
            var rangeList = new RangeList(new Extent(0, items.Count * expansionFactor));

            return rangeList.SelectList(
                selector: static (index, _, state) =>
                {
                    return state.selector((state.items[index / state.expansionFactor], index % state.expansionFactor));
                },
                state: (items, selector, expansionFactor));
        }

        /// <summary>
        /// Creates a projection list over the list using <paramref name="selector"/>
        /// </summary>
        public static IReadOnlyList<TResult> SelectList<T, TResult, TState>(this IReadOnlyList<T> list, Func<T, int, TState, TResult> selector, TState state)
        {
            Contract.Requires(list != null);

            return new SelectList<T, TResult, TState>(list, selector, state);
        }

        public static IReadOnlyList<T> AsSingle<T>(this T value)
        {
            return new[] { value };
        }

        public static T SingleOrDefaultNoThrow<T>(this IEnumerable<T> items)
        {
            int count = 0;
            T result = default(T);
            foreach (var item in items)
            {
                if (count == 0)
                {
                    result = item;
                }
                else
                {
                    return default(T);
                }

                count++;
            }

            return result;
        }

        public static IEnumerable<(T Item, int Index)> WithIndices<T>(this IEnumerable<T> items)
        {
            int index = 0;
            foreach (var item in items)
            {
                yield return (item, index);
                index++;
            }
        }

        public static void ForEachIndex<T>(this IEnumerable<T> items, Action<(T Item, int Index)> action)
        {
            foreach (var item in items.WithIndices())
            {
                action(item);
            }
        }

        public static List<T> AsList<T>(this IEnumerable<T> items)
        {
            if (items is List<T> list)
            {
                return list;
            }
            else
            {
                return items.ToList();
            }
        }

        public static ReadOnlyListEnumerator<TList, T> GetEnumerator<TList, T>(this TList items)
            where TList : IReadOnlyList<T>
        {
            return new ReadOnlyListEnumerator<TList, T>(items, 0, items.Count);
        }

        public static ReadOnlyListEnumerator<TList, T> GetEnumerator<TList, T>(this TList items, int start, int length)
            where TList : IReadOnlyList<T>
        {
            return new ReadOnlyListEnumerator<TList, T>(items, start, length);
        }

        public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> items)
        {
            if (items is IReadOnlyList<T>)
            {
                return (IReadOnlyList<T>)items;
            }
            else
            {
                return items.ToList();
            }
        }

        public static bool TryGetSingle<T>(this IReadOnlyList<T> list, out T single)
        {
            if (list.Count == 1)
            {
                single = list[0];
                return true;
            }

            single = default;
            return false;
        }

        public static void AddRange<T>(this ArrayBuilder<T> list, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                list.Add(item);
            }
        }

        public static IEnumerable<(TKey Key, ListSegment<TValue> Items)> SortedBufferGroupBy<TKey, TValue>(
            this IEnumerable<TValue> values,
            Func<TValue, TKey> keySelector,
            IEqualityComparer<TKey> keyComparer = null,
            ArrayBuilder<TValue> buffer = null)
        {
            keyComparer ??= EqualityComparer<TKey>.Default;

            buffer ??= new();
            buffer.Reset();

            foreach ((var current, var next, int index, bool isLast) in values
                .Select(v => (key: keySelector(v), value: v))
                .WithNexts())
            {
                buffer.Add(current.value);
                if (isLast || !next.HasValue || !keyComparer.Equals(current.key, next.Value.key))
                {
                    // Special case. List with only a single element so next is null.
                    yield return (current.key, new ListSegment<TValue>(buffer));
                    buffer.Reset();
                }
            }
        }

        public static IEnumerable<ListSegment<T>> GroupAt<T>(
            this IReadOnlyList<T> values, Func<T, bool> endGroup)
        {
            int segmentStart = 0;
            foreach ((var value, var index, bool isLast) in values.WithIndicesAndIsLast())
            {
                if (isLast || endGroup(value))
                {
                    var range = Extent.FromBounds(segmentStart, endExclusive: index + 1);
                    yield return new ListSegment<T>(values, range);
                    segmentStart = range.EndExclusive;
                }
            }
        }

        public static IEnumerable<ListSegment<T>> GroupAtStarts<T>(
            this IReadOnlyList<T> values, Func<T, bool> startGroup)
        {
            int segmentStart = 0;
            foreach ((var value, var index) in values.WithIndices())
            {
                if (startGroup(value))
                {
                    var range = Extent.FromBounds(segmentStart, endExclusive: index);
                    yield return new ListSegment<T>(values, range);
                    segmentStart = index;
                }
            }

            if (values.Count != 0)
            {
                var range = Extent.FromBounds(segmentStart, endExclusive: values.Count);
                yield return new ListSegment<T>(values, range);
            }
        }

        public static IEnumerable<(TKey Key, ListSegment<TValue> Items)> SortedGroupBy<TKey, TValue>(
            this IReadOnlyList<TValue> values, Func<TValue, TKey> keySelector, IEqualityComparer<TKey> keyComparer = null)
        {
            keyComparer ??= EqualityComparer<TKey>.Default;

            int segmentStart = 0;
            foreach ((var current, var next, int index, bool isLast) in values
                .Select(v => (key: keySelector(v), value: v))
                .WithNexts())
            {
                if (isLast || !next.HasValue || !keyComparer.Equals(current.key, next.Value.key))
                {
                    var range = Extent.FromBounds(segmentStart, endExclusive: index + 1);
                    yield return (current.key, new ListSegment<TValue>(values, range).Unwrap());
                    segmentStart = range.EndExclusive;
                }
            }
        }

        public static void SortedDedupe<T>(this IList<T> items, IComparer<T> comparer = null)
        {
            comparer ??= Comparer<T>.Default;

            T lastItem = default(T);
            bool hasLastItem = false;
            int cursor = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!hasLastItem || comparer.Compare(lastItem, item) != 0)
                {
                    hasLastItem = true;
                    lastItem = item;
                    items[cursor] = item;
                    cursor++;
                }
            }

            for (int i = items.Count - 1; i > cursor; i--)
            {
                items.RemoveAt(i);
            }
        }

        public static IEnumerable<T> SortedUnique<T>(this IEnumerable<T> items, IComparer<T> comparer = null)
        {
            comparer ??= Comparer<T>.Default;

            T lastItem = default(T);
            bool hasLastItem = false;
            foreach (var item in items)
            {
                if (!hasLastItem || comparer.Compare(lastItem, item) != 0)
                {
                    hasLastItem = true;
                    lastItem = item;
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> SortedUnique<T, TComparable>(this IEnumerable<T> items, Func<T, TComparable> getComparable)
           where TComparable : IComparable<TComparable>
        {
            TComparable lastItemComparable = default(TComparable);
            bool hasLastItem = false;
            foreach (var item in items)
            {
                var itemComparable = getComparable(item);
                if (!hasLastItem || lastItemComparable.Compare(itemComparable) != CompareResult.Equal)
                {
                    hasLastItem = true;
                    lastItemComparable = itemComparable;
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> ExclusiveInterleave<T>(this IEnumerable<T> items1, IEnumerable<T> items2, IComparer<T> comparer)
        {
            var enumerator1 = items1.GetEnumerator();
            var enumerator2 = items2.GetEnumerator();

            bool hasCurrent1 = TryMoveNext(enumerator1, out var current1);
            bool hasCurrent2 = TryMoveNext(enumerator2, out var current2);

            while (hasCurrent1 || hasCurrent2)
            {
                while (hasCurrent1)
                {
                    if (!hasCurrent2 || comparer.Compare(current1, current2) <= 0)
                    {
                        yield return current1;

                        // Skip over matching spans from second list
                        while (hasCurrent2 && comparer.Compare(current1, current2) == 0)
                        {
                            hasCurrent2 = TryMoveNext(enumerator2, out current2);
                        }
                    }
                    else
                    {
                        break;
                    }

                    hasCurrent1 = TryMoveNext(enumerator1, out current1);
                }

                while (hasCurrent2)
                {
                    if (!hasCurrent1 || comparer.Compare(current1, current2) > 0)
                    {
                        yield return current2;
                    }
                    else
                    {
                        break;
                    }

                    hasCurrent2 = TryMoveNext(enumerator2, out current2);
                }
            }
        }

        public static bool TryMoveNext<T>(this IEnumerator<T> enumerator1, out T current)
        {
            if (enumerator1.MoveNext())
            {
                current = enumerator1.Current;
                return true;
            }
            else
            {
                current = default(T);
                return false;
            }
        }

        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                value = defaultValue;
            }

            return value;
        }

        public static TValue GetOrAdd<TKey, TValue, TArg>(this IDictionary<TKey, TValue> dictionary, TKey key, TArg arg, Func<TKey, TArg, TValue> valueFactory)
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                value = valueFactory(key, arg);
                dictionary[key] = value;
            }

            return value;
        }

        public static TValue GetIfOrAdd<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> map, TKey key, Func<TKey, TValue> valueFactory, Func<TValue, bool> shouldReplace)
        {
            if (map.TryGetValue(key, out var value))
            {
                if (!shouldReplace(value))
                {
                    return value;
                }
                else
                {
                    map.TryRemove(new(key, value));
                }
            }

            return map.GetOrAdd(key, valueFactory);
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                dictionary[key] = defaultValue;
                value = defaultValue;
            }

            return value;
        }

        public static Dictionary<TKey, TValue> ToDictionarySafe<TValue, TKey>(this IEnumerable<TValue> source, Func<TValue, TKey> keySelector, IEqualityComparer<TKey> comparer = null, bool overwrite = false)
        {
            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);

            foreach (var element in source)
            {
                var key = keySelector(element);

                if (overwrite)
                {
                    result[key] = element;
                }
                else if (!result.ContainsKey(key))
                {
                    result.Add(key, element);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts sequence to dictionary, but accepts duplicate keys. First will win.
        /// </summary>
        public static Dictionary<TKey, TValue> ToDictionarySafe<T, TKey, TValue>(this IEnumerable<T> source, Func<T, TKey> keySelector,
            Func<T, TValue> valueSelector, IEqualityComparer<TKey> comparer = null, bool overwrite = false)
        {
            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);

            foreach (var element in source)
            {
                var key = keySelector(element);
                var value = valueSelector(element);

                if (overwrite)
                {
                    result[key] = value;
                }
                else if (!result.ContainsKey(key))
                {
                    result.Add(key, value);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a difference between <paramref name="leftItems"/> sequence and <paramref name="rightItems"/> sequence assuming that both sequences are sorted.
        /// </summary>
        public static IEnumerable<T> SortedExcept<T, TComparable>(
            IEnumerable<T> leftItems,
            IEnumerable<T> rightItems,
            Func<T, TComparable> getComparable)
            where TComparable : IComparable<TComparable>
        {
            foreach (var mergeItem in DistinctMergeSorted(leftItems, rightItems, getComparable, getComparable))
            {
                if (mergeItem.mode == MergeMode.LeftOnly)
                {
                    yield return mergeItem.left;
                }
                else
                {
                    // items in both or just the right
                    // are excluded from the result set
                }
            }
        }

        /// <summary>
        /// Returns a difference between <paramref name="leftItems"/> sequence and <paramref name="rightItems"/> sequence assuming that both sequences are sorted.
        /// </summary>
        public static IEnumerable<(T item, MergeMode mode)> DistinctDiffSorted<T, TComparable>(
            IEnumerable<T> leftItems,
            IEnumerable<T> rightItems,
            Func<T, TComparable> getComparable)
            where TComparable : IComparable<TComparable>
        {
            foreach (var mergeItem in DistinctMergeSorted(leftItems, rightItems, getComparable, getComparable))
            {
                if (mergeItem.mode == MergeMode.LeftOnly)
                {
                    yield return (mergeItem.left, mergeItem.mode);
                }
                else if (mergeItem.mode == MergeMode.RightOnly)
                {
                    yield return (mergeItem.right, mergeItem.mode);
                }
            }
        }

        public static IEnumerable<(T current, T? next, int index, bool isLast)> WithNexts<T>(this IEnumerable<T> items)
            where T : struct
        {
            (T item, int index, bool isLast) current = default;
            foreach (var item in items.WithIndicesAndIsLast())
            {
                if (item.index != 0)
                {
                    yield return (current.item, item.item, current.index, isLast: false);
                }

                if (item.isLast)
                {
                    yield return (item.item, default, item.index, item.isLast);
                }

                current = item;
            }
        }

        public static IEnumerable<(T current, T? prior, int index, bool isLast)> WithPriors<T>(this IEnumerable<T> items)
        {
            (T item, int index, bool isLast) current = default;
            foreach (var item in items.WithIndicesAndIsLast())
            {
                yield return (item.item, current.item, current.index, isLast: false);

                current = item;
            }
        }

        public static IEnumerable<(T item, int index, bool isLast)> WithIndicesAndIsLast<T>(this IEnumerable<T> items)
        {
            bool isFirst = true;
            (T item, int index) current = default;
            foreach (var item in items.WithIndices())
            {
                if (item.Index > 0)
                {
                    yield return (current.item, current.index, isLast: false);
                }

                current = item;
                isFirst = false;
            }

            if (!isFirst)
            {
                yield return (current.item, current.index, isLast: true);
            }
        }

        public static IEnumerable<T> WrapEnumerator<T>(this IEnumerator<T> items)
        {
            while (items.TryMoveNext(out var current))
            {
                yield return current;
            }
        }

        public static IEnumerator<T> OutFirst<T>(this IEnumerable<T> items, out T first, out bool hasFirst)
        {
            hasFirst = false;
            first = default;

            var enumerator = items.GetEnumerator();

            foreach (var item in items)
            {
                if (!hasFirst)
                {
                    first = item;
                    hasFirst = true;
                    return enumerator;
                }
            }

            return enumerator;
        }

        /// <summary>
        /// Gets a valid item from a merge tuple
        /// </summary>
        public static T Either<T>(this (T left, T right, MergeMode mode) mergeItem)
        {
            return mergeItem.mode == MergeMode.LeftOnly ? mergeItem.left : mergeItem.right;
        }

        /// <summary>
        /// Gets a valid item from a merge tuple
        /// </summary>
        public static TCommon Either<TLeft, TRight, TCommon>(this (TLeft left, TRight right, MergeMode mode) mergeItem, TCommon hint)
            where TLeft : TCommon
            where TRight : TCommon
        {
            return mergeItem.mode == MergeMode.LeftOnly ? mergeItem.left : mergeItem.right;
        }

        /// <summary>
        /// Gets a valid item from a merge tuple
        /// </summary>
        public static TCommon Least<TLeft, TRight, TCommon>(this (TLeft left, TRight right, MergeMode mode) mergeItem, IComparer<TCommon> comparer)
            where TLeft : TCommon
            where TRight : TCommon
        {
            return mergeItem.mode == MergeMode.Both
                ? (comparer.CompareItems(mergeItem.left, mergeItem.right) == CompareResult.RightGreater ? mergeItem.left : mergeItem.right)
                : mergeItem.Either<TCommon>();
        }

        public static T GetSortedFirst<T>(this IComparer<T> sorter, T left, T right)
        {
            return sorter.CompareItems(left, right) == CompareResult.RightGreater ? left : right;
        }

        public static TCommon Greatest<TLeft, TRight, TCommon>(this (TLeft left, TRight right, MergeMode mode) mergeItem, IComparer<TCommon> comparer)
            where TLeft : TCommon
            where TRight : TCommon
        {
            return mergeItem.mode == MergeMode.Both
                ? (comparer.CompareItems(mergeItem.left, mergeItem.right) == CompareResult.RightGreater ? mergeItem.right : mergeItem.left)
                : mergeItem.Either<TCommon>();
        }

        public static (TLeft left, TRight right, MergeMode mode) GreatestOnly<TLeft, TRight, TCommon>(this (TLeft left, TRight right, MergeMode mode) mergeItem, IComparer<TCommon> comparer)
            where TLeft : TCommon
            where TRight : TCommon
        {
            return mergeItem.mode == MergeMode.Both
                ? (comparer.CompareItems(mergeItem.left, mergeItem.right, equal: mergeItem, rightGreater: mergeItem.RightOnly(), leftGreater: mergeItem.LeftOnly()))
                : mergeItem;
        }

        public static (TLeft left, TRight right, MergeMode mode) LeastOnly<TLeft, TRight, TCommon>(this (TLeft left, TRight right, MergeMode mode) mergeItem, IComparer<TCommon> comparer)
            where TLeft : TCommon
            where TRight : TCommon
        {
            return mergeItem.mode == MergeMode.Both
                ? (comparer.CompareItems(mergeItem.left, mergeItem.right, equal: mergeItem, leftGreater: mergeItem.RightOnly(), rightGreater: mergeItem.LeftOnly()))
                : mergeItem;
        }

        public static (TLeft left, TRight right, MergeMode mode) RightOnly<TLeft, TRight>(this (TLeft left, TRight right, MergeMode mode) mergeItem)
        {
            return mergeItem with { left = default, mode = MergeMode.RightOnly };
        }

        public static (TLeft left, TRight right, MergeMode mode) LeftOnly<TLeft, TRight>(this (TLeft left, TRight right, MergeMode mode) mergeItem)
        {
            return mergeItem with { right = default, mode = MergeMode.LeftOnly };
        }

        /// <summary>
        /// Merges two sorted sequences.
        /// </summary>
        public static IEnumerable<(TLeft left, TRight right, MergeMode mode)> DistinctMergeSorted<TLeft, TRight, TComparable>(
            IEnumerable<TLeft> leftItems,
            IEnumerable<TRight> rightItems,
            Func<TLeft, TComparable> getLeftComparable,
            Func<TRight, TComparable> getRightComparable)
            where TComparable : IComparable<TComparable>
        {
            var leftEnumerator = leftItems.SortedUnique(getLeftComparable).GetEnumerator();
            var rightEnumerator = rightItems.SortedUnique(getRightComparable).GetEnumerator();

            return DistinctMergeSorted(leftEnumerator, rightEnumerator, getLeftComparable, getRightComparable);
        }

        /// <summary>
        /// Merges two sorted sequences with no duplicates.
        /// </summary>
        public static IEnumerable<(T left, T right, MergeMode mode, MergeMode hasMode)> DistinctMergeSorted<T>(
            IIterator<T> leftEnumerator,
            IIterator<T> rightEnumerator,
            IComparer<T> comparer)
        {
            bool hasCurrentLeft = leftEnumerator.TryMoveNext(out var leftCurrent);
            bool hasCurrentRight = rightEnumerator.TryMoveNext(out var rightCurrent);

            while (hasCurrentLeft || hasCurrentRight)
            {
                if (!hasCurrentRight)
                {
                    do
                    {
                        yield return (leftCurrent, default(T), MergeMode.LeftOnly, MergeMode.LeftOnly);
                    }
                    while (leftEnumerator.TryMoveNext(out leftCurrent));

                    yield break;
                }
                else if (!hasCurrentLeft)
                {
                    do
                    {
                        yield return (default(T), rightCurrent, MergeMode.RightOnly, MergeMode.RightOnly);
                    }
                    while (rightEnumerator.TryMoveNext(out rightCurrent));

                    yield break;
                }
                else // hasCurrent1 && hasCurrent2
                {
                    var comparison = comparer.OrderItems(leftCurrent, rightCurrent);
                    if (comparison.IsEqual) // (leftCurrent == rightCurrent)
                    {
                        yield return (leftCurrent, rightCurrent, MergeMode.Both, MergeMode.Both);
                        hasCurrentLeft = leftEnumerator.TryMoveNext(out leftCurrent);
                        hasCurrentRight = rightEnumerator.TryMoveNext(out rightCurrent);
                    }
                    else if (comparison.IsRightGreater) // (leftCurrent < rightCurrent)
                    {
                        yield return (leftCurrent, default(T), MergeMode.LeftOnly, MergeMode.Both);
                        hasCurrentLeft = leftEnumerator.TryMoveNext(out leftCurrent);
                    }
                    else // comparison > 0 (leftCurrent > rightCurrent)
                    {
                        yield return (default(T), rightCurrent, MergeMode.RightOnly, MergeMode.Both);
                        hasCurrentRight = rightEnumerator.TryMoveNext(out rightCurrent);
                    }
                }
            }
        }

        /// <summary>
        /// Merges two sorted sequences with no duplicates.
        /// </summary>
        public static IEnumerable<(TLeft left, TRight right, MergeMode mode)> DistinctMergeSorted<TLeft, TRight, TComparable>(
            IEnumerator<TLeft> leftEnumerator,
            IEnumerator<TRight> rightEnumerator,
            Func<TLeft, TComparable> getLeftComparable,
            Func<TRight, TComparable> getRightComparable)
            where TComparable : IComparable<TComparable>
        {
            bool hasCurrentLeft = TryMoveNext(leftEnumerator, out var leftCurrent);
            bool hasCurrentRight = TryMoveNext(rightEnumerator, out var rightCurrent);

            while (hasCurrentLeft || hasCurrentRight)
            {
                if (!hasCurrentRight)
                {
                    do
                    {
                        yield return (leftCurrent, default(TRight), MergeMode.LeftOnly);
                    }
                    while (TryMoveNext(leftEnumerator, out leftCurrent));

                    yield break;
                }
                else if (!hasCurrentLeft)
                {
                    do
                    {
                        yield return (default(TLeft), rightCurrent, MergeMode.RightOnly);
                    }
                    while (TryMoveNext(rightEnumerator, out rightCurrent));

                    yield break;
                }
                else // hasCurrent1 && hasCurrent2
                {
                    var comparison = getLeftComparable(leftCurrent).Compare(getRightComparable(rightCurrent));
                    if (comparison == CompareResult.Equal) // (leftCurrent == rightCurrent)
                    {
                        yield return (leftCurrent, rightCurrent, MergeMode.Both);
                        hasCurrentLeft = TryMoveNext(leftEnumerator, out leftCurrent);
                        hasCurrentRight = TryMoveNext(rightEnumerator, out rightCurrent);
                    }
                    else if (comparison == CompareResult.RightGreater) // (leftCurrent < rightCurrent)
                    {
                        yield return (leftCurrent, default(TRight), MergeMode.LeftOnly);
                        hasCurrentLeft = TryMoveNext(leftEnumerator, out leftCurrent);
                    }
                    else // comparison > 0 (leftCurrent > rightCurrent)
                    {
                        yield return (default(TLeft), rightCurrent, MergeMode.RightOnly);
                        hasCurrentRight = TryMoveNext(rightEnumerator, out rightCurrent);
                    }
                }
            }
        }

        private static bool MoveNextUntilDifferent<T, TOther>(
            IEnumerator<T> enumerator,
            TOther comparisonValue,
            out T current,
            Func<T, TOther, bool> equal)
        {
            while (TryMoveNext(enumerator, out current))
            {
                if (!equal(current, comparisonValue))
                {
                    return true;
                }
            }

            return false;
        }

        public static int? ChainCompareTo<T>(this T left, T right)
            where T : IComparable<T>
        {
            var compareResult = left.CompareTo(right);
            if (compareResult == 0)
            {
                return null;
            }
            else
            {
                return compareResult;
            }
        }

        public static CompareResult Compare<T>(this T left, T right)
            where T : IComparable<T>
        {
            var compareResult = left.CompareTo(right);
            if (compareResult == 0)
            {
                return CompareResult.Equal;
            }
            else if (compareResult < 0)
            {
                return CompareResult.RightGreater;
            }
            else
            {
                return CompareResult.LeftGreater;
            }
        }

        public static OrderResult AsOrder(this int compareResult)
        {
            return new(compareResult);
        }

        public static OrderResult Order<T>(this T left, in T right)
            where T : IComparable<T>
        {
            return new(left.CompareTo(right));
        }

        public static OrderResult OrderWith<T, TOther>(this T left, in TOther right)
            where T : IComparable<TOther>
        {
            return new(left.CompareTo(right));
        }

        public static OrderResult OrderItems<T>(this IComparer<T> comparer, in T left, in T right)
        {
            return new(comparer.Compare(left, right));
        }

        public static T Min<T>(this IComparer<T> comparer, in T left, in T right)
        {
            return comparer.OrderItems(left, right).IsRightLesser ? right : left;
        }

        public static T Max<T>(this IComparer<T> comparer, in T left, in T right)
        {
            return comparer.OrderItems(left, right).IsRightGreater ? right : left;
        }

        public static T Min<T>(this T left, in T right)
            where T : IComparable<T>
        {
            return Comparer<T>.Default.Min(left, right);
        }

        public static T Max<T>(this T left, in T right)
            where T : IComparable<T>
        {
            return Comparer<T>.Default.Max(left, right);
        }

        public static CompareResult CompareItems<T>(this IComparer<T> comparer, T left, T right)
        {
            return comparer.CompareItems(left, right, equal: CompareResult.Equal, rightGreater: CompareResult.RightGreater, leftGreater: CompareResult.LeftGreater);
        }

        public static TResult CompareItems<T, TResult>(this IComparer<T> comparer, T left, T right, TResult equal, TResult rightGreater, TResult leftGreater)
        {
            var compareResult = comparer.Compare(left, right);
            if (compareResult == 0)
            {
                return equal;
            }
            else if (compareResult < 0)
            {
                return rightGreater;
            }
            else
            {
                return leftGreater;
            }
        }

        public struct ArrayPoolLease<T>(T[] array, ArrayPool<T> pool) : IDisposable
        {
            public void Dispose()
            {
                pool.Return(array);
            }
        }

        /// <nodoc />
        public enum CompareResult
        {
            /// <nodoc />
            LeftGreater,

            /// <nodoc />
            RightGreater,

            /// <nodoc />
            Equal
        }

        /// <nodoc />
        [Flags]
        public enum MergeMode
        {
            /// <nodoc />
            LeftOnly = 1,

            /// <nodoc />
            RightOnly = 1 << 1,

            /// <nodoc />
            Both = LeftOnly | RightOnly
        }
    }

    public record struct OrderResult(int Result)
    {
        public bool IsRightAfter => Result < 0;
        public bool IsRightBefore => Result > 0;
        public bool IsRightAfterOrEqual => Result <= 0;
        public bool IsRightBeforeOrEqual => Result >= 0;
        public bool IsLeftAfter => Result > 0;
        public bool IsLeftBefore => Result < 0;
        public bool IsLeftAfterOrEqual => Result >= 0;
        public bool IsLeftBeforeOrEqual => Result <= 0;

        public bool IsRightGreater => Result < 0;
        public bool IsRightLesser => Result > 0;
        public bool IsRightGreaterOrEqual => Result <= 0;
        public bool IsRightLesserOrEqual => Result >= 0;
        public bool IsLeftGreater => Result > 0;
        public bool IsLeftLesser => Result < 0;
        public bool IsLeftGreaterOrEqual => Result >= 0;
        public bool IsLeftLesserOrEqual => Result <= 0;
        public bool IsEqual => Result == 0;

        public static implicit operator OrderResult(int compareResult)
        {
            return new(compareResult);
        }
    }
}
