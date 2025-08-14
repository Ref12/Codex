using Lucene.Net.Search;
using Lucene.Net.Util;

namespace Codex.Lucene.Framework;

public interface IFixedBitSet : IBits
{
    int Cardinality { get; }

    void Flip(int startIndex, int endIndex);

    int NextSetBit(int index);

    int? Count { get; set; }

    DocIdSet DocIdSet { get; }

    void Set(int index);

    Memory<long> GetBits();
}
