// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static Codex.Utilities.Serialization.SpanSerializationExtensions;

#nullable enable

namespace Codex.Utilities.Serialization
{
    /// <summary>
    /// A set of extension methods for <see cref="SpanReader"/>.
    /// </summary>
    /// <remarks>
    /// This class mimics the API available via <see cref="System.IO.BinaryReader"/>,
    /// and <see cref="System.IO.BinaryWriter"/>, but for (de)serializing
    /// entities from a <see cref="ReadOnlySpan{T}"/> and <see cref="Span{T}"/> instead of a stream.
    /// </remarks>
    public static class SpanSerializationExtensions
    {
        public static Span<T> Truncate<T>(this Span<T> span, int maxLength) => span.Length <= maxLength ? span : span.Slice(0, maxLength);

        public static Span<T> Slice<T>(this Span<T> span, Extent extent) => span.Slice(extent.Start, extent.Length);
        public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> span, Extent extent) => span.Slice(extent.Start, extent.Length);

        public static Memory<T> Slice<T>(this Memory<T> Memory, Extent extent) => Memory.Slice(extent.Start, extent.Length);
        public static ReadOnlyMemory<T> Slice<T>(this ReadOnlyMemory<T> Memory, Extent extent) => Memory.Slice(extent.Start, extent.Length);

        /// <summary>
        /// Interprets a struct instance <paramref name="value"/> as an array of bytes.
        /// </summary>
        /// <remarks>
        /// The method is memory safe only if the lifetime of the resulting span is shorter then the lifetime of the <paramref name="value"/>.
        /// I.e. the following code is not safe: <code>ReadOnlySpan&lt;byte&gt; Unsafe() => AsBytesUnsafe(42); </code>.
        ///
        /// But besides the lifetime issue, this method is memory safe because it relies on the runtime support for <see cref="Span{T}"/> and
        /// <see cref="ReadOnlySpan{T}"/> that allows the GC to track the lifetime of <paramref name="value"/> even if it's an
        /// interior pointer of a managed object.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlySpan<byte> AsBytesUnsafe<T>(in T value)
            where T : unmanaged
        {
            return MemoryMarshal.CreateReadOnlySpan(
                    reference: ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in value)),
                    length: Unsafe.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlySpan<byte> AsBytesUnsafe<T>(this In<T> value)
            where T : unmanaged
        {
            return MemoryMarshal.CreateReadOnlySpan(
                    reference: ref Unsafe.As<T, byte>(ref Unsafe.AsRef(value.Value)),
                    length: Unsafe.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlySpan<T> AsReadOnlySpanUnsafe<T>(in T value)
        {
            return MemoryMarshal.CreateReadOnlySpan(
                    reference: ref Unsafe.AsRef(in value),
                    length: 1);
        }

        public static ReadOnlySpan<byte> ToBytes<T>(this ReadOnlySpan<T> span)
            where T : unmanaged
        {
            return MemoryMarshal.AsBytes(span);
        }

        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this In<T> value)
            where T : unmanaged
        {
            if (!value.IsValid) return default;

            return AsReadOnlySpanUnsafe(value.Value);
        }

        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this ref T value)
            where T : unmanaged
        {
            return MemoryMarshal.CreateReadOnlySpan(ref value, 1);
        }

        public static Span<T> AsSpan<T>(this ref T value)
            where T : struct
        {
            return MemoryMarshal.CreateSpan(ref value, 1);
        }

        public static ReadOnlySpan<byte> AsBytes<T>(this ref T value)
            where T : unmanaged
        {
            return MemoryMarshal.AsBytes(AsReadOnlySpan(ref value));
        }

        public static Span<byte> AsWritableBytes<T>(this ref T value)
            where T : unmanaged
        {
            return MemoryMarshal.AsBytes(AsSpan(ref value));
        }

        public static ReadOnlySpanScope<T> AsScope<T>(this ReadOnlySpan<T> span)
            where T : unmanaged
            => ReadOnlySpanScope<T>.FromSpan(span);

        public static ReadOnlySpanScope<T> AsReadOnlyScope<T>(this Span<T> span)
            where T : unmanaged
            => ReadOnlySpanScope<T>.FromSpan(span);

        public static SpanScope<T> AsScope<T>(this Span<T> span)
            where T : unmanaged
            => SpanScope<T>.FromSpan(span);

        /// <nodoc />
        public static ReadOnlySpan<byte> AsReadOnlySpan(this ArraySegment<byte> span) => span;

        /// <nodoc />
        public static ReadOnlySpan<T> AsReadOnly<T>(this Span<T> span) => span;

        /// <nodoc />
        public static SpanReader AsReader(this ReadOnlySpan<byte> reader) => new SpanReader(reader);

        /// <nodoc />
        public static SpanReader AsReader(this Span<byte> reader) => new SpanReader(reader);

        /// <nodoc />
        public static T Read<T>(this ref Span<byte> span, ReadItemFromSpan<T> read)
        {
            var reader = AsReader(span);
            return read(ref reader);
        }

        /// <nodoc />
        public static SpanWriter AsWriter(this Span<byte> reader) => new SpanWriter(reader);

        /// <nodoc />
        public static bool ReadBoolean(this ref SpanReader reader) => reader.ReadByte() != 0;

        /// <nodoc />
        public static int ReadInt32(this ref SpanReader reader)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(reader.ReadSpan(sizeof(int)));
        }

        /// <nodoc />
        public static long ReadInt64(this ref SpanReader reader)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(reader.ReadSpan(sizeof(long)));
        }

        /// <summary>
        /// Reads <see cref="uint"/>.
        /// </summary>
        public static uint ReadUInt32Compact(this ref SpanReader reader)
        {
            var value = reader.Read7BitEncodedInt();
            return unchecked((uint)value);
        }

        /// <nodoc />
        public static void WriteCompact(this ref SpanWriter writer, uint value)
        {
            writer.Write7BitEncodedInt(unchecked((int)value));
        }

        /// <nodoc />
        public static int ReadInt32Compact(this ref SpanReader reader)
        {
            return reader.Read7BitEncodedInt();
        }

        /// <nodoc />
        public static ushort ReadUInt16(this ref SpanReader reader)
            => BinaryPrimitives.ReadUInt16LittleEndian(reader.ReadSpan(sizeof(ushort)));

        /// <nodoc />
        public static long ReadInt64Compact(this ref SpanReader reader)
        {
            return reader.Read7BitEncodedLong();
        }

        /// <summary>
        /// Compactly writes an int
        /// </summary>
        public static void WriteInt32Compact(this ref SpanWriter writer, int value)
        {
            writer.Write7BitEncodedInt(value);
        }

        /// <summary>
        /// Compactly writes a int positive or negative int
        /// </summary>
        public static void WriteZigZag(this ref SpanWriter writer, long value)
        {
            if (value < 0)
            {
                value = ~value;
                value <<= 1;
                value |= 1;
            }
            else
            {
                value <<= 1;
            }

            writer.WriteCompact(value);
        }

        /// <nodoc />
        public static int ReadInt32ZigZag(this ref SpanReader reader)
        {
            return checked((int)reader.ReadInt64ZigZag());
        }

        public static long ReadInt64ZigZag(this ref SpanReader reader)
        {
            var value = reader.Read7BitEncodedLong();
            if (value.HasFlag(1))
            {
                value >>>= 1;
                value = ~value;
            }
            else
            {
                value >>>= 1;
            }

            return value;
        }

        /// <nodoc />
        public static void WriteCompact(this ref SpanWriter writer, long value)
        {
            writer.WriteCompact(unchecked((ulong)value));
        }

        /// <nodoc />
        public static void Write7BitEncodedInt(this ref SpanWriter writer, int value)
        {
            uint uValue = unchecked((uint)value);

            // Write out an int 7 bits at a time. The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            //
            // Using the constants 0x7F and ~0x7F below offers smaller
            // codegen than using the constant 0x80.

            while (uValue > 0x7Fu)
            {
                writer.Write(unchecked((byte)(uValue | ~0x7Fu)));
                uValue >>= 7;
            }

            writer.Write((byte)uValue);
        }

        /// <nodoc />
        public static void WriteCompact(this ref SpanWriter writer, ulong value)
        {
            int writeCount = 1;
            unchecked
            {
                // Write out a long 7 bits at a time.  The high bit of the byte,
                // when on, tells reader to continue reading more bytes.
                while (value >= 0x80)
                {
                    writeCount++;
                    writer.Write((byte)(value | 0x80));
                    value >>= 7;
                }

                writer.Write((byte)value);
            }
        }

        /// <nodoc />
        public static TimeSpan ReadTimeSpan(this ref SpanReader reader) =>
            TimeSpan.FromTicks(reader.Read7BitEncodedLong());

        /// <nodoc />
        public static DateTime ReadDateTime(this ref SpanReader reader) =>
            DateTime.FromBinary(reader.ReadInt64());

        internal static int Read7BitEncodedInt(this ref SpanReader reader)
        {
            // Unlike writing, we can't delegate to the 64-bit read on
            // 64-bit platforms. The reason for this is that we want to
            // stop consuming bytes if we encounter an integer overflow.

            uint result = 0;
            byte byteReadJustNow;

            // Read the integer 7 bits at a time. The high bit
            // of the byte when on means to continue reading more bytes.
            //
            // There are two failure cases: we've read more than 5 bytes,
            // or the fifth byte is about to cause integer overflow.
            // This means that we can read the first 4 bytes without
            // worrying about integer overflow.

            const int MaxBytesWithoutOverflow = 4;
            for (var shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                // ReadByte handles end of stream cases for us.
                byteReadJustNow = reader.ReadByte();
                unchecked
                {
                    result |= (byteReadJustNow & 0x7Fu) << shift;
                }

                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int)result; // early exit
                }
            }

            // Read the 5th byte. Since we already read 28 bits,
            // the value of this byte must fit within 4 bits (32 - 28),
            // and it must not have the high bit set.

            byteReadJustNow = reader.ReadByte();
            if (byteReadJustNow > 0b_1111u)
            {
                // throw new FormatException(SR.Format_Bad7BitInt);
                throw new FormatException();
            }

            result |= (uint)byteReadJustNow << MaxBytesWithoutOverflow * 7;
            return unchecked((int)result);
        }

        /// <summary>
        /// The method returns a byte array from <paramref name="reader"/>, please note, that the length 
        /// of the final array might be smaller than the given <paramref name="length"/> if <paramref name="allowIncomplete"/>
        /// is true (false by default).
        /// </summary>
        public static byte[] ReadBytes(this ref SpanReader reader, int length, bool allowIncomplete = false)
        {
            // This implementation's behavior when incomplete = true
            // mimics BinaryReader.ReadBytes which allows
            // returning an array less than the size requested.
            return reader.ReadSpan(length, allowIncomplete).ToArray();
        }

        /// <nodoc />
        internal static long Read7BitEncodedLong(this ref SpanReader reader)
        {
            // Read out an Int64 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            long count = 0;
            var shift = 0;
            byte b;
            do
            {
                // ReadByte handles end of stream cases for us.
                b = reader.ReadByte();
                long m = b & 0x7f;
                count |= m << shift;
                shift += 7;
            }
            while ((b & 0x80) != 0);
            return count;
        }

        /// <nodoc />
        public delegate T ReadItemFromSpan<out T>(ref SpanReader reader);

        /// <nodoc />
        public delegate void WriteItemToSpan<in T>(ref SpanWriter writer, T item);

        /// <nodoc />
        public static T[] ReadArray<T>(this ref SpanReader reader, ReadItemFromSpan<T> itemReader, int minimumLength = 0)
        {
            var length = reader.ReadInt32Compact();
            if (length == 0)
            {
                return Array.Empty<T>();
            }

            var array = reader.ReadArrayCore(itemReader, length, minimumLength: minimumLength);

            return array;
        }

        private static T[] ReadArrayCore<T>(this ref SpanReader reader, ReadItemFromSpan<T> itemReader, int length, int minimumLength = 0)
        {
            var arrayLength = Math.Max(minimumLength, length);
            var array = arrayLength == 0 ? Array.Empty<T>() : new T[arrayLength];
            for (var i = 0; i < length; i++)
            {
                array[i] = itemReader(ref reader);
            }

            return array;
        }

        public static int SafeCopyTo<T>(this ReadOnlySpan<T> span, Span<T> target)
        {
            var copyLength = Math.Min(span.Length, target.Length);
            span.Slice(0, copyLength).CopyTo(target);
            return copyLength;
        }

        /// <nodoc />
        public static T Read<T>(this Span<byte> bytes)
            where T : unmanaged
        {
            var reader = bytes.AsReader();
            return reader.Read<T>();
        }

        /// <nodoc />
        public static T Read<T>(this ref SpanReader reader)
            where T : unmanaged
        {
            var itemSpan = reader.ReadSpan(Unsafe.SizeOf<T>());
            var result = MemoryMarshal.Read<T>(itemSpan);
            return result;
        }

        /// <nodoc />
        public static void Write(this ref SpanWriter writer, byte value) => writer.WriteByte(value); // Using a special overload instead of using a generic method for performance reasons

        /// <nodoc />
        public static void Write(this ref SpanWriter writer, ushort value) => writer.WriteShort(value); // Using a special overload instead of using a generic method for performance reasons

        /// <nodoc />
        public static void Write<T>(this ref SpanWriter writer, T value)
            where T : unmanaged
        {
#if NET5_0_OR_GREATER
            // This version only works in .NET Core, because CreateReadOnlySpan is not available for full framework.
            var bytes = MemoryMarshal.CreateReadOnlySpan(
                reference: ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in value)),
                length: Unsafe.SizeOf<T>());
            writer.WriteSpan(bytes);
#else
            // For the full framework case (that is not used in production on a hot paths)
            // using an array pool with 1 element to avoid stackalloc approach that
            // causes issues because writer.WriteSpan can be re-created on the fly with a new instance from the array writer.
            using var pooledHandle = ConversionArrayPool.Array<T>.ArrayPool.GetInstance(1);
            var input = pooledHandle.Instance;
            input[0] = value;

            var bytes = MemoryMarshal.AsBytes(input.AsSpan());
            writer.WriteSpan(bytes);
#endif
        }

        /// <nodoc />
        public static void Write<T>(this ref SpanWriter writer, Span<T> span)
            where T : unmanaged
        {
            var bytes = MemoryMarshal.AsBytes(span);
            writer.WriteSpan(bytes);
        }

        /// <nodoc />
        public static void Write(this ref SpanWriter writer, Span<byte> bytes)
        {
            writer.WriteSpan(bytes);
        }

        public static void Serialize<T>(this ref SpanWriter writer, T value)
            where T : ISpanSerializable<T>
        {
            value.Serialize(ref writer);
        }

        public static T Deserialize<T>(this ref SpanReader reader)
            where T : ISpanSerializable<T>
        {
            return T.Deserialize(ref reader);
        }

        /// <summary>
        /// Writes an array.
        /// </summary>
        public static void Write<T>(this ref SpanWriter writer, T[] value, WriteItemToSpan<T> write)
        {
            WriteReadOnlyListCore(ref writer, value, write);
        }

        /// <summary>
        /// Writes a readonly list.
        /// </summary>
        public static void Write<T>(this ref SpanWriter writer, IReadOnlyList<T> value, WriteItemToSpan<T> write)
        {
            WriteReadOnlyListCore(ref writer, value, write);
        }

        private static void WriteReadOnlyListCore<T, TReadOnlyList>(this ref SpanWriter writer, TReadOnlyList value, WriteItemToSpan<T> write)
            where TReadOnlyList : IReadOnlyList<T>
        {
            writer.WriteInt32Compact(value.Count);
            for (int i = 0; i < value.Count; i++)
            {
                write(ref writer, value[i]);
            }
        }

        private static void WriteToOutStream(ref SpanWriter writer, byte[] buffer, int offset, int count)
        {
            writer.Write(buffer.AsSpan(offset, count));
        }

        public static SpanCastWrapper<T> Wrap<T>(this Span<T> span)
            where T : unmanaged
        {
            return new SpanCastWrapper<T>(span);
        }

        public ref struct SpanCastWrapper<T>(Span<T> Span)
            where T : unmanaged
        {
            public Span<T> Span { get; } = Span;

            public Span<TOther> Cast<TOther>()
                where TOther : unmanaged
            {
                return MemoryMarshal.Cast<T, TOther>(Span);
            }
        }
    }
}
