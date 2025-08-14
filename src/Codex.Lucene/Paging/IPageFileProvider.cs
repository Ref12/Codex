namespace Codex.Lucene.Search
{
    public interface IPageFileProvider
    {
        IPageFile CreatePageFile(string path, PagingFileEntry entry);
    }

    public interface IPageFile : IDisposable
    {
        PagingFileEntry Entry { get; }

        IPageFile CreateClone();

        long Length { get; }

        int ReadRange(long position, byte[] buffer, int offset, int count);

        void IDisposable.Dispose() { }

        void AddOwner() { }
    }
}
