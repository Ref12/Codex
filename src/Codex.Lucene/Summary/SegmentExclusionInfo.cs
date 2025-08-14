using System.Collections;
using System.Collections.Specialized;
using System.Numerics;
using System.Runtime.CompilerServices;
using Codex.Utilities;
using Lucene.Net.Index;

namespace Codex.Lucene.Search;

public record struct BitSetOverview(int SetBitCount, int TotalCount)
{
    public bool IsNone => SetBitCount == 0;
    public bool IsAll => SetBitCount == TotalCount;
    public bool IsSomeNotAll => !IsNone && !IsAll;
}

public record struct SegmentBitMap(int SegmentCount)
{
    public BitVector32 Bits;
    public BitArray BitsExtended;

    public BitSetOverview Overview => new BitSetOverview(GetSetBitCount(), SegmentCount);

    public bool Get(int index)
    {
        return BitsExtended?.Get(index) ?? Bits.Get(index);
    }

    private int GetSetBitCount()
    {
        return BitsExtended?.Count 
            ?? BitOperations.PopCount(unchecked((uint)Bits.Data));
    }

    public SegmentBitMap Set(int index, bool value = true)
    {
        if (BitsExtended != null)
        {
            BitsExtended.Set(index, value);
        }
        else
        {
            Bits.Set(index, value);
        }

        return this;
    }

    public SegmentBitMap Or(SegmentBitMap other)
    {
        Bits = new BitVector32(Bits.Data | other.Bits.Data);
        BitsExtended?.Or(other.BitsExtended);
        return this;
    }

    public SegmentBitMap And(SegmentBitMap other)
    {
        Bits = new BitVector32(Bits.Data & other.Bits.Data);
        BitsExtended?.And(other.BitsExtended);
        return this;
    }

    public static SegmentBitMap Create(int segmentCount)
    {
        if (segmentCount <= 32)
        {
            return new SegmentBitMap(segmentCount);
        }
        else
        {
            return new SegmentBitMap(segmentCount)
            {
                BitsExtended = new BitArray(segmentCount)
            };
        }
    }

    public static SegmentBitMap operator &(SegmentBitMap? left, SegmentBitMap right)
    {
        return left?.And(right) ?? right;
    }

    public static SegmentBitMap operator |(SegmentBitMap? left, SegmentBitMap right)
    {
        return left?.Or(right) ?? right;
    }
}
