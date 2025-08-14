using Codex.Sdk.Utilities;
using Codex.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Codex
{
    public static class TextChunker
    {
        public static List<ListSegment<ReadOnlySegment<char>>> GetConsistentChunks(IReadOnlyList<ReadOnlySegment<char>> lines, int chunkSizeHint, int minChunkSize)
        {
            var chunks = new List<ListSegment<ReadOnlySegment<char>>>();
            using (var lease = Pools.EncoderContextPool.Acquire())
            {
                EncoderContext context = lease.Instance;
                var hashList = context.UIntList;

                int chunkStartIndex = 0;
                double chunkFactor = 0.25;
                int chunkMinLength = Math.Max(minChunkSize, (int)(chunkSizeHint * (1 - chunkFactor)));
                int chunkMaxLength = (int)((chunkMinLength * (1 + chunkFactor)) / (1 - chunkFactor));
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    var chunkLength = (i - chunkStartIndex) + 1;
                    ulong hash = ulong.MaxValue;

                    if (chunkLength >= chunkMinLength)
                    {
                        hash = ComputeLineHash(context, line, (uint)i);
                    }

                    hashList.Add(hash);

                    if (chunkLength == chunkMaxLength)
                    {
                        // Scan to find line with maximum hash in range of end lines which would make a valid chunk
                        ulong max = 0;
                        int chunkEndIndex = chunkStartIndex;
                        for (int chunkLineIndex = chunkStartIndex + chunkMinLength; chunkLineIndex <= i; chunkLineIndex++)
                        {
                            var chunkLineHash = hashList[chunkLineIndex];
                            if (chunkLineHash >= max)
                            {
                                max = chunkLineHash;
                                chunkEndIndex = chunkLineIndex;
                            }
                        }

                        // Now scan to find a better breakpoint (namely lines which are only whitespace or punctation (preferring whitespace only))
                        ulong min = uint.MaxValue;
                        for (int chunkLineIndex = chunkEndIndex; chunkLineIndex <= i; chunkLineIndex++)
                        {
                            var chunkLineHash = hashList[chunkLineIndex];

                            if (chunkLineHash <= min)
                            {
                                min = chunkLineHash;
                                chunkEndIndex = chunkLineIndex;
                            }
                        }

                        if ((lines.Count - chunkEndIndex) < chunkMinLength)
                        {
                            // Merge in tail lines into current chunk if they would not reach minimum chunk size
                            chunkEndIndex = lines.Count - 1;
                        }

                        AddChunk(chunks, lines, chunkStartIndex, chunkEndIndex);
                        chunkStartIndex = chunkEndIndex + 1;
                    }
                }

                AddChunk(chunks, lines, chunkStartIndex, lines.Count - 1);
                return chunks;
            }
        }

        private static void AddChunk(List<ListSegment<ReadOnlySegment<char>>> chunks, IReadOnlyList<ReadOnlySegment<char>> lines, int chunkStartIndex, int chunkEndIndex)
        {
            if (chunkEndIndex >= chunkStartIndex)
            {
                chunks.Add(new ListSegment<ReadOnlySegment<char>>(lines, chunkStartIndex, count: (chunkEndIndex - chunkStartIndex) + 1));
            }
        }

        private static ulong ComputeLineHash(EncoderContext context, ReadOnlyMemory<char> line, uint lineNumber)
        {
            if (IsNullOrWhiteSpace(line.Span))
            {
                return 1_000_000 - lineNumber;
            }
            else if (IsPunctuationOrWhitespace(line.Span))
            {
                return 1_000_000 + lineNumber;
            }
            else
            {
                return context.ToHash(line).High + lineNumber;
            }
        }

        private static bool IsNullOrWhiteSpace(ReadOnlySpan<char> line)
        {
            // Iterate line in reverse since lines tend to be preceded with whitespace for indentation
            for (int i = line.Length - 1; i >= 0; i--)
            {
                var c = line[i];
                if (!char.IsWhiteSpace(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPunctuationOrWhitespace(ReadOnlySpan<char> line)
        {
            // Iterate line in reverse since lines tend to be preceded with whitespace for indentation
            for (int i = line.Length - 1; i >= 0; i--)
            {
                var c = line[i];
                if (!(char.IsPunctuation(c) || char.IsWhiteSpace(c)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
