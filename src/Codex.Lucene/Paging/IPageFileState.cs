namespace Codex.Lucene.Search
{
    public interface IPageFileState : IDisposable
    {
        ValueTask<PageFileSegment> GetSegmentAsync(long position, int length);

        Lazy<PageFileSegment> GetSegment(long position, int length);

        IPageFileState CreateClone() => null;
    }
}
