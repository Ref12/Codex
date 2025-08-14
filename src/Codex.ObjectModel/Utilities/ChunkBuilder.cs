namespace Codex.Utilities
{
    public record ChunkBuilder(uint DesiredChunkSize, bool AutoNext = true, int ChunkWindowSize = 13)
    {
        public long[] ChunkWindow = new long[ChunkWindowSize];

        public uint MinChunkSize { get; init; } = Math.Max(1, DesiredChunkSize / 2);

        public uint OversizeChunkSize { get; init; } = (uint)Math.Max(2, DesiredChunkSize * 1.5);

        public uint Threshold = uint.MaxValue / DesiredChunkSize;
        public uint EffectiveThreshold = uint.MaxValue / DesiredChunkSize;

        public long _sum = 0;

        public uint CompareValue { get; private set; }

        public int ChunkSize { get; private set; }

        public int ChunkIndex = 0;

        private int _itemIndex = 0;
        private bool _isFull = false;

        private const uint EMPTY_HASH = 0x49D03A63;

        public bool Add(MurmurHash itemHash) => Add(itemHash.Low);

        public bool Add(ShortHash itemHash) => Add(itemHash.Low);

        public bool Add(ulong itemHash) => Add(unchecked((uint)itemHash));

        public bool Add(uint itemHash)
        {
            if (itemHash == 0) itemHash = EMPTY_HASH;

            if (_isFull)
            {
                _sum -= ChunkWindow[_itemIndex];
            }

            _sum += itemHash;
            ChunkWindow[_itemIndex] = itemHash;

            _itemIndex++;
            if (_itemIndex == ChunkWindow.Length)
            {
                _isFull = true;
                _itemIndex = 0;
            }

            var compareValue = unchecked((uint)_sum);
            return AddCompareValue(compareValue);
        }

        private bool AddCompareValue(uint compareValue)
        {
            ChunkSize++;
            CompareValue = compareValue;
            if (ChunkSize >= MinChunkSize)
            {
                if (CompareValue < EffectiveThreshold)
                {
                    if (AutoNext)
                    {
                        NextChunk();
                    }

                    return true;
                }

                if (ChunkSize == OversizeChunkSize)
                {
                    EffectiveThreshold = Threshold * 2;
                }
            }

            return false;
        }

        public void Reset()
        {
            NextChunk();
            ChunkIndex = 0;
        }

        public void NextChunk()
        {
            EffectiveThreshold = Threshold;
            ChunkIndex++;
            _isFull = false;
            _itemIndex = 0;
            _sum = 0;
            ChunkSize = 0;
        }
    }
}
