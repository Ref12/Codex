namespace Codex.ObjectModel
{
    /// <summary>
    /// Marker interface for searchable entities
    /// TODO: Consider moving <see cref="EntityContentId"/> out if its not needed by all searchable entities
    /// </summary>
    public partial interface ISearchEntity : ITypedSearchEntity
    {
        [Exclude(ObjectStage.Hash)]
        MurmurHash Uid { get; set; }

        /// <summary>
        /// Defines the content addressable identifier for the entity. This is used
        /// to determine if an entity with the same <see cref="Uid"/> should be updated
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        [Exclude(ObjectStage.Hash)]
        MurmurHash EntityContentId { get; set; }

        /// <summary>
        /// Defines the size of the raw serialized entity.
        /// </summary>
        [Exclude(ObjectStage.Hash)]
        int EntityContentSize { get; set; }

        /// <summary>
        /// Indicates if the entity is newly added to the store.
        /// </summary>
        [Include(ObjectStage.None)]
        bool IsAdded { get; set; }

        /// <summary>
        /// The per-group stable identity. This is NOT persisted in main entity document.
        /// It can be obtained from doc values if needed at some point. In fact, its probably
        /// already known in the lookup as a part of stored filter application.
        /// </summary>
        [Include(ObjectStage.None)]
        int StableId { get; set; }
    }

    /// <summary>
    /// A search entity which allows retrieving the underlying type
    /// </summary>
    /// <remarks>
    /// This type is separate because code generation doesn't allow interface types to have methods
    /// </remarks>
    [GeneratorExclude]
    public interface ITypedSearchEntity
    {
        SearchType GetSearchType() => throw new NotSupportedException();

        int DocId { get => -1; set { } }
    }

    [GeneratorExclude]
    public partial interface ISearchEntity<T> : ISearchEntity, ITypedSearchEntity
        where T : class, ISearchEntity<T>
    {
        SearchType ITypedSearchEntity.GetSearchType() => SearchTypes.GetSearchType<T>();
    }

    public static class SearchEntityExtensions
    {
        public static SearchTypeId GetSearchTypeId(this ISearchEntity entity)
        {
            return entity.GetSearchType().TypeId;
        }
    }
}
