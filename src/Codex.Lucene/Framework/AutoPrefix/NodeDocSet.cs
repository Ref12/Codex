using Lucene.Net.Codecs;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace Codex.Lucene.Framework.AutoPrefix
{
    public class NodeDocSet(int docCount, bool validating) : PostingsConsumer, INodeValue<NodeDocSet>
    {
        private OpenBitSet builder = new OpenBitSet(docCount);
        public DocIdSet Docs => builder;

        public NodeDocSet CreateNew()
        {
            return new NodeDocSet(docCount, validating);
        }

        public void Add(NodeDocSet other)
        {
            builder.Union(other.builder);
        }

        public void Clear()
        {
            builder.GetBits().AsSpan().Clear();
        }

        public override void StartDoc(int docId, int freq)
        {
            builder.FastSet(docId);
        }

        public override void AddPosition(int position, BytesRef payload, int startOffset, int endOffset)
        {
        }

        public override void FinishDoc()
        {
        }
    }
}
