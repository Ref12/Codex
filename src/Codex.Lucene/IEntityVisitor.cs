namespace Codex.Lucene.Search
{
    public interface IEntityVisitor<T>
        where T : class, ISearchEntity
    {
        void OnAdding(SearchType<T> searchType, T entity);
    }
}