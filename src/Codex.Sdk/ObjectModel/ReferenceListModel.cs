using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Codex.ObjectModel;
using Codex.Utilities;
using static Codex.Utilities.SerializationUtilities;

namespace Codex.ObjectModel.Implementation
{
    [DataContract]
    public class ReferenceListModel : SpanListModel<ReferenceSpan, ReferenceSpanListSegmentModel, ReferenceSymbol, ReferenceSymbol>, IReferenceListModel
    {
        public static readonly IEqualityComparer<ReferenceSymbol> ReferenceSymbolEqualityComparer = new EqualityComparerBuilder<ReferenceSymbol>()
            .CompareByAfter(s => s.ProjectId)
            .CompareByAfter(s => s.Id.Value)
            .CompareByAfter(s => s.ReferenceKind);

        public static readonly IComparer<ReferenceSymbol> ReferenceSymbolComparer = new ComparerBuilder<ReferenceSymbol>()
            .CompareByAfter(s => s.ProjectId)
            .CompareByAfter(s => s.Kind)
            .CompareByAfter(s => s.Id.Value)
            .CompareByAfter(s => s.ReferenceKind);

        private static readonly SymbolSpan EmptySymbolSpan = new SymbolSpan();

        [DataMember(Order = 20)]
        public SymbolLineSpanListModel LineSpanModel { get; set; }

        [DataMember(Order = 21)]
        public IntegerListModel LineIndices { get; set; }

        IReadOnlyList<IReferenceSymbol> IReferenceListModel.SharedValues => SharedValues;

        public ReferenceListModel()
        {
        }

        public ReferenceListModel(IReadOnlyList<ReferenceSpan> spans, bool includeLineInfo = false, bool externalLineTextPersistence = false)
            : base(spans, ReferenceSymbolEqualityComparer, ReferenceSymbolComparer)
        {
            if (includeLineInfo)
            {
                LineSpanModel = new SymbolLineSpanListModel(spans, useOrdinalSort: externalLineTextPersistence)
                {
                    // Start/length already captured. No need for it in the line data
                    IncludeSpanRanges = false
                };

                if (externalLineTextPersistence)
                {
                    LineIndices = IntegerListModel.Create(LineSpanModel.SharedValues, span => span.LineIndex);
                }
            }
            //PostProcessReferences();
        }

        public static ReferenceListModel CreateFrom(IReadOnlyList<ReferenceSpan> spans, bool includeLineInfo = false, bool externalLineTextPersistence = false)
        {
            //if (spans is IndexableListAdapter<ReferenceSpan> list && list.Indexable is ReferenceListModel model)
            //{
            //    var listModel = new ReferenceListModel(spans, includeLineInfo, externalLineTextPersistence);
            //    return model;
            //}

            return new ReferenceListModel(spans, includeLineInfo, externalLineTextPersistence);
        }

        [OnSerializing]
        public void PostProcessReferences(StreamingContext context)
        {
            string projectId = null;
            StringEnum<SymbolKinds> kind = default;
            SymbolId id = default(SymbolId);
            foreach (var reference in SharedValues)
            {
                reference.ProjectId = RemoveDuplicate(reference.ProjectId, ref projectId);
                reference.Kind = RemoveDuplicate(reference.Kind, ref kind);
                reference.Id = RemoveDuplicate(reference.Id, ref id);
                reference.ExcludeFromSearch = false;
            }

            if (LineSpanModel != null && LineIndices != null)
            {
                LineSpanModel.SharedValues.Clear();

                if (Optimize)
                {
                    LineIndices.Optimize(new OptimizationContext());
                }
            }
        }

        [OnDeserialized]
        public void MakeReferences(StreamingContext context)
        {
            string projectId = null;
            StringEnum<SymbolKinds> kind = default;
            SymbolId id = default(SymbolId);
            foreach (var reference in SharedValues)
            {
                reference.ProjectId = AssignDuplicate(reference.ProjectId, ref projectId);
                reference.Kind = AssignDuplicate(reference.Kind, ref kind);
                reference.Id = AssignDuplicate(reference.Id, ref id);
            }

            if (LineSpanModel != null && LineIndices != null)
            {
                if (LineIndices.CompressedData != null)
                {
                    LineIndices.ExpandData(new OptimizationContext());
                }

                for (int i = 0; i < LineIndices.Count; i++)
                {
                    LineSpanModel.SharedValues.Add(new SymbolSpan()
                    {
                        LineIndex = LineIndices[i]
                    });
                }
            }
        }

        public override ReferenceSpanListSegmentModel CreateSegment(ListSegment<ReferenceSpan> segmentSpans)
        {
            return new ReferenceSpanListSegmentModel(segmentSpans);
        }

        public override ReferenceSpan CreateSpan(int start, int length, ReferenceSymbol shared, ReferenceSpanListSegmentModel segment, int segmentOffset)
        {
            if (shared.ProjectId == null || shared.Kind.StringValue == null || shared.ReferenceKind == ReferenceKind.None)
            {
                MakeReferences(default(StreamingContext));
            }

            // This is to workaround a former bug where ExcludeFromSearch was
            // mistakenly retained on shared value. This value should come from
            // segment.GetExcludedFromSearch(..) below
            shared.ExcludeFromSearch = false;

            var index = segment.SegmentStartIndex + segmentOffset;
            var lineSpan = LineSpanModel?.GetShared(index) ?? EmptySymbolSpan;

            var relatedDefinition = segment.GetRelatedDefinition(segmentOffset);
            var excludeFromSearch = segment.GetExcludedFromSearch(segmentOffset);

            return new ReferenceSpan(lineSpan)
            {
                Start = start,
                Length = length,
                Reference = !excludeFromSearch ? shared : new ReferenceSymbol(shared)
                {
                    ExcludeFromSearch = excludeFromSearch,
                },
                LineSpanStart = start - lineSpan.Start,
                RelatedDefinition = SymbolId.UnsafeCreateWithValue(relatedDefinition)
            };
        }

        public override ReferenceSymbol GetShared(ReferenceSpan span)
        {
            // Copy the reference so changes made during serialization do not affect
            // original reference
            return new ReferenceSymbol(span.Reference)
            {
                ExcludeFromSearch = false
            };
        }

        public override ReferenceSymbol GetSharedKey(ReferenceSpan span)
        {
            return span.Reference;
        }
    }

    [DataContract]
    public class ReferenceSpanListSegmentModel : SpanListSegmentModel
    {
        [DataMember(Order = 20)]
        public IntegerListModel ExcludedFromSearchSpans { get; set; }
        [DataMember(Order = 21)]
        public IntegerListModel RelatedDefinitionsIndices { get; set; }
        [DataMember(Order = 22)]
        public List<string> RelatedDefinitionIds { get; set; }

        [Placeholder.Todo("Serialize this instead of integer list? Does this even need to be serialized for indexing?")]
        private BitArray excludedFromSearchBitArray;

        public ReferenceSpanListSegmentModel()
        {
        }

        public ReferenceSpanListSegmentModel(ListSegment<ReferenceSpan> spans)
        {
            ListSet<string> listSet = new ListSet<string>();

            if (spans.Any(s => s.RelatedDefinition.Value != null))
            {
                RelatedDefinitionsIndices = IntegerListModel.Create(spans, span => listSet.Add(span.RelatedDefinition.Value ?? string.Empty));
                RelatedDefinitionIds = listSet.List;
            }

            excludedFromSearchBitArray = GetExcludedFromSearchBitArray(spans);
            if (excludedFromSearchBitArray != null)
            {
                ExcludedFromSearchSpans = new IntegerListModel(excludedFromSearchBitArray);
            }
        }

        public bool GetExcludedFromSearch(int spanIndex)
        {
            if (ExcludedFromSearchSpans == null)
            {
                return false;
            }

            if (excludedFromSearchBitArray == null)
            {
                excludedFromSearchBitArray = new BitArray(ExcludedFromSearchSpans.Data);
            }

            return excludedFromSearchBitArray[spanIndex];
        }

        public string GetRelatedDefinition(int spanIndex)
        {
            if (RelatedDefinitionIds == null)
            {
                return null;
            }

            var relatedDefinition = RelatedDefinitionIds[RelatedDefinitionsIndices[spanIndex]];
            if (string.IsNullOrEmpty(relatedDefinition))
            {
                return null;
            }

            return relatedDefinition;
        }

        private static BitArray GetExcludedFromSearchBitArray(ListSegment<ReferenceSpan> spans)
        {
            BitArray bitArray = null;

            for (int i = 0; i < spans.Count; i++)
            {
                var span = spans[i];
                if (span.Reference.ExcludeFromSearch)
                {
                    bitArray = bitArray ?? new BitArray(spans.Count);
                    bitArray[i] = true;
                }
            }

            return bitArray;
        }

        internal override void OptimizeLists(OptimizationContext context)
        {
            ExcludedFromSearchSpans?.Optimize(context);
            RelatedDefinitionsIndices?.Optimize(context);

            base.OptimizeLists(context);
        }

        internal override void ExpandLists(OptimizationContext context)
        {
            ExcludedFromSearchSpans?.ExpandData(context);
            RelatedDefinitionsIndices?.ExpandData(context);

            base.ExpandLists(context);
        }
    }
}
