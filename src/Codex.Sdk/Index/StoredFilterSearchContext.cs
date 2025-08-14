using Codex.ObjectModel;
using Codex.Sdk.Search;

namespace Codex.Search
{
    public class StoredFilterSearchContext<TClient> : ClientContext<TClient>, IStoredFilterInfo
        where TClient : IClient
    {
        public StoredFilterSearchContext(TClient client)
        {
            Client = client;
        }

        public RepoAccess AccessLevel { get; set; }

        public IStoredFilterInfo SecondaryFilter { get; set; }

        public virtual IStoredFilterInfo DeclaredDefinitionsFilter => null;

        public bool DedupeEntities { get; set; } = true;
    }
}
