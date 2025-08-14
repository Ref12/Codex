using Codex.Utilities;

namespace Codex.Storage;

public record TransitioningStableIdStorage(IStableIdStorage Primary, IStableIdStorage Secondary) : IStableIdStorage
{
    public async ValueTask DisposeAsync()
    {
        await Primary.DisposeAsync();
        await Secondary.DisposeAsync();
    }

    IEnumerable<string> IStableIdStorage.GetPendingDeletions() => Primary.GetPendingDeletions().Concat(Secondary.GetPendingDeletions());

    public void Initialize(StableIdStorageHeader header)
    {
        Primary.Initialize(header);
        Secondary.Initialize(header);
    }

    public bool TryGet(SearchType searchType, ShortHash entityUid, out DocumentRef docRef)
    {
        return Primary.TryGet(searchType, entityUid, out docRef);
    }

    public bool TryReserve(SearchType searchType, ShortHash entityUid, out DocumentRef docRef)
    {
        if(Primary.TryReserve(searchType, entityUid, out docRef))
        {
            Secondary.UnsafePut(searchType, entityUid, docRef);
            return true;
        }
        else
        {
            return false;
        }
    }

    public void UnsafePut(SearchType searchType, ShortHash entityUid, DocumentRef docRef)
    {
        Primary.UnsafePut(searchType, entityUid, docRef);
        Secondary.UnsafePut(searchType, entityUid, docRef);
    }
}
