using System.Runtime.InteropServices;
using Codex.Utilities.Serialization;

namespace Codex.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Murmur3
    {
        // 128 bit output, 64 bit platform version

        private static readonly Murmur3 _instance = new Murmur3();

        public static Murmur3 Shared => _instance;

        public const int READ_SIZE = 16;
        private static ulong C1 = 0x87c37b91114253d5L;
        private static ulong C2 = 0x4cf5ad432745937fL;

        private ulong processedCount;
        private uint seed; // if want to start with a seed, create a constructor
        ulong high;
        ulong low;

        private int stateOffset;
        private State state;

        public MurmurHash HashBytes<T>(T bytes)
            where T : unmanaged
        {
            return ComputeHash(MemoryMarshal.AsBytes(stackalloc[] { bytes }));
        }

        public MurmurHash ComputeHash(byte[] bb, int start = 0, int length = -1)
        {
            return ComputeHash(bb.AsSpan(start, length < 0 ? bb.Length : length));
        }

        public static MurmurHash ComputeBytesHash<T>(ReadOnlySpan<T> span)
            where T : unmanaged
        {
            var murmur3 = new Murmur3();
            return murmur3.ComputeHash(MemoryMarshal.AsBytes(span));
        }

        public MurmurHash ComputeHash(ReadOnlySpan<byte> bb)
        {
            Reset();
            ProcessBytes(bb);
            ProcessFinal();
            return Hash;
        }

        public MurmurHash ComputeHash(IEnumerable<ReadOnlyMemory<byte>> byteSegments)
        {
            return ComputeSequenceHash(byteSegments, static s => s.Value.Span);
        }

        public MurmurHash ComputeSequenceHash<T>(IEnumerable<T> byteSegments, ReadOnlySpanResultInFunc<T, byte> getBytes)
        {
            Reset();
            foreach (var byteSegment in byteSegments)
            {
                ProcessBytes(getBytes(new(byteSegment)));
            }

            ProcessFinal();
            return Hash;
        }

        public MurmurHash ComputeSequenceHash<T>(ReadOnlySpan<T> byteSegments, ReadOnlySpanResultInFunc<T, byte> getBytes)
        {
            Reset();
            foreach (var byteSegment in byteSegments)
            {
                ProcessBytes(getBytes(new(byteSegment)));
            }

            ProcessFinal();
            return Hash;
        }

        private void ProcessBytes(ReadOnlySpan<byte> bb)
        {
            int pos = 0;
            int remaining = bb.Length;

            int read = state.ConsumeInitial(ref this, bb);
            pos += read;
            remaining -= read;

            // read 128 bits, 16 bytes, 2 longs in each cycle
            while (remaining >= READ_SIZE)
            {
                ulong k1 = bb.GetUInt64(pos);
                pos += 8;

                ulong k2 = bb.GetUInt64(pos);
                pos += 8;

                remaining -= READ_SIZE;

                MixBody(k1, k2);
            }

            if (remaining >= 0)
            {
                read = state.ConsumeRemaining(ref this, bb.Slice(pos, remaining));
            }

            processedCount += (ulong)bb.Length;
        }

        private void Reset()
        {
            high = seed;
            low = 0;
            this.processedCount = 0L;
        }

        private void ProcessFinal()
        {
            if (stateOffset > 0)
            {
                MixRemaining(state.K1, state.K2);
            }

            high ^= processedCount;
            low ^= processedCount;

            high += low;
            low += high;

            high = MixFinal(high);
            low = MixFinal(low);

            high += low;
            low += high;
        }

        private void MixRemaining(ulong k1, ulong k2)
        {
            high ^= MixKey1(k1);
            low ^= MixKey2(k2);
        }

        #region Mix Methods

        private void MixBody(ulong k1, ulong k2)
        {
            high ^= MixKey1(k1);

            high = high.RotateLeft(27);
            high += low;
            high = high * 5 + 0x52dce729;

            low ^= MixKey2(k2);

            low = low.RotateLeft(31);
            low += high;
            low = low * 5 + 0x38495ab5;
        }

        private static ulong MixKey1(ulong k1)
        {
            k1 *= C1;
            k1 = k1.RotateLeft(31);
            k1 *= C2;
            return k1;
        }

        private static ulong MixKey2(ulong k2)
        {
            k2 *= C2;
            k2 = k2.RotateLeft(33);
            k2 *= C1;
            return k2;
        }

        private static ulong MixFinal(ulong k)
        {
            // avalanche bits

            k ^= k >> 33;
            k *= 0xff51afd7ed558ccdL;
            k ^= k >> 33;
            k *= 0xc4ceb9fe1a85ec53L;
            k ^= k >> 33;
            return k;
        }

        #endregion

        public MurmurHash Hash
        {
            get
            {
                return new MurmurHash() { High = high, Low = low };
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private unsafe struct State
        {
            public const int BYTE_LENGTH = 16;

            [FieldOffset(0)]
            public ulong K1;

            [FieldOffset(8)]
            public ulong K2;

            [FieldOffset(0)]
            private fixed byte bytes[BYTE_LENGTH];

            public byte this[int i]
            {
                get
                {
                    fixed (byte* bytes = this.bytes)
                    {
                        return bytes[i];
                    }
                }

                set
                {
                    fixed (byte* bytes = this.bytes)
                    {
                        bytes[i] = value;
                    }
                }
            }

            public int ConsumeInitial(ref Murmur3 murmur, ReadOnlySpan<byte> bb)
            {
                if (murmur.stateOffset != 0)
                {
                    return ConsumeRemaining(ref murmur, bb);
                }

                return 0;
            }

            public int ConsumeRemaining(ref Murmur3 murmur, ReadOnlySpan<byte> bb)
            {
                fixed (byte* bytes = this.bytes)
                {
                    int i = 0;
                    for (i = 0; i < bb.Length && murmur.stateOffset < BYTE_LENGTH; i++, murmur.stateOffset++)
                    {
                        bytes[murmur.stateOffset] = bb[i];
                    }

                    if (murmur.stateOffset == READ_SIZE)
                    {
                        murmur.MixBody(K1, K2);
                        murmur.stateOffset = 0;
                        K1 = 0;
                        K2 = 0;
                    }

                    return i;
                }
            }
        }
    }
}
