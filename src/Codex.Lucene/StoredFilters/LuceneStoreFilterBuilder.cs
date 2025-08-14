using System.Collections.Concurrent;
using Codex.Storage;
using Codex.Utilities;
using Lucene.Net.QueryParsers.Xml;

namespace Codex.Lucene.Search
{
    public enum FilterField : byte
    {
        Repo,
        Commit,
    }

    public class LuceneStoreFilterBuilder
    {
        public LazySearchTypesMap<Segment> SegmentMap { get; }

        public LuceneStoreFilterBuilder()
        {
            SegmentMap = new LazySearchTypesMap<Segment>(t => new Segment(t));
        }

        public void Add(SearchType searchType, DocumentRef docRef)
        {
            var segment = SegmentMap[searchType];
            segment.Add(docRef);
        }

        public record Segment(SearchType SearchType)
        {
            public ConcurrentRoaringFilterBuilder FilterBuilder = new ConcurrentRoaringFilterBuilder();

            public void Add(DocumentRef docRef)
            {
                FilterBuilder.Add(docRef.DocId);
            }
        }

        public PersistedStoredFilterSet ToPersisted()
        {
            var result = new PersistedStoredFilterSet();
            result.FiltersByType = SegmentMap.Enumerate(allowInit: false).ToDictionary(
                s => s.Key.TypeId,
                s => s.Value.FilterBuilder.Build());

            return result;
        }

        public void LoadPersisted(PersistedStoredFilterSet filterSet)
        {
            foreach (var searchType in SearchTypes.RegisteredSearchTypes)
            {
                if (filterSet.FiltersByType.TryGetValue(searchType.TypeId, out var filter))
                {
                    SegmentMap[searchType].FilterBuilder.RoaringFilter = filter;
                }
            }
        }

        public static void AddToFilters(SearchType searchType, DocumentRef docRef, IReadOnlyList<LuceneStoreFilterBuilder> filters)
        {
            if (filters?.Count > 0)
            {
                foreach (var filter in filters)
                {
                    filter.Add(searchType, docRef);
                }
            }
        }
    }
}
