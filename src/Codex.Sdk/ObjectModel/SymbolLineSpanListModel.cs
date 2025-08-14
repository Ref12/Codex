using System.Collections.Generic;
using System.Runtime.Serialization;
using Codex.ObjectModel;
using Codex.Utilities;
using static Codex.Utilities.SerializationUtilities;

namespace Codex.ObjectModel.Implementation
{
    [DataContract]
    public class SymbolLineSpanListModel : SpanListModel<SymbolSpan, SpanListSegmentModel, SymbolSpan, int>, ISymbolLineSpanListModel
    {
        public static readonly IComparer<SymbolSpan> SharedSymbolLineModelComparer = new ComparerBuilder<SymbolSpan>()
            .CompareByAfter(s => s.LineSpanText);

        public static readonly IComparer<SymbolSpan> OrdinalSymbolLineModelComparer = new ComparerBuilder<SymbolSpan>()
            .CompareByAfter(s => s.LineNumber);

        public SymbolLineSpanListModel()
        {
            Optimize = false;
        }

        public SymbolLineSpanListModel(IReadOnlyList<SymbolSpan> spans, bool useOrdinalSort = false)
            : base(spans, sharedValueSorter: useOrdinalSort ? OrdinalSymbolLineModelComparer : SharedSymbolLineModelComparer)
        {
            Optimize = false;
        }

        public override SpanListSegmentModel CreateSegment(ListSegment<SymbolSpan> segmentSpans)
        {
            return new SpanListSegmentModel();
        }

        public override SymbolSpan CreateSpan(int start, int length, SymbolSpan shared, SpanListSegmentModel segment, int segmentOffset)
        {
            return new SymbolSpan()
            {
                Start = shared.Start + start,
                LineSpanStart = start,
                Length = length,
                LineSpanText = shared.LineSpanText,
                LineIndex = shared.LineIndex
            };
        }

        public override SymbolSpan GetShared(SymbolSpan span)
        {
            return new SymbolSpan()
            {
                LineSpanText = span.LineSpanText,
                LineIndex = span.LineIndex,
                Start = span.Start - span.LineSpanStart
            };
        }

        public override int GetStart(SymbolSpan span, SymbolSpan shared)
        {
            return span.Start - shared.Start;
        }

        public override int GetSharedKey(SymbolSpan span)
        {
            return span.LineIndex;
        }

        [OnSerializing]
        public void PostProcessReferences(StreamingContext context)
        {
            CharString lineSpanText = null;
            foreach (var symbolLine in SharedValues)
            {
                symbolLine.LineSpanText = RemoveDuplicate(symbolLine.LineSpanText, ref lineSpanText);
            }
        }

        [OnDeserialized]
        public void MakeReferences(StreamingContext context)
        {
            CharString lineSpanText = null;
            foreach (var symbolLine in SharedValues)
            {
                symbolLine.LineSpanText = AssignDuplicate(symbolLine.LineSpanText, ref lineSpanText);
            }
        }
    }
}
