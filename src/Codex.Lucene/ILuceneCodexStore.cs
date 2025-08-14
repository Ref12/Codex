using Codex.Storage;

namespace Codex.Lucene.Search
{
    public interface ILuceneCodexStore : ICodexStore, ICodexStoreWriterProvider
    {
        LuceneWriteConfiguration Configuration { get; }
    }
}
