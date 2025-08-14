using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Codex.Utilities;

[GeneratorExclude]
public interface IBinaryItem<TSelf> : IBinaryItem
    where TSelf : IBinaryItem<TSelf>
{
    static abstract ReadOnlySpan<byte> GetSpan(in TSelf self);
}

[GeneratorExclude]
public interface IBinarySpanItem<TSelf> : IBinaryItem<TSelf>
    where TSelf : IBinaryItem<TSelf>
{
    static abstract TSelf FromSpan(ReadOnlySpan<byte> bytes);
}

[GeneratorExclude]
public interface IBinaryItem
{
    int StartOffset => 0;

    int EndOffset => Length;

    int Length { get; }
}

public static class BinaryItem
{
    public static void CopyTo<TBinaryItem>(this ref TBinaryItem item, Span<byte> span)
       where TBinaryItem : struct, IBinaryItem<TBinaryItem>
    {
        TBinaryItem.GetSpan(item).CopyTo(span);
    }

    public static ReadOnlySpan<byte> GetSpan<TBinaryItem>(this ref TBinaryItem item)
       where TBinaryItem : struct, IBinaryItem<TBinaryItem>
    {
        return TBinaryItem.GetSpan(item);
    }

    public static StructBinaryItem<T> Create<T>(T value, int length = -1)
        where T : unmanaged
    {
        return new StructBinaryItem<T>(value, length);
    }

    public static MemoryBinaryItem Create(ReadOnlyMemory<byte> bytes) => new MemoryBinaryItem(bytes);

    public record struct MemoryBinaryItem(ReadOnlyMemory<byte> Bytes) : IBinaryItem<MemoryBinaryItem>
    {
        public int Length => Bytes.Length;

        public static ReadOnlySpan<byte> GetSpan(in MemoryBinaryItem self)
        {
            return self.Bytes.Span;
        }
    }

    public record struct StructBinaryItem<T>(T Value, int Length = -1) : IBinaryItem<StructBinaryItem<T>>
        where T : unmanaged
    {
        public readonly T Value = Value;

        private static int Size { get; } = Unsafe.SizeOf<T>();

        public int Length { get; init; } = Length >= 0 ? Length : Size;

        public static ReadOnlySpan<byte> GetSpan(in StructBinaryItem<T> self)
        {
            return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(self.Value), 1)).Slice(0, self.Length);
        }

        public void CopyTo(Span<byte> target)
        {
            MemoryMarshal.AsBytes(stackalloc[] { Value }).Slice(0, Length).CopyTo(target);
        }

    }
}
