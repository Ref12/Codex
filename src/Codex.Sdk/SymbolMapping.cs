
using System.Buffers;
using Codex.ObjectModel;

namespace Codex.Storage.BlockLevel;

public static class SymbolMapping
{
    const int HASH_COUNT = 8;

    public static readonly FeatureSwitch<bool> PopulateSymbolMapDiffMode = true;
    public static readonly FeatureSwitch<int> MinSymbolMapMaxValue = 1 << 7;

    public record struct HashRingEntry(ulong Hash, int Index, bool IsSymbol) : IComparable<HashRingEntry>
    {
        public int CompareTo(HashRingEntry other)
        {
            return Hash.ChainCompareTo(other.Hash)
                ?? IsSymbol.ChainCompareTo(other.IsSymbol)
                ?? Index.CompareTo(other.Index);
        }
    }

    public record struct HashRingDiffEntry(ulong Diff, int Index, int SymbolIndex) : IComparable<HashRingDiffEntry>
    {
        public int CompareTo(HashRingDiffEntry other)
        {
            return
                default(int?)
                ?? Diff.ChainCompareTo(other.Diff)
                ?? Index.ChainCompareTo(other.Index)
                ?? SymbolIndex.ChainCompareTo(other.SymbolIndex)
                ?? 0;
        }
    }

    public static ulong[] HASH_SEEDS = Enumerable.Range(0, HASH_COUNT).Select(i => Murmur3.ComputeBytesHash<int>(stackalloc[] { i }).Low).ToArray();

    public static IImmutableDictionary<TSymbol, int> PopulateSymbolMap<TSymbol>(
        IEnumerable<TSymbol> symbols,
        IUnifiedComparer<TSymbol> comparer,
        Func<TSymbol, MurmurHash> getHash,
        AsyncOut<TSymbol[]> symbolIndexMap = null)
    {
        var symbolMap = ImmutableSortedDictionary.Create<TSymbol, int>(comparer)
            .SetItems(symbols.Select(s => KeyValuePair.Create(s, 0)))
            .ToBuilder();

        bool isDefault(TSymbol symbol)
        {
            return comparer.Equals(symbol, default);
        }

        var symbolsArray = symbolMap.Keys.ToArray();

        // Ensure mappedSymbols.Length is at least 127 i.e. size of 1-byte
        // 7-bit encoded integer since that's the minimum size used for encoding
        // the reference ids.
        var mappedSymbols = new TSymbol[Math.Max(MinSymbolMapMaxValue, symbolMap.Count * 4)];
        symbolIndexMap?.Set(mappedSymbols);

        Span<ulong> hashInputSpan = stackalloc ulong[3] { 0, 0, 0 };

        List<HashRingEntry> entries = new List<HashRingEntry>();

        for (int index = 1; index < mappedSymbols.Length; index++)
        {
            hashInputSpan[0] = (ulong)index;
            hashInputSpan[1] = 0;

            foreach (var seed in HASH_SEEDS)
            {
                hashInputSpan[2] = seed;
                var hash = Murmur3.ComputeBytesHash<ulong>(hashInputSpan);

                entries.Add(new(hash.Low, index, false));
            }
        }

        foreach ((var symbol, var index) in symbolsArray.WithIndices())
        {
            var symbolHash = getHash(symbol).GetParts();
            hashInputSpan[0] = symbolHash.High;
            hashInputSpan[1] = symbolHash.Low;

            foreach (var seed in HASH_SEEDS)
            {
                hashInputSpan[2] = seed;
                var hash = Murmur3.ComputeBytesHash<ulong>(hashInputSpan);

                entries.Add(new(hash.Low, index, true));
            }
        }

        entries.Sort();

        HashRingEntry? lastIndexEntry = null;
        if (PopulateSymbolMapDiffMode)
        {
            int assignmentCount = 0;
            var diffEntries = new List<HashRingDiffEntry>(entries.Count);
            int remainingIterations = 5;

            while (assignmentCount < symbolsArray.Length && remainingIterations-- > 0)
            {
                diffEntries.Clear();
                foreach (var entry in entries)
                {
                    if (!entry.IsSymbol)
                    {
                        if (isDefault(mappedSymbols[entry.Index]))
                        {
                            lastIndexEntry = entry;
                        }
                    }
                    else if (lastIndexEntry is { } indexEntry)
                    {
                        diffEntries.Add(new(Diff: entry.Hash - indexEntry.Hash, indexEntry.Index, SymbolIndex: entry.Index));
                    }
                }

                diffEntries.Sort();

                foreach (var entry in diffEntries)
                {
                    var index = entry.Index;
                    ref var symbol = ref symbolsArray[entry.SymbolIndex];
                    ref var mappedSymbol = ref mappedSymbols[index];
                    if (!isDefault(symbol) && isDefault(mappedSymbol))
                    {
                        mappedSymbol = symbol;
                        symbolMap[symbol] = index;

                        // Prevent reassignment of symbol
                        symbol = default;
                        assignmentCount++;
                        if (assignmentCount == symbolsArray.Length) break;
                    }
                }
            }

            Contract.Assert(assignmentCount == symbolsArray.Length);
        }
        else
        {
            bool isIndexAvailable = true;
            foreach (var entry in entries)
            {
                if (!entry.IsSymbol)
                {
                    lastIndexEntry = entry;
                    isIndexAvailable = mappedSymbols[entry.Index] == null;
                }
                else if (lastIndexEntry != null && isIndexAvailable)
                {
                    var index = lastIndexEntry.Value.Index;
                    ref var symbol = ref symbolsArray[entry.Index];
                    ref var mappedSymbol = ref mappedSymbols[index];
                    if (!isDefault(symbol) && isDefault(mappedSymbol))
                    {
                        mappedSymbol = symbol;
                        isIndexAvailable = false;
                        symbolMap[symbol] = index;

                        // Prevent reassignment of symbol
                        symbol = default;
                    }
                }
            }
        }

        return symbolMap.ToImmutable();
    }
}
