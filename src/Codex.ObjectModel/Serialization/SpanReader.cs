// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable

namespace Codex.Utilities.Serialization
{
    /// <summary>
    /// A lightweight wrapper around <see cref="ReadOnlySpan{T}"/> that tracks its position.
    /// </summary>
    /// <remarks>
    /// The main purpose of this type is deserializing instances directly from spans.
    /// Because its a struct, the deserialization methods should get the instance by ref in order for the caller methods to "observe" the position
    /// changes that happen during deserialization.
    /// </remarks>
    public ref struct SpanReader
    {
        public delegate bool RequestBytesDelegate(ref SpanReader reader, int requiredLength);

        public RequestBytesDelegate? RequestBytes;

        /// <summary>
        /// The original span.
        /// </summary>
        public ReadOnlySpan<byte> Span { get; }

        /// <summary>
        /// The current position inside the span. A valid range is [0..Span.Length].
        /// </summary>
        public int Position { get; set; }

        /// <nodoc />
        public bool IsEnd => Span.Length == Position;

        /// <summary>
        /// Returns a remaining length available by the reader.
        /// </summary>
        public int RemainingLength => Span.Length - Position;

        /// <summary>
        /// Gets the remaining data in the original span.
        /// </summary>
        public ReadOnlySpan<byte> Remaining => Span.Slice(Position);

        /// <nodoc />
        public SpanReader(ReadOnlySpan<byte> span)
            : this(span, position: 0, requestBytes: null)
        {
        }

        private SpanReader(ReadOnlySpan<byte> span, int position, RequestBytesDelegate? requestBytes)
        {
            Span = span;
            Position = position;
            RequestBytes = requestBytes;
        }

        /// <nodoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            EnsureLength(sizeof(byte));
            return Span[Position++];
        }

        /// <summary>
        /// Advances the current position by <paramref name="length"/>.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The operation will fail if <code>Position + length >= Span.Length;</code>.
        /// </exception>
        public void Advance(int length)
        {
            EnsureLength(length);
            Position += length;
        }

        /// <summary>
        /// The method returns a byte array from <seealso cref="Span"/>.
        /// Please note, that the length of the final array might be smaller than the given <paramref name="length"/>.
        /// </summary>
        public ReadOnlySpan<byte> ReadSpan(int length, bool allowIncomplete = false)
        {
            // This implementation mimics the one from BinaryReader that allows
            // getting back an array of a smaller size than requested.
            if (allowIncomplete)
            {
                length = Math.Min(RemainingLength, length);
            }

            var result = Span.Slice(Position, length);
            Position += length;
            return result;
        }

        /// <nodoc />
        public ReadOnlySpan<T> Read<T>(int count)
            where T : unmanaged
        {
            // Reading a span instead of reading bytes to avoid unnecessary allocations.
            var itemSpan = ReadSpan(Unsafe.SizeOf<T>() * count);
            var result = MemoryMarshal.Cast<byte, T>(itemSpan);
            return result;
        }

        internal void EnsureLength(int minLength)
        {
            if (RemainingLength < minLength && RequestBytes?.Invoke(ref this, minLength) != true)
            {
                // Extracting the throw method to make the current one inline friendly.
                InsufficientLengthException.Throw(minLength, RemainingLength);
            }
        }

        public static SpanReader FromMemoryStream(MemoryStream stream, bool fromCurrent = true)
        {
            var position = (int)stream.Position;
            var offset = fromCurrent ? position : 0;

            var buffer = stream.GetBuffer().AsSpan(0, (int)stream.Length).Slice(offset);
            var reader = new SpanReader(buffer, position: fromCurrent ? 0 : position, requestBytes: null);

            return reader;
        }

        /// <nodoc />
        public static implicit operator SpanReader(Span<byte> span)
        {
            return new SpanReader(span);
        }

        /// <nodoc />
        public static implicit operator SpanReader(ReadOnlySpan<byte> span)
        {
            return new SpanReader(span);
        }
    }
}