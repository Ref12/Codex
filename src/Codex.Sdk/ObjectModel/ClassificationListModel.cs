using System.Runtime.Serialization;
using Codex.Utilities;

namespace Codex.ObjectModel.Implementation
{
    [DataContract]
    public class ClassificationListModel : SpanListModel<ClassificationSpan, ClassificationSpanListSegmentModel, ClassificationStyle, StringEnum<ClassificationName>>, IClassificationListModel
    {
        public ClassificationListModel()
        {
        }

        public ClassificationListModel(IReadOnlyList<ClassificationSpan> spans)
            : base(spans)
        {
        }

        public static ClassificationListModel CreateFrom(IReadOnlyList<ClassificationSpan> spans)
        {
            //if (spans is IndexableListAdapter<ClassificationSpan> list && list.Indexable is ClassificationListModel model)
            //{
            //    return model;
            //}

            return new ClassificationListModel(spans);
        }

        public override ClassificationSpanListSegmentModel CreateSegment(ListSegment<ClassificationSpan> segmentSpans)
        {
            return new ClassificationSpanListSegmentModel()
            {
                LocalSymbolGroupIds = IntegerListModel.Create(segmentSpans, span => span.LocalGroupId, nullIfAllZeros: true),
                LocalSymbolDepths = IntegerListModel.Create(segmentSpans, span => span.SymbolDepth, nullIfAllZeros: true)
            };
        }

        public override ClassificationSpan CreateSpan(int start, int length, ClassificationStyle shared, ClassificationSpanListSegmentModel segment, int segmentOffset)
        {
            return new ClassificationSpan()
            {
                Start = start,
                Length = length,
                Classification = shared.Name,
                DefaultClassificationColor = shared.Color,
                LocalGroupId = segment.LocalSymbolGroupIds?[segmentOffset] ?? 0,
                SymbolDepth = segment.LocalSymbolDepths?[segmentOffset] ?? 0
            };
        }

        public override ClassificationStyle GetShared(ClassificationSpan span)
        {
            return new ClassificationStyle()
            {
                Name = span.Classification,
                Color = span.DefaultClassificationColor
            };
        }

        public override StringEnum<ClassificationName> GetSharedKey(ClassificationSpan span)
        {
            return span.Classification;
        }
    }

    [DataContract]
    public class ClassificationSpanListSegmentModel : SpanListSegmentModel
    {
        [DataMember(Order = 20)]
        public IntegerListModel LocalSymbolGroupIds { get; set; }

        [DataMember(Order = 21)]
        public IntegerListModel LocalSymbolDepths { get; set; }

        internal override void OptimizeLists(OptimizationContext context)
        {
            LocalSymbolGroupIds?.Optimize(context);
            LocalSymbolDepths?.Optimize(context);

            base.OptimizeLists(context);
        }

        internal override void ExpandLists(OptimizationContext context)
        {
            LocalSymbolGroupIds?.ExpandData(context);
            LocalSymbolDepths?.ExpandData(context);

            base.ExpandLists(context);
        }
    }
}
