using System.Numerics;

namespace Codex.Utilities
{
    public record struct ByteCount(long Value) : IAdditionOperators<ByteCount, long, ByteCount>
    {
        public long Value = Value;

        public static ByteCount operator +(ByteCount left, long right)
        {
            return new ByteCount(left.Value + right);
        }

        public override string ToString()
        {
            var mb = Value / 1_000_000.0;

            return $"{mb:F2} MB";
        }

        public static implicit operator ByteCount(long Value) => new(Value);
    }

    public static class ByteCountExtensions
    {
        public static void InterlockedAdd(this ref ByteCount count, long bytes)
        {
            Interlocked.Add(ref count.Value, bytes);
        }
    }
}
