using Codex.Utilities;
using Codex.Utilities.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Codex.Sdk.Utilities
{
    public class EncoderContext
    {
        public readonly StringBuilder StringBuilder = new StringBuilder();
        public readonly StreamWriter Writer;
        public MemoryStream Stream { get; } = new MemoryStream();
        public readonly List<ulong> UIntList = new List<ulong>();
        public readonly CodexArrayBufferWriter<byte> ByteBuffer;
        public readonly char[] CharBuffer;

        public EncoderContext(int charBufferSize = 1024)
        {
            Writer = new StreamWriter(Stream);
            CharBuffer = new char[charBufferSize];
            ByteBuffer = new(Encoding.UTF8.GetMaxByteCount(charBufferSize));
        }

        public SpanWriter GetSpanWriter()
        {
            ByteBuffer.SetPosition(0);
            var writer = new SpanWriter(ByteBuffer);
            return writer;
        }

        public SpanReader GetStreamSpanReader(bool fromStart = false)
        {
            return SpanReader.FromMemoryStream(Stream, fromCurrent: !fromStart);
        }

        public void Reset()
        {
            Writer.Flush();
            ByteBuffer.SetPosition(0);
            StringBuilder.Clear();
            UIntList.Clear();
            ResetStreamOnly();
        }

        public void ResetStreamOnly()
        {
            Stream.SetLength(0);
            Stream.Position = 0;
        }

        public string ToBase64HashString(IEnumerable<string> content)
        {
            return ToHash(content).ToBase64String();
        }

        public MurmurHash ToHash(IEnumerable<string> contentStream)
        {
            return new Murmur3().ComputeHash(contentStream.SelectMany(content => GetByteStream(content)));
        }

        public string ToBase64HashString(string content)
        {
            return ToHash(content).ToBase64String();
        }

        public MurmurHash ToHash(string content)
        {
            return new Murmur3().ComputeHash(GetByteStream(content));
        }

        public MurmurHash ToHash(ReadOnlyMemory<char> content)
        {
            return new Murmur3().ComputeHash(GetByteStream(content));
        }

        public string ToBase64HashString(StringBuilder builder)
        {
            return new Murmur3().ComputeHash(GetByteStream(builder)).ToBase64String();
        }

        public MurmurHash ToHash(MemoryStream stream, int start = 0)
        {
            var buffer = stream.GetBuffer();
            return new Murmur3().ComputeHash(buffer, start, (int)(stream.Position - start));
        }

        public MurmurHash ToHash(int start = 0)
        {
            return ToHash(Stream, start);
        }

        public string ToBase64HashString(MemoryStream stream)
        {
            return ToHash(stream).ToBase64String();
        }

        private delegate void CopyChars<T>(T charSource, int offset, char[] charBuffer, int length);

        private IEnumerable<ReadOnlyMemory<byte>> GetByteStream<T>(T charSource, int length, CopyChars<T> copyTo)
        {
            int offset = 0;
            int remainingChars = length;
            var chars = CharBuffer;
            ByteBuffer.SetPosition(0);
            var bytes = ByteBuffer.GetMemory(ByteBuffer.Capacity);
            while (remainingChars > 0)
            {
                var copiedChars = Math.Min(remainingChars, chars.Length);
                copyTo(charSource, offset, chars, copiedChars);
                var byteLength = Encoding.UTF8.GetBytes(chars.AsSpan(0, copiedChars), bytes.Span);
                yield return bytes.Slice(0, byteLength);
                offset += copiedChars;
                remainingChars -= copiedChars;
            }
        }

        public IEnumerable<ReadOnlyMemory<byte>> GetByteStream(StringBuilder builder)
        {
            return GetByteStream(builder, builder.Length,
                (StringBuilder builder, int offset, char[] charBuffer, int length)
                    => builder.CopyTo(offset, charBuffer, 0, length));
        }

        public IEnumerable<ReadOnlyMemory<byte>> GetByteStream(string content)
        {
            return GetByteStream(content, content.Length,
                (string builder, int offset, char[] charBuffer, int length)
                    => builder.CopyTo(offset, charBuffer, 0, length));
        }

        public IEnumerable<ReadOnlyMemory<byte>> GetByteStream(ReadOnlyMemory<char> content)
        {
            return GetByteStream(content, content.Length,
                (ReadOnlyMemory<char> builder, int offset, char[] charBuffer, int length)
                    => builder.Slice(offset, length).CopyTo(charBuffer.AsMemory(0, length)));
        }
    }
}
