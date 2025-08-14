using Codex.ObjectModel;

namespace Codex.Sdk.Search;

public record TieredCodex(ICodex LocalCodex, ICodex RemoteCodex) : ICodex
{
    public Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
    {
        return RunTieredAsync(codex => codex.SearchAsync(arguments), 
            r => r.Result?.HasHits() == true,
            results =>
            {
                results.localResult.Result.Merge(results.remoteResult.Result);
                return results.localResult;
            });
    }

    public Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
    {
        return RunTieredAsync(codex => codex.FindAllReferencesAsync(arguments),
            r => r.Result?.HasHits() == true,
            results =>
            {
                results.localResult.Result.Merge(results.remoteResult.Result);
                return results.localResult;
            });
    }

    public Task<IndexQueryHitsResponse<ICommit>> GetRepositoryHeadsAsync(GetRepositoryHeadsArguments arguments)
    {
        return RunTieredAsync(codex => codex.GetRepositoryHeadsAsync(arguments),
            r => r.Result?.HasHits() == true,
            results =>
            {
                results.localResult.Result.Merge(results.remoteResult.Result);
                return results.localResult;
            });
    }

    public async Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
    {
        bool fallbackFindAllRefs = arguments.FallbackFindAllReferences;

        // First pass without find all references fallback
        var response = await RunTieredAsync(codex => codex.FindDefinitionLocationAsync(arguments with
        {
            FallbackFindAllReferences = false
        }), r => r.Result?.HasHits() == true);

        if (response?.Result?.HasHits() == true || !arguments.FallbackFindAllReferences)
        {
            return response;
        }

        return await FindAllReferencesAsync(arguments);
    }

    public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
    {
        return RunTieredAsync(codex => codex.FindDefinitionAsync(arguments), r => r.Result != null);
    }

    public Task<IndexQueryResponse<GetProjectResult>> GetProjectAsync(GetProjectArguments arguments)
    {
        return RunTieredAsync(codex => codex.GetProjectAsync(arguments), r => r.Result != null);
    }

    public Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments)
    {
        return RunTieredAsync(codex => codex.GetSourceAsync(arguments), r => r.Result != null);
    }

    public async Task<TResult> RunTieredAsync<TResult>(Func<ICodex, Task<TResult>> runAsync,
        Func<TResult, bool> useResult,
        Func<(TResult localResult, TResult remoteResult), TResult> mergeResults = null)
    {
        var localResult = await runAsync(LocalCodex);
        bool useLocal = useResult(localResult);
        if (useLocal && mergeResults == null) return localResult;

        var remoteResult = await runAsync(RemoteCodex);
        if (useResult(remoteResult))
        {
            if (useLocal)
            {
                return mergeResults((localResult, remoteResult));
            }
            else
            {
                return remoteResult;
            }
        }
        else
        {
            return localResult;
        }
    }
}
