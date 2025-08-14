using System.Collections.Concurrent;
using static Codex.Lucene.Search.CachingPageFileProvider;

namespace Codex.Lucene.Search
{
    public interface IPageSegmentCache
    {
        bool TryGetSegment(PageSegmentKey key, int desiredLength, out PageFileSegment segment);

        void OnLoadedSegment(PageSegmentKey key, PageFileSegment segment);
    }

    public class SegmentCacheMap : IPageSegmentCache
    {
        public CachingPageFileProvider Owner { get; set; }

        public ConcurrentDictionary<PageSegmentKey, PageFileSegment> Map { get; } = new();

        public virtual bool TryGetSegment(PageSegmentKey key, int desiredLength, out PageFileSegment segment)
        {
            if (Map.TryGetValue(key, out segment))
            {
                segment.Source = SegmentSource.Precache;
                return true;
            }

            return false;
        }

        public virtual void OnLoadedSegment(PageSegmentKey key, PageFileSegment segment)
        {
        }
    }
}
