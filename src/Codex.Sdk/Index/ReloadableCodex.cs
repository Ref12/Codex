using Codex.Sdk.Utilities;

namespace Codex.Sdk.Search;


public record ReloadableCodex(Func<ReloadableCodex, ContextCodexArgumentsBase, Task<ICodex>> LoadCodexAsync) : CodexWrapperBase
{
    protected virtual int RetryCount => 3;

    Task<ICodex> _loadCodex;
    private int _version;

    public int Version => _version;

    private static AsyncLocal<ReloadToken> _scopedReloadableCodex = new();

    public static bool TryGetToken(out ReloadToken token)
    {
        token = _scopedReloadableCodex.Value;
        return token != null;
    }

    public bool Invalidate(int version)
    {
        if (Atomic.TryCompareExchange(ref _version, version + 1, comparand: version))
        {
            _loadCodex = null;
            return true;
        }

        return false;
    }

    public override sealed async ValueTask<ICodex> GetBaseCodex(ContextCodexArgumentsBase arguments)
    {
        var codex = await Atomic.RunOnceAsync(ref _loadCodex, (@this: this, arguments), static t => t.@this.LoadCodexAsync(t.@this, t.arguments));
        return codex;
    }

    public Task<TResponse> CustomRunAsync<TArgs, TResponse>(TArgs arguments, Func<ICodex, Task<TResponse>> runAsync)
        where TArgs : ContextCodexArgumentsBase
        where TResponse : IndexQueryResponse, new()
    {
        return RunAsync(arguments, runAsync);
    }

    protected override async Task<TResponse> RunAsync<TArgs, TResponse>(TArgs arguments, Func<ICodex, Task<TResponse>> runAsync)
    {
        int i = 0;

        Retry:
        var startToken = new ReloadToken(this);
        bool shouldRetry()
        {
            // Check if codex was invalidated
            return startToken.Version != _version && i++ < RetryCount;
        }

        try
        {
            _scopedReloadableCodex.Value = startToken;
            var result = await base.RunAsync(arguments, runAsync);
            if (shouldRetry())
            {
                goto Retry;
            }

            return result;
        }
        catch when (shouldRetry())
        {
            goto Retry;
        }
    }

    public class ReloadToken(ReloadableCodex codex)
    {
        public int Version { get; } = codex.Version;

        public bool InvalidateCodex()
        {
            return codex.Invalidate(Version);
        }
    }
}