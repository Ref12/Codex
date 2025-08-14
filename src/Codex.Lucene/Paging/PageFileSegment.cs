using System.Diagnostics;

namespace Codex.Lucene.Search
{
    public record PageFileSegment(long Start, ReadOnlyMemory<byte> Bytes)
    {
        public SegmentSource Source { get; set; }
        public SegmentTrackerBase Tracker = SegmentTrackerBase.Instance;

        public static readonly PageFileSegment Empty = new PageFileSegment(0, Array.Empty<byte>());

        public int Length => Bytes.Length;
        public long End => Start + Length;

        public virtual int CopyTo(ref long position, byte[] buffer, ref int offset, ref int count)
        {
            Tracker.BeforeCopy(position);
            if (position < Start || position >= End)
            {
                Debug.Fail($"{position} < {Start} || {position} >= {End} ({offset}, {count})");
            }

            var bytesOffset = (int)(position - Start);
            var copyLength = Math.Min(count, Length - bytesOffset);
            Bytes.Span.Slice(bytesOffset, copyLength).CopyTo(buffer.AsSpan(offset, copyLength));

            position += copyLength;
            offset += copyLength;
            count -= copyLength;

            Tracker.AfterCopy(position);
            return copyLength;
        }

        public bool Contains(long position)
        {
            return (position >= Start) && (position < End);
        }
    }

    public enum SegmentSource
    {
        Unknown,
        Precache,
        Cache,
        Provider
    }

    public record SegmentTrackerBase()
    {
        public static SegmentTrackerBase Instance { get; } = new SegmentTrackerBase();

        public virtual void BeforeCopy(long position) { }
        public virtual void AfterCopy(long position) { }
    }
}
