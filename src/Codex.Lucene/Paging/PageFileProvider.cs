namespace Codex.Lucene.Search
{
    public abstract class PageFileProvider
    {
        public IPageSegmentCache SegmentPreCache { get; set; } = new SegmentCacheMap();
    }
}
