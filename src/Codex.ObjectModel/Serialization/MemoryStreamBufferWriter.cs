// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;

// The code is adopted from here: https://github.com/dotnet/runtime/blob/57bfe474518ab5b7cfe6bf7424a79ce3af9d6657/src/libraries/Common/src/System/Buffers/ArrayBufferWriter.cs
// with some minor changes
namespace Codex.Utilities.Serialization
{
    public record MemoryStreamBufferWriter(MemoryStream Stream) : IBufferWriter<byte>
    {
        public void Advance(int count)
        {
            Stream.Position += count;
            Stream.SetLength(Stream.Position);
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            byte[] buffer = EnsureBuffer(sizeHint);
            return buffer.AsMemory((int)Stream.Position, sizeHint);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            byte[] buffer = EnsureBuffer(sizeHint);
            return buffer.AsSpan((int)Stream.Position, sizeHint);
        }

        private byte[] EnsureBuffer(int sizeHint)
        {
            var end = Stream.Position + sizeHint;
            if (end > Stream.Capacity)
            {
                Stream.SetLength(end);
                Stream.SetLength(Stream.Position);
            }

            var buffer = Stream.GetBuffer();
            return buffer;
        }
    }
}