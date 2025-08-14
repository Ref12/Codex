using Codex.ObjectModel;

namespace Codex.Sdk.Search
{
    public record NullCodex : CodexWrapperBase
    {
        public override ValueTask<ICodex> GetBaseCodex(ContextCodexArgumentsBase arguments)
        {
            throw new NotImplementedException();
        }

        protected override Task<TResponse> RunAsync<TArgs, TResponse>(TArgs arguments, Func<ICodex, Task<TResponse>> runAsync)
        {
            var response = new TResponse();
            response.Error = "No results found.";
            return Task.FromResult(response);
        }
    }
}
