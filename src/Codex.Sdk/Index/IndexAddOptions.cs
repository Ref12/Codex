namespace Codex.Storage
{
    public record struct IndexAddOptions
    {
        public bool StoredExternally { get; set; }
        public FilterName AdditionalStoredFilters { get; set; }
        public bool HasStableId { get; set; }
    }

    [Flags]
    public enum FilterName : byte
    {
        None,
        DeclaredDefinitions = 1 << 0
    }
}