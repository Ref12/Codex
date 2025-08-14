using Codex.Lucene.Framework;
using Codex.Sdk.Utilities;
using Codex.Utilities;
using CommunityToolkit.HighPerformance;
using Lucene.Net.Search;
using Lucene.Net.Support;
using Lucene.Net.Util;
using Lucene.Net.Util.Fst;
using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Codex.Lucene.Formats
{
    /// <summary>
    /// {@link DocIdSet} implementation inspired from http://roaringbitmap.org/
    /// <p>
    /// The space is divided into blocks of 2^16 bits and each block is encoded
    /// independently. In each block, if less than 2^12 bits are set, then
    /// documents are simply stored in a short[]. If more than 2^16-2^12 bits are
    /// set, then the inverse of the set is encoded in a simple short[]. Otherwise
    /// a {@link FixedBitSet} is used.
    /// 
    /// Ported from RoaringDocIdSet in Lucene.
    /// </summary>
    public partial class RoaringDocIdSet : DocIdSet, IBitSet
    {
        // Number of documents in a block
        public const int BLOCK_SIZE = 1 << 16;
        // The maximum length for an array, beyond that point we switch to a bitset
        private const int MAX_ARRAY_LENGTH = 1 << 12;
        private const int MAX_RANGE_ARRAY_LENGTH = 1 << 11;
        private static readonly long BASE_RAM_BYTES_USED = RamUsageEstimator.ShallowSizeOfInstance(typeof(RoaringDocIdSet));

        public static readonly RoaringDocIdSet Empty = new Builder().Build();

        private enum DocIdSetType : byte
        {
            NONE,
            SHORT_ARRAY,
            BIT
        }

        private enum PersistedDocIdSetType : byte
        {
            NONE,
            SHORT_ARRAY,
            BIT,
            BIT_WITH_SUMMARY,
            RANGE_ARRAY,

        }

        private readonly DocIdSet[] docIdSets;
        private readonly IBits[] bitSets;
        public readonly int Count;
        public readonly int MaxDoc;

        private RoaringDocIdSet(DocIdSet[] docIdSets, int cardinality, int maxDoc)
        {
            this.docIdSets = docIdSets;
            this.bitSets = docIdSets.SelectArray(set => (IBits)set);
            this.Count = cardinality;
            this.MaxDoc = maxDoc;
        }

        public DocIdSet GetSubSet(int setIndex)
        {
            return docIdSets[setIndex];
        }

        public RangeDocIdSet AsRangeDocIdSet(int setIndex)
        {
            var ranges = new List<DocIdRange>();

            DocIdRange range = default;
            DocIdRange createRange(int start)
            {
                var range = new DocIdRange((ushort)start);
                //ranges.Add(range);
                return range;
            }

            DocIdSet docIdSet = docIdSets[setIndex];
            int count = 0;
            foreach (int docId in docIdSet.Enumerate())
            {
                if (count > 0 && range.EndInclusive + 1 == docId)
                {
                    range.EndInclusive = (ushort)docId;
                }
                else
                {
                    if (count > 0)
                    {
                        ranges.Add(range);
                    }

                    range = createRange(docId);
                }

                count++;
            }

            if (count > 0)
            {
                ranges.Add(range);
            }

            return new RangeDocIdSet(ranges.ToArray());
        }

        public bool Contains(int id)
        {
            int targetBlockIndex = id >> 16;
            if (targetBlockIndex >= docIdSets.Length || id > MaxDoc)
            {
                return false;
            }

            var targetBlock = bitSets[targetBlockIndex];
            if (targetBlock == null)
            {
                return false;
            }

            ushort subIndex = (ushort)(id & 0xFFFF);

            if (subIndex >= targetBlock.Length)
            {
                return false;
            }

            return targetBlock.Get(subIndex);
        }

        public static RoaringDocIdSet From(IEnumerable<int> orderedIds)
        {
            var builder = new Builder();
            foreach (var id in orderedIds)
            {
                builder.Add(id);
            }

            return builder.Build();
        }

        private ref struct SetHeader
        {
            public const int Length = 3;

            public Span<byte> Bytes;

            public ref PersistedDocIdSetType Type;
            public ref short ShortSetCardinality;
            public ref ushort FixedSetCardinality;
        }

        private static SetHeader GetHeader(Span<byte> headerBytes)
        {
            var header = new SetHeader();
            header.Bytes = headerBytes;
            header.Type = ref MemoryMarshal.AsRef<PersistedDocIdSetType>(headerBytes.Slice(2));
            header.ShortSetCardinality = ref MemoryMarshal.AsRef<short>(headerBytes.Slice(0, 2));
            header.FixedSetCardinality = ref MemoryMarshal.AsRef<ushort>(headerBytes.Slice(0, 2));
            return header;
        }

        public static RoaringDocIdSet FromPersisted(PersistedIdSet persisted)
        {
            if (persisted.Cardinality == 0 || persisted.Segments.Count == 0)
            {
                return new RoaringDocIdSet(Array.Empty<DocIdSet>(), 0, -1);
            }

            var maxSegmentIndex = persisted.Segments.Max(k => k.Key);
            DocIdSet[] docIdSets = new DocIdSet[maxSegmentIndex + 1];
            
            foreach ((var index, var bytes) in persisted.Segments)
            {
                var header = GetHeader(bytes.Slice(0, 3).Span);
                var dataBytes = bytes.Slice(3);
                if (header.Type == PersistedDocIdSetType.SHORT_ARRAY)
                {
                    var docIds = dataBytes.Cast<byte, ushort>();
                    bool invert = header.ShortSetCardinality <= 0;
                    var set = new ShortArrayDocIdSet(docIds, invert);
                    docIdSets[index] = set;
                }
                else if (header.Type == PersistedDocIdSetType.RANGE_ARRAY)
                {
                    var docIds = dataBytes.Cast<byte, DocIdRange>();
                    var set = new RangeDocIdSet(docIds);
                    docIdSets[index] = set;
                }
                else
                {
                    var bits = dataBytes.Cast<byte, long>();
                    var set = StructArray.Create(bits).AsFixedBitSet(header.FixedSetCardinality);
                    docIdSets[index] = set;
                }
            }

            return new RoaringDocIdSet(docIdSets, persisted.Cardinality, persisted.MaxDoc);
        }

        public PersistedIdSet ToPersisted()
        {
            var persistedSet = new PersistedIdSet()
            {
                Cardinality = Count,
                MaxDoc = MaxDoc
            };

            var header = GetHeader(stackalloc byte[SetHeader.Length]);

            foreach (var entry in docIdSets.WithIndices())
            {
                ReadOnlySpan<byte> dataBytes;
                if (entry.Item == null)
                {
                    continue;
                }
                else if (entry.Item is ShortArrayDocIdSet shortSet)
                {
                    header.Type = PersistedDocIdSetType.SHORT_ARRAY;
                    var cardinality = shortSet.DocIDs.Length;
                    header.ShortSetCardinality = (short)(shortSet.Invert ? -cardinality : cardinality);
                    dataBytes = MemoryMarshal.Cast<ushort, byte>(shortSet.DocIDs.Span);
                }
                else if (entry.Item is RangeDocIdSet rangeSet)
                {
                    header.Type = PersistedDocIdSetType.RANGE_ARRAY;
                    var cardinality = rangeSet.DocIDs.Length;
                    header.ShortSetCardinality = (short)cardinality;
                    dataBytes = MemoryMarshal.AsBytes(rangeSet.DocIDs.Span);
                }
                else
                {
                    var fixedBits = (IFixedBitSet)entry.Item;
                    header.Type = PersistedDocIdSetType.BIT;
                    var cardinality = fixedBits.Cardinality;
                    header.FixedSetCardinality = (ushort)cardinality;
                    var longs = fixedBits.GetBits();
                    dataBytes = MemoryMarshal.Cast<long, byte>(longs.Span.TrimEnd(0));
                }

                var bytes = new byte[header.Bytes.Length + dataBytes.Length];
                header.Bytes.CopyTo(bytes.AsSpan(0, 3));
                dataBytes.CopyTo(bytes.AsSpan(3));

                persistedSet.Segments[entry.Index] = bytes;
            }

            return persistedSet;
        }

        public override DocIdSetIterator GetIterator()
        {
            if (Count == 0)
            {
                return DocIdSetIterator.GetEmpty();
            }

            return new Iterator(this);
        }

        public IEnumerable<int> AsEnumerable => Enumerate();

        int IBits.Length => MaxDoc + 1;

        public IEnumerable<int> Enumerate()
        {
            var iterator = GetIterator();
            while (true)
            {
                var doc = iterator.NextDoc();
                if (doc == DocIdSetIterator.NO_MORE_DOCS)
                {
                    yield break;
                }

                yield return doc;
            }
        }

        private class Iterator : DocIdSetIterator
        {
            RoaringDocIdSet rdis;
            int block;
            DocIdSetIterator sub = null;
            int doc;

            public Iterator(RoaringDocIdSet rdis)
            {
                doc = -1;
                block = -1;
                this.rdis = rdis;
                sub = DocIdSetIterator.GetEmpty();
            }

            public override int DocID => doc;

            public override int NextDoc()
            {
                int subNext = sub.NextDoc();
                if (subNext == NO_MORE_DOCS)
                {
                    return firstDocFromNextBlock();
                }
                return doc = (block << 16) | subNext;
            }


            public override int Advance(int target)
            {
                int targetBlock = target >> 16;
                if (targetBlock != block)
                {
                    block = targetBlock;
                    if (block >= rdis.docIdSets.Length)
                    {
                        sub = null;
                        return doc = NO_MORE_DOCS;
                    }
                    if (rdis.docIdSets[block] == null)
                    {
                        return firstDocFromNextBlock();
                    }
                    sub = rdis.docIdSets[block].GetIterator();
                }

                int subNext = sub.Advance(target & 0xFFFF);
                if (subNext == NO_MORE_DOCS)
                {
                    return firstDocFromNextBlock();
                }
                return doc = (block << 16) | subNext;
            }

            private int firstDocFromNextBlock()
            {
                while (true)
                {
                    block += 1;
                    if (block >= rdis.docIdSets.Length)
                    {
                        sub = null;
                        return doc = NO_MORE_DOCS;
                    }
                    else if (rdis.docIdSets[block] != null)
                    {
                        sub = rdis.docIdSets[block].GetIterator();
                        int subNext = sub.NextDoc();
                        Contract.Assert(subNext != NO_MORE_DOCS);
                        return doc = (block << 16) | subNext;
                    }
                }
            }


            public override long GetCost()
            {
                return rdis.Count;
            }
        }

        /**
         * Return the exact number of documents that are contained in this set.
         */
        public int Cardinality()
        {
            return Count;
        }

        public override String ToString()
        {
            return "RoaringDocIdSet(cardinality=" + Count + ")";
        }

        bool IBits.Get(int index)
        {
            return Contains(index);
        }

        public void SetBits(FixedBitSet targetSet)
        {
            for (int i = 0; i < this.docIdSets.Length; i++)
            {
                var set = this.docIdSets[i];
                var bitOffset = i << 16;
                var wordOffset = bitOffset >> 6;
                if (set is FixedBitSet fixedBitSet)
                {
                    targetSet.Or(fixedBitSet, wordOffset);
                }
                else if (set is RangeDocIdSet rangeSet)
                {
                    rangeSet.SetBits(targetSet, bitOffset);
                }
                else if (set is ShortArrayDocIdSet shortSet)
                {
                    shortSet.SetBits(targetSet, bitOffset);
                }
                else if (set != null)
                {
                    targetSet.Or(set.GetIterator(), wordOffset);
                }
            }
        }

        /**
         * A builder of {@link RoaringDocIdSet}s.
         */
        public class Builder
        {
            private readonly List<DocIdSet> sets = new List<DocIdSet>();

            private int cardinality;
            private int lastDocId;
            private int currentBlock;
            private int currentBlockCardinality;

            // We start by filling the buffer and when it's full we copy the content of
            // the buffer to the FixedBitSet and put further documents in that bitset
            private readonly ushort[] buffer;
            private IFixedBitSet denseBuffer;
            private ArrayBuilder<DocIdRange> rangeBuffer;

            /**
             * Sole constructor.
             */
            public Builder()
            {
                lastDocId = -1;
                currentBlock = -1;
                buffer = new ushort[MAX_ARRAY_LENGTH];
                rangeBuffer = new ArrayBuilder<DocIdRange>(MAX_RANGE_ARRAY_LENGTH);
            }

            private void EnsureCapacity()
            {
                for (int i = sets.Count; i <= currentBlock; i++)
                {
                    sets.Add(null);
                }
            }

            private void SetCurrentBlock(DocIdSet docIdSet)
            {
                if (Features.EnableRangeDocIdSets && rangeBuffer.Length < MAX_RANGE_ARRAY_LENGTH)
                {
                    // Its possible to create a range doc ids set. Compare to see
                    // which takes less space.
                    if (docIdSet is ShortArrayDocIdSet shortSet)
                    {
                        // Range entries take up twice the space so we need to multiply
                        // by two when comparing memory usage
                        if (rangeBuffer.Length * 2 < shortSet.DocIDs.Length)
                        {
                            docIdSet = new RangeDocIdSet(rangeBuffer.ToArray());
                        }
                    }
                    else if (docIdSet is FixedBitSet)
                    {
                        // Range doc id set is always smaller than FixedBitSet when length is less than
                        // the max
                        docIdSet = new RangeDocIdSet(rangeBuffer.ToArray());
                    }
                }

                sets[currentBlock] = docIdSet;
            }

            private void Flush()
            {
                Contract.Assert(currentBlockCardinality <= BLOCK_SIZE);

                EnsureCapacity();
                if (currentBlockCardinality <= MAX_ARRAY_LENGTH)
                {
                    // Use sparse encoding
                    Contract.Assert(denseBuffer == null);
                    if (currentBlockCardinality > 0)
                    {
                        var blockBuffer = new ushort[currentBlockCardinality];
                        Array.Copy(buffer, blockBuffer, currentBlockCardinality);
                        SetCurrentBlock(new ShortArrayDocIdSet(blockBuffer, false));
                    }
                }
                else
                {
                    Contract.Assert(denseBuffer != null);
                    Contract.Assert(denseBuffer.Cardinality == currentBlockCardinality);
                    if (denseBuffer.Length == BLOCK_SIZE && BLOCK_SIZE - currentBlockCardinality < MAX_ARRAY_LENGTH)
                    {
                        // Doc ids are very dense, inverse the encoding
                        ushort[] excludedDocs = new ushort[BLOCK_SIZE - currentBlockCardinality];
                        denseBuffer.Flip(0, denseBuffer.Length);
                        int excludedDoc = -1;
                        for (int i = 0; i < excludedDocs.Length; ++i)
                        {
                            excludedDoc = denseBuffer.NextSetBit(excludedDoc + 1);
                            Contract.Assert(excludedDoc != DocIdSetIterator.NO_MORE_DOCS);
                            excludedDocs[i] = (ushort)excludedDoc;
                        }
                        Contract.Assert(excludedDoc + 1 == denseBuffer.Length || denseBuffer.NextSetBit(excludedDoc + 1) == -1);
                        SetCurrentBlock(new ShortArrayDocIdSet(excludedDocs, true));
                    }
                    else
                    {
                        // Neither sparse nor super dense, use a fixed bit set
                        SetCurrentBlock(denseBuffer.DocIdSet);
                        denseBuffer.Count = currentBlockCardinality;
                    }
                    denseBuffer = null;
                }

                cardinality += currentBlockCardinality;
                denseBuffer = null;
                currentBlockCardinality = 0;
                rangeBuffer.Clear();
            }

            /**
             * Add a new doc-id to this builder.
             * NOTE: doc ids must be added in order.
             */
            public Builder Add(int docId)
            {
                if (docId <= lastDocId)
                {
                    throw new ArgumentOutOfRangeException("Doc ids must be added in-order, got " + docId + " which is <= lastDocID=" + lastDocId);
                }

                int block = docId >> 16;
                if (block != currentBlock)
                {
                    // we went to a different block, let's flush what we buffered and start from fresh
                    Flush();
                    currentBlock = block;
                }

                if (rangeBuffer.Length > 0 && (docId - lastDocId == 1))
                {
                    // Continuing current run.
                    rangeBuffer.Last.EndInclusive = (ushort)docId;
                }
                else
                {
                    rangeBuffer.TryAdd(new DocIdRange(Start: (ushort)docId));
                }

                if (currentBlockCardinality < MAX_ARRAY_LENGTH)
                {
                    buffer[currentBlockCardinality] = (ushort)docId;
                }
                else
                {
                    if (denseBuffer == null)
                    {
                        // the buffer is full, let's move to a fixed bit set
                        denseBuffer = Helpers.CreateFixedBitSet(1 << 16);
                        foreach (short doc in buffer)
                        {
                            denseBuffer.Set(doc & 0xFFFF);
                        }
                    }
                    denseBuffer.Set(docId & 0xFFFF);
                }

                lastDocId = docId;
                currentBlockCardinality += 1;
                return this;
            }

            /**
             * Add the content of the provided {@link DocIdSetIterator}.
             */
            public Builder Add(DocIdSetIterator disi, int offset = 0)
            {
                for (int doc = disi.NextDoc(); doc != DocIdSetIterator.NO_MORE_DOCS; doc = disi.NextDoc())
                {
                    Add(doc + offset);
                }
                return this;
            }

            /**
             * Build an instance.
             */
            public RoaringDocIdSet Build()
            {
                Flush();
                return new RoaringDocIdSet(sets.ToArray(), cardinality, lastDocId);
            }
        }
    }
}
