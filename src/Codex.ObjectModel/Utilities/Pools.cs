using Codex.Utilities;
using Codex.Utilities.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Utilities
{
    public static class Pools
    {
        public static readonly ObjectPool<byte[]> ByteArrayPool = new ObjectPool<byte[]>(
            () => new byte[1024],
            array => Array.Clear(array, 0, array.Length));

        public static readonly ObjectPool<StringBuilder> StringBuilderPool = new ObjectPool<StringBuilder>(
            () => new StringBuilder(),
            sb => sb.Clear());

        public static readonly ObjectPool<StringWriter> StringWriterPool = new ObjectPool<StringWriter>(
            () => new StringWriter(),
            sw => sw.GetStringBuilder().Clear());

        public static readonly ObjectPool<EncoderContext> EncoderContextPool = new ObjectPool<EncoderContext>(
            () => new EncoderContext(),
            sw => sw.Reset());

        public static readonly ObjectPool<CodexArrayBufferWriter<byte>> ByteBufferPool = new (
            () => new(2048),
            sw => sw.SetPosition(0));
    }
}
