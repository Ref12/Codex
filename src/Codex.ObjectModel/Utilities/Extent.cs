using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.Utilities
{
    [DataContract]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Extent : IComparable<Extent>, IComparable<int>, IValueEnumerable<Extent, uint, int>, IReadOnlyCollection<int>
    {
        public static IComparer<Extent> StartComparer { get; } = new ComparerBuilder<Extent>()
            .CompareByAfter(e => e.Start);

        [DataMember(Order = 0)]
        [JsonInclude]
        public int Start;

        [DataMember(Order = 1)]
        [JsonInclude]
        public int Length;

        public int Last => EndExclusive - 1;

        public int EndExclusive => Start + Length;

        public bool IsEmpty => Length <= 0;

        int IReadOnlyCollection<int>.Count => Length;

        public Extent(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public static Extent FromBounds(int startInclusive, int endExclusive)
        {
            return new Extent(startInclusive, endExclusive - startInclusive);
        }

        public static Extent FromEnd(int end, int length)
        {
            return new(end - length, length);
        }

        public static void CheckBounds(int index, int count)
        {
            Contract.Assert(unchecked((uint)index < (uint)count));
        }

        public Extent? Intersect(Extent other)
        {
            var start = Math.Max(Start, other.Start);
            var end = Math.Min(EndExclusive, other.EndExclusive);
            return start <= end ? FromBounds(start, end) : null;
        }

        public int Constrain(int position)
        {
            var result = Math.Max(Start, position);
            result = Math.Min(EndExclusive, position);
            return result;
        }

        public Extent TruncateStart(int newLength)
        {
            Contract.Assert(newLength <= Length);
            var diff = Length - newLength;
            return new(Start + diff, newLength);
        }

        public Extent Slice(int relativeStart)
        {
            Contract.Assert(relativeStart <= Length);
            return new(Start + relativeStart, Length - relativeStart);
        }

        public Extent TruncateEnd(int newLength)
        {
            Contract.Assert(newLength <= Length);
            return new(Start, newLength);
        }

        public Extent MinEnd(int end)
        {
            return FromBounds(Start, Math.Min(end, EndExclusive));
        }

        public void SetEnd(int end)
        {
            Length = end - Start;
        }

        public void SetStart(int start)
        {
            Start = start;
            Length -= (start - Start);
        }

        public Extent Union(Extent other)
        {
            var start = Math.Min(Start, other.Start);
            var end = Math.Max(EndExclusive, other.EndExclusive);
            return FromBounds(start, end);
        }

        public Extent MakeRelative(int position)
        {
            Contract.Assert(position <= Start);
            return new(Start - position, Length);
        }

        public Extent Shift(int relativePosition)
        {
            return new(Start + relativePosition, Length);
        }

        public int CompareTo(Extent other)
        {
            if (Start > other.Last)
            {
                return RangeHelper.FirstGreaterThanSecond;
            }
            else if (Last < other.Start)
            {
                return RangeHelper.FirstLessThanSecond;
            }

            return RangeHelper.FirstEqualSecond;
        }

        public override string ToString()
        {
            return $"[{Start}-{EndExclusive}]";
        }

        public string Serialize()
        {
            return $"{Start}-{EndExclusive}";
        }

        public static Extent Parse(ReadOnlySpan<char> chars)
        {
            chars = chars.Trim("[]");
            var firstDot = chars.IndexOfAny("-.");
            return FromBounds(
                startInclusive: int.Parse(chars[0..firstDot], null),
                endExclusive: int.Parse(chars.Slice(firstDot + 1).Trim("-."), null));
        }

        public bool Contains(int position)
        {
            return Start <= position && position < EndExclusive;
        }

        public static bool operator ==(Extent a, Extent b)
        {
            return (a.Start, a.Length) == (b.Start, b.Length);
        }

        public static bool operator !=(Extent a, Extent b)
        {
            return (a.Start, a.Length) != (b.Start, b.Length);
        }

        public static implicit operator Extent((int startInclusive, int endExclusive) tuple)
        {
            return Extent.FromBounds(tuple.startInclusive, tuple.endExclusive);
        }

        public LongExtent ToLong()
        {
            return this;
        }

        public static bool IsValidIndex(int index, int count)
        {
            return unchecked((uint)index < (uint)count);
        }

        public int CompareTo(int position)
        {
            // first = Range
            // second = position
            if (position < Start)
            {
                return RangeHelper.FirstGreaterThanSecond;
            }
            else if (position >= EndExclusive)
            {
                return RangeHelper.FirstLessThanSecond;
            }
            else
            {
                return RangeHelper.FirstEqualSecond;
            }
        }

        public System.Range ToSystemRange() => new(Start, EndExclusive);

        public ValueEnumerator<(Range Self, uint State), int> GetEnumerator()
        {
            return new((this, 0), TryMoveNext);
        }

        public static bool TryMoveNext(ref (Range Self, uint State) state, out int current)
        {
            if (state.State < state.Self.Length)
            {
                current = (int)(state.State + state.Self.Start);
                state.State++;
                return true;
            }
            else
            {
                current = default;
                return false;
            }
        }
    }
}
