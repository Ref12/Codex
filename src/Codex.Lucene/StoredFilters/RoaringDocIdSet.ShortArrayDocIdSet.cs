using Codex.Utilities;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System.Diagnostics.ContractsLight;
using static Codex.Lucene.Formats.RoaringDocIdSet;
using static Codex.Lucene.Formats.RoaringDocIdSet.ShortArrayDocIdSet;

namespace Codex.Lucene.Formats
{
    public partial class RoaringDocIdSet
    {
        /**
         * {@link DocIdSet} implementation that can store documents up to 2^16-1 in a short[].
         */
        public class ShortArrayDocIdSet : ShortArrayDocIdSetBase<ushort, ushort>
        {
            public ShortArrayDocIdSet(ReadOnlyMemory<ushort> docIDs, bool invert) 
                : base(docIDs, invert)
            {
            }

            protected override ushort GetSearchItem(int docId)
            {
                return (ushort)docId;
            }

            protected override bool NextDoc(ushort currentItem, int currentDocId, out int nextDoc)
            {
                nextDoc = default;
                return false;
            }

            protected override ushort StartDoc(ushort item)
            {
                return item;
            }

            internal void SetBits(FixedBitSet targetSet, int bitOffset)
            {
                int lastDocId = -1;

                var maxDocExclusive = Math.Min(ushort.MaxValue + 1, targetSet.Length - bitOffset);
                if (Invert)
                {
                    void set(int start, int endExclusive)
                    {
                        targetSet.Set(start + bitOffset, endExclusive + bitOffset);
                    }

                    foreach (var docId in DocIDs.Span)
                    {
                        if (docId <= maxDocExclusive)
                        {
                            var start = lastDocId + 1;
                            var endExclusive = docId;
                            set(start, endExclusive);

                            lastDocId = docId;
                        }
                    }

                    if (lastDocId < maxDocExclusive)
                    {
                        set(lastDocId + 1, maxDocExclusive);
                    }
                }
                else
                {
                    foreach (var docId in DocIDs.Span)
                    {
                        targetSet.Set(docId + bitOffset);
                    }
                }
            }
        }

        public record struct DocIdRange(ushort Start)
        {
            public ushort EndInclusive { get; set; } = Start;

            public int Length => (EndInclusive + 1) - Start;
        }

        public record struct SearchDocId(int DocId) : IComparable<DocIdRange>
        {
            public int CompareTo(DocIdRange other)
            {
                if (DocId < other.Start)
                {
                    return -1;
                }
                else if (DocId > other.EndInclusive)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        public class RangeDocIdSet : ShortArrayDocIdSetBase<DocIdRange, SearchDocId>
        {
            public RangeDocIdSet(ReadOnlyMemory<DocIdRange> docIDs)
                : base(docIDs, invert: false)
            {
            }

            protected override SearchDocId GetSearchItem(int docId)
            {
                return new SearchDocId(docId);
            }

            protected override bool NextDoc(DocIdRange currentItem, int currentDocId, out int nextDoc)
            {
                if (currentDocId < currentItem.EndInclusive)
                {
                    nextDoc = ++currentDocId;
                    return true;
                }
                else
                {
                    nextDoc = default;
                    return false;
                }
            }

            protected override ushort StartDoc(DocIdRange item)
            {
                return item.Start;
            }

            internal void SetBits(FixedBitSet targetSet, int bitOffset)
            {
                foreach (var range in DocIDs.Span)
                {
                    targetSet.Set(range.Start + bitOffset, range.EndInclusive + bitOffset + 1);
                }
            }
        }

        /**
         * {@link DocIdSet} implementation that can store documents up to 2^16-1 in a short[].
         */
        public abstract class ShortArrayDocIdSetBase<TItem, TSearch> : DocIdSet, IBits
            where TSearch : struct, IComparable<TItem>
            where TItem : unmanaged
        {
            private static readonly long BASE_RAM_BYTES_USED = RamUsageEstimator.ShallowSizeOfInstance(typeof(ShortArrayDocIdSet));

            public readonly ReadOnlyMemory<TItem> DocIDs;
            public readonly bool Invert;

            int IBits.Length => 1 << 16;

            public ShortArrayDocIdSetBase(ReadOnlyMemory<TItem> docIDs, bool invert)
            {
                this.DocIDs = docIDs;
                this.Invert = invert;
            }

            protected abstract TSearch GetSearchItem(int docId);

            protected abstract bool NextDoc(TItem currentItem, int currentDocId, out int nextDoc);

            protected abstract ushort StartDoc(TItem item);

            public bool Get(int docId)
            {
                bool found = DocIDs.Span.BinarySearch(GetSearchItem(docId)) >= 0;
                return Invert ? !found : found;
            }

            public override DocIdSetIterator GetIterator()
            {
                DocIdSetIterator iterator = new CoreIterator(this);
                if (!Invert)
                {
                    return iterator;
                }
                else
                {
                    return new InverseIterator(this, iterator);
                }
            }

            // Copied from NotDocIdSet
            private class InverseIterator : DocIdSetIterator
            {
                int doc = -1;
                int nextSkippedDoc = -1;
                const int MAX_DOC = BLOCK_SIZE;
                private readonly ShortArrayDocIdSetBase<TItem, TSearch> set;
                private readonly DocIdSetIterator iterator;

                public InverseIterator(ShortArrayDocIdSetBase<TItem, TSearch> set, DocIdSetIterator iterator)
                {
                    this.set = set;
                    this.iterator = iterator;
                }

                public override int NextDoc()
                {
                    return Advance(doc + 1);
                }

                public override int DocID => doc;

                public override long GetCost()
                {
                    return MAX_DOC - set.DocIDs.Length;
                }

                public override int Advance(int target)
                {
                    doc = target;
                    if (doc > nextSkippedDoc)
                    {
                        nextSkippedDoc = iterator.Advance(doc);
                    }
                    while (true)
                    {
                        if (doc >= MAX_DOC)
                        {
                            return doc = NO_MORE_DOCS;
                        }
                        Contract.Assert(doc <= nextSkippedDoc);
                        if (doc != nextSkippedDoc)
                        {
                            return doc;
                        }
                        doc += 1;
                        nextSkippedDoc = iterator.NextDoc();
                    }
                }
            }

            private class CoreIterator : DocIdSetIterator
            {
                int i = -1; // this is the index of the current document in the array
                private int docInner = -1;
                int doc
                {
                    get => docInner;
                    set
                    {
                        if (value == 0 && EqualityComparer<TItem>.Default.Equals(default, item))
                        {

                        }

                        docInner = value;
                    }
                }
                TItem item = default;

                private readonly ShortArrayDocIdSetBase<TItem, TSearch> set;

                public CoreIterator(ShortArrayDocIdSetBase<TItem, TSearch> set)
                {
                    this.set = set;
                }

                private int docId(int i)
                {
                    item = set.DocIDs.Span[i];
                    return set.StartDoc(item);
                }

                public override int NextDoc()
                {
                    if (i >= 0 && set.NextDoc(item, doc, out var nextDoc))
                    {
                        return doc = nextDoc;
                    }

                    if (++i >= set.DocIDs.Length)
                    {
                        return doc = NO_MORE_DOCS;
                    }

                    return doc = docId(i);
                }

                public override int DocID => doc;

                public override long GetCost()
                {
                    return set.DocIDs.Length;
                }

                public override int Advance(int target)
                {
                    // binary search
                    var lo = set.DocIDs.Span.BinarySearch(set.GetSearchItem(target));
                    if (lo < 0)
                    {
                        lo = ~lo;
                    }

                    if (lo == set.DocIDs.Length)
                    {
                        i = set.DocIDs.Length;
                        return doc = NO_MORE_DOCS;
                    }
                    else
                    {
                        i = lo;
                        return doc = docId(i);
                    }
                }
            }
        }
    }
}
