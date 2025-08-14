namespace Codex.Lucene.Search
{
    public interface IPageFileAccessor
    {
        IPageFileState CreateState(string path, PagingFileEntry entry);

        Task<Stream> OpenStreamAsync(string path, Codex.Utilities.Extent? range = default, bool writable = false);
    }
}
