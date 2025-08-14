using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Codex.ObjectModel.Attributes;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace Codex.Utilities
{
    public static class SerializationUtilities
    {
        public const int StableIdGroupMaxValue = byte.MaxValue;
        public const long MaxVersion = 1L << 40;

        public static readonly MemoryStream EmptyStream = new MemoryStream(Array.Empty<byte>(), 0, 0, writable: false, publiclyVisible: true);

        public static IEnumerable<ReadOnlyMemory<byte>> StreamSegments(this Stream stream, int bufferSize = 1 << 12)
        {
            var buffer = new byte[1024];
            var length = stream.Length;
            var remaining = length;
            while (remaining > 0)
            {
                var read = stream.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length));
                if (read <= 0)
                {
                    break;
                }

                remaining -= read;
                yield return buffer.AsMemory(0, read);
            }
        }

        public static byte[] ReadAllBytes(this Stream stream, bool dispose = true)
        {
            try
            {
                if (stream.CanSeek)
                {
                    var bytes = new byte[(int)stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                    return bytes;
                }
                else
                {
                    var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            finally
            {
                if (dispose)
                {
                    stream.Dispose();
                }
            }
        }

        public static string ReadAllText(this Stream stream, bool leaveOpen = false)
        {
            using var reader = new StreamReader(stream, leaveOpen: leaveOpen);
            return reader.ReadToEnd();
        }

        public static Stream AsStream(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value));
        }

        public static MemoryStream AsStream(this byte[] value, bool writable = true, bool exposed = true)
        {
            return new MemoryStream(value, 0, value.Length, writable: writable, publiclyVisible: exposed);
        }

        public static Memory<byte> GetMemory(this MemoryStream stream)
        {
            return stream.GetBuffer().AsMemory(0, (int)stream.Length);
        }

        public static Span<byte> GetSpan(this MemoryStream stream)
        {
            return stream.GetBuffer().AsSpan(0, (int)stream.Length);
        }

        public static Memory<byte> GetForwardMemory(this MemoryStream stream)
        {
            return stream.GetMemory().Slice((int)stream.Position);
        }

        public static string ReadAllText(this Stream stream, out SourceEncodingInfo encodingInfo, out int size)
        {
            using var bomCaptureStream = new BomDetectionStream(stream);
            using var reader = new StreamReader(bomCaptureStream, detectEncodingFromByteOrderMarks: true);
            var result = reader.ReadToEnd();

            size = bomCaptureStream.ReadBytes;

            encodingInfo = bomCaptureStream.GetEncodingInfo(reader.CurrentEncoding);
            return result;
        }

        public static void WriteAllText(this Stream stream, string text, bool leaveOpen = true)
        {
            using var writer = new StreamWriter(stream, leaveOpen: leaveOpen);
            writer.Write(text);
        }

        public static int WriteSpan<T>(this Stream stream, ReadOnlySpan<T> span)
            where T : unmanaged
        {
            var bytes = span.AsBytes();
            stream.Write(bytes);
            return bytes.Length;
        }

        public static T AssignDuplicate<T>(T value, ref T lastValue)
        {
            if (EqualityComparer<T>.Default.Equals(value, default(T)))
            {
                return lastValue;
            }
            else
            {
                lastValue = value;
                return value;
            }
        }

        public static T RemoveDuplicate<T>(T value, ref T lastValue)
        {
            if (EqualityComparer<T>.Default.Equals(value, lastValue))
            {
                return default(T);
            }
            else
            {
                lastValue = value;
                return value;
            }
        }

        public static T PopulateContentIdAndSize<T>(this T entity, bool force = false)
            where T : class, ISearchEntity
        {
            if (entity.EntityContentId == default || entity.EntityContentSize == 0 || force)
            {
                using (var lease = Pools.EncoderContextPool.Acquire())
                {
                    var encoderContext = lease.Instance;
                    entity.SerializeEntityTo(encoderContext.Stream, stage: ObjectStage.Hash);
                    entity.EntityContentId = encoderContext.ToHash(encoderContext.Stream);
                    entity.EntityContentSize = (int)encoderContext.Stream.Position;

                    if (entity.Uid == default || force)
                    {
                        entity.Uid = entity.EntityContentId;
                    }
                }
            }

            return entity;
        }

        public static MurmurHash ComputeFullHash(this string v)
        {
            return IndexingUtilities.ComputeFullHash(v);
        }

        public static string GetEntityContentId(this EntityBase entity, ObjectStage stage = ObjectStage.All)
        {
            using (var lease = Pools.EncoderContextPool.Acquire())
            {
                var encoderContext = lease.Instance;
                var stringValue = entity.SerializeEntity(ObjectStage.Index);

                entity.SerializeEntityTo(encoderContext.Stream, stage: ObjectStage.Index);
                return encoderContext.ToBase64HashString(encoderContext.Stream);
            }
        }
    }
}
