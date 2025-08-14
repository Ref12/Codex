using Codex.ObjectModel;

namespace Codex.Sdk.Search
{
    public record CodexWrapper(ValueTask<ICodex> BaseCodexTask) : CodexWrapperBase
    {
        public CodexWrapper(ICodex BaseCodex) 
            : this(ValueTask.FromResult(BaseCodex))
        {
        }

        public override ValueTask<ICodex> GetBaseCodex(ContextCodexArgumentsBase arguments) => BaseCodexTask;
    }

    public abstract record CodexWrapperBase : ICodex
    {
        public abstract ValueTask<ICodex> GetBaseCodex(ContextCodexArgumentsBase arguments);

        public virtual async Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
        {
            return await RunAsync(arguments, c => c.FindAllReferencesAsync(arguments));
        }

        public virtual async Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            return await RunAsync(arguments, c => c.FindDefinitionAsync(arguments));
        }

        public virtual async Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            return await RunAsync(arguments, c => c.FindDefinitionLocationAsync(arguments));
        }

        public virtual async Task<IndexQueryResponse<GetProjectResult>> GetProjectAsync(GetProjectArguments arguments)
        {
            return await RunAsync(arguments, c => c.GetProjectAsync(arguments));
        }

        public virtual async Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments)
        {
            return await RunAsync(arguments, c => c.GetSourceAsync(arguments));
        }

        public virtual async Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
        {
            return await RunAsync(arguments, c => c.SearchAsync(arguments));
        }

        public async Task<IndexQueryHitsResponse<ICommit>> GetRepositoryHeadsAsync(GetRepositoryHeadsArguments arguments)
        {
            return await RunAsync(arguments, c => c.GetRepositoryHeadsAsync(arguments));
        }

        protected virtual async Task<TResponse> RunAsync<TArgs, TResponse>(TArgs arguments, Func<ICodex, Task<TResponse>> runAsync)
            where TArgs : ContextCodexArgumentsBase
            where TResponse : IndexQueryResponse, new()
        {
            ModifyArguments(arguments);
            var codex = await GetBaseCodex(arguments);
            return await runAsync(codex);
        }

        protected virtual void ModifyArguments(ContextCodexArgumentsBase arguments)
        {

        }
    }
}
