using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Utilities.Serialization;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    public static class IndexingUtilities
    {
        private const ulong HighBits = ulong.MaxValue - uint.MaxValue;
        private const ulong LowBits = uint.MaxValue;
        public const int UidLength = 12;

        private static readonly char[] s_toLowerInvariantCache = CreateToLowerInvariantCache();

        private static readonly char[] s_hexMap =
        {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};

        private static char[] CreateToLowerInvariantCache()
        {
            var a = new char[char.MaxValue + 1];
            for (int c = char.MinValue; c <= char.MaxValue; c++)
            {
                a[c] = char.ToLowerInvariant((char)c);
            }

            return a;
        }

        public static ShortHash ToShortHash(this in MurmurHash hash)
        {
            return MemoryMarshal.Read<ShortHash>(SpanSerializationExtensions.AsBytesUnsafe(hash));
        }

        /// <summary>
        /// <code>code.ToLowerInvariant</code> is surprisingly expensive; this is a cache
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ToLowerInvariantFast(this char character)
        {
            return s_toLowerInvariantCache[character];
        }

        /// <summary>
        /// Get the bytes as a hex string.
        /// </summary>
        public unsafe static string ToHex(this IReadOnlyList<byte> checksum)
        {
            char* charBuffer = stackalloc char[(2 * checksum.Count) + 1];
            var j = 0;

            for (var i = 0; i < checksum.Count; i++)
            {
                charBuffer[j++] = s_hexMap[(checksum[i] & 0xF0) >> 4];
                charBuffer[j++] = s_hexMap[checksum[i] & 0x0F];
            }

            charBuffer[j] = '\0';

            return new string(charBuffer);
        }

        public static string GetChecksumKey(ChecksumAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case ChecksumAlgorithm.Sha1:
                    return "Checksum.Sha1";
                case ChecksumAlgorithm.Sha256:
                    return "Checksum.Sha256";
                case ChecksumAlgorithm.MD5:
                    return "Checksum.MD5";
                default:
                    return null;
            }
        }

        public static string ComputeSymbolUid(ReadOnlySpan<char> symbolIdName)
        {
            var hash = ComputeFullHash(symbolIdName);
            return ComputeSymbolUid(hash);
        }

        public static string ComputeSymbolUid(in MurmurHash hash)
        {
            var bytes = SpanSerializationExtensions.AsBytesUnsafe(hash);
            return Base64.Convert(bytes, 0, static (span, arg) =>
            {
                ref var firstChar = ref span[0];
                if (char.IsBetween(firstChar, '0', '9'))
                {
                    firstChar = (char)(firstChar - '0' + 'a');
                }

                return new string(span.Slice(0, UidLength));
            },
            Base64.Format.LowercaseAlphanumeric);
        }

        public static string ComputeUrlSafeHashString(ReadOnlySpan<char> symbolIdName, int maxLength = int.MaxValue)
        {
            var hash = UnicodeHash(symbolIdName);
            return ComputeHashString(hash, maxLength);
        }

        public static string ComputeHashString(MurmurHash hash, int maxLength = int.MaxValue)
        {
            return hash.ToBase64String(maxCharLength: maxLength, Base64.Format.UrlSafe);
        }

        public static MurmurHash UnicodeHash(ReadOnlySpan<char> value)
        {
            var bytes = MemoryMarshal.AsBytes(value);
            var hasher = new Murmur3();
            return hasher.ComputeHash(bytes);
        }

        public static MurmurHash ComputeFullHash(ReadOnlySpan<char> value)
        {
            var max = Encoding.UTF8.GetMaxByteCount(value.Length);
            var buffer = Perf.TryAllocateIfLarge<byte>(max) ?? stackalloc byte[max];

            int byteLength = Encoding.UTF8.GetBytes(value, buffer);
            var hasher = new Murmur3();
            return hasher.ComputeHash(buffer.Slice(0, byteLength));
        }

        public static QualifiedName ParseQualifiedName(string fullyQualifiedTerm)
        {
            QualifiedName qn = new QualifiedName();
            if (fullyQualifiedTerm == null)
            {
                return qn;
            }

            int indexOfLastDot = fullyQualifiedTerm.LastIndexOf('.');
            if (indexOfLastDot >= 0)
            {
                qn.ContainerName = fullyQualifiedTerm.Substring(0, indexOfLastDot);
            }

            qn.Name = fullyQualifiedTerm.Substring(indexOfLastDot + 1);
            return qn;
        }

        public static IEnumerable<LineSpan<IClassificationSpan>> GetLineClassifications(this IBoundSourceFile file)
        {
            return GetLineClassifications(file.Classifications, file.SourceFile.Content.GetLineSpans(includeLineBreakInSpan: false));
        }

        public static string GetExtentSubstring(this Extent extent, string value)
        {
            return value.Substring(extent.Start, extent.Length);
        }

        public static ReadOnlySpan<T> GetExtentSpan<T>(this Extent extent, ReadOnlyMemory<T> value)
        {
            return value.Slice(extent.Start, extent.Length).Span;
        }

        public static ReadOnlySpan<T> GetExtentSpan<T>(this Extent extent, ReadOnlySpan<T> value)
        {
            return value.Slice(extent.Start, extent.Length);
        }

        public static ReadOnlyMemory<T> GetExtentMemory<T>(this Extent extent, ReadOnlyMemory<T> value)
        {
            return value.Slice(extent.Start, extent.Length);
        }

        public static IEnumerable<LineSpan<IClassificationSpan>> GetLineClassifications(IEnumerable<IClassificationSpan> classifications, IEnumerable<Extent> lineRanges)
        {
            return GetLineSpans(classifications, lineRanges, allowEmpty: false);
        }

        public static IEnumerable<LineSpan<TSpan>> GetLineSpans<TSpan>(IEnumerable<TSpan> sourceSpans, IEnumerable<Extent> lineRanges, bool allowEmpty)
            where TSpan : ISpan
        {
            var minLength = allowEmpty ? -1 : 0;
            var lineMarkers = lineRanges.Select((l, index) => new SpanWithLine<TSpan>(default, index, l));
            var preannotatedClassifications = sourceSpans.Select((l, index) => new SpanWithLine<TSpan>(l, StartLineIndex: null, l.AsExtent()));

            Extent? currentLineExtent = null;
            int? currentLineNumber = null;
            SpanWithLine<TSpan>? lastPreannotatedClassification = null;
            foreach ((var lineMarker, var preannotatedClassification, var mode) in CollectionUtilities.DistinctMergeSorted(lineMarkers, preannotatedClassifications, i => i, i => i))
            {
                bool tryGetLineSpan(SpanWithLine<TSpan>? span, out LineSpan<TSpan> lineSpan)
                {
                    if (currentLineExtent != null && span?.Extent.Intersect(currentLineExtent.Value) is Extent extentInLine && extentInLine.Length > minLength)
                    {
                        var lineExtent = extentInLine.MakeRelative(currentLineExtent.Value.Start);
                        lineSpan = new LineSpan<TSpan>(
                            LineNumber: currentLineNumber.Value,
                            Start: extentInLine.Start,
                            Offset: lineExtent.Start,
                            Length: lineExtent.Length,
                            Value: span.Value.Span,
                            FullLineExtent: currentLineExtent.Value);
                        return true;
                    }
                    else
                    {
                        lineSpan = default;
                        return false;
                    }
                }

                if (mode == CollectionUtilities.MergeMode.LeftOnly)
                {
                    // This is a line marker
                    currentLineExtent = lineMarker.Extent;

                    // Line numbers of 1-based
                    currentLineNumber = lineMarker.StartLineIndex + 1;

                    if (tryGetLineSpan(lastPreannotatedClassification, out var lineSpan))
                    {
                        yield return lineSpan;
                    }

                }
                else
                {
                    Contract.Assert(mode == CollectionUtilities.MergeMode.RightOnly);

                    if (tryGetLineSpan(preannotatedClassification, out var lineSpan))
                    {
                        yield return lineSpan;
                    }

                    lastPreannotatedClassification = preannotatedClassification;
                }
            }
        }

        private record struct SpanWithLine<TSpan>(TSpan Span, int? StartLineIndex, Extent Extent) : IComparable<SpanWithLine<TSpan>>
        {
            // Elements with StartLineIndex set take precedence since those are line markers
            private int SameStartSortKey => StartLineIndex != null ? 0 : 1;

            public int CompareTo(SpanWithLine<TSpan> other)
            {
                static int getIsNonEmpty(SpanWithLine<TSpan> s) => s.Extent.Length == 0 ? 0 : 1;
                return Extent.Start.ChainCompareTo(other.Extent.Start)
                    ?? SameStartSortKey.ChainCompareTo(other.SameStartSortKey)
                    ?? getIsNonEmpty(this).CompareTo(getIsNonEmpty(other));
            }
        }

        public static List<ListSegment<ReadOnlySegment<char>>> GetTextIndexingChunks(IReadOnlyList<ReadOnlySegment<char>> lines)
        {
            return TextChunker.GetConsistentChunks(lines, chunkSizeHint: lines.Count / 100, minChunkSize: 10);
        }

        /// <summary>
        /// Essentially extracts all upper-case characters not preceded by another uppercase character
        /// </summary>
        public static T AccumulateAbbreviationCharacters<T>(this IDefinitionSymbol symbol, T initial, Func<(T accumulated, char ch, int index), T> accumulator)
        {
            return AccumulateAbbreviationCharacters(symbol.ShortName, initial, accumulator);
        }

        /// <summary>
        /// Essentially extracts all upper-case characters not preceded by another uppercase character
        /// </summary>
        public static T AccumulateAbbreviationCharacters<T>(string shortName, T initial, Func<(T accumulated, char ch, int index), T> accumulator)
        {
            if (string.IsNullOrEmpty(shortName))
            {
                return initial;
            }

            if (shortName.Length < 3)
            {
                return accumulator((initial, shortName[0], 0));
            }

            var value = initial;
            (bool isUpper, bool isAccumulated) state = default;
            for (int i = 0; i < shortName.Length; i++)
            {
                var ch = shortName[i];
                var lastState = state;
                state = (char.IsUpper(ch), false);

                if (i == 0 && char.IsLetter(shortName[0]))
                {
                    state.isAccumulated = true;
                    value = accumulator((value, ch, i));
                    continue;
                }

                if (state.isUpper)
                {
                    //if (!lastCharacterWasUppercase || 
                    //    // Special case interface names which start with canonical 'I{UpperChar}{LowerChar}' prefix
                    //    // For instance IDefinitionSymbol should be abbreviated as IDS even though ID are two consecutive upper characters
                    //    (i == 1 && ShortName[0] == 'I' && !char.IsUpper(ShortName[2])))
                    //{
                    //    value = accumulator(value, ShortName[i]);
                    //}

                    if (!lastState.isUpper)
                    {
                        state.isAccumulated = true;
                        value = accumulator((value, ch, i));
                        continue;
                    }
                }
                else
                {
                    if (lastState.isUpper && !lastState.isAccumulated && char.IsLower(ch))
                    {
                        // Current char is a lowercase character after an uppercase character which was not accumulated.
                        // Assume that the prior character must have been the start of a word and accumulate it
                        value = accumulator((value, shortName[i - 1], i - 1));
                    }
                }
            }

            return value;
        }
    }

    public class QualifiedName
    {
        public string ContainerName;
        public string Name;
    }

    public enum ChecksumAlgorithm
    {
        MD5,
        Sha1,
        Sha256
    }
}
