using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Codex.Configuration;
using Codex.Lucene.Search;
using Codex.Sdk;
using Codex.Sdk.Search;
using Codex.Search;
using Codex.Utilities;
using Codex.View;
using Codex.Web.Wasm;
using Codex.Workspaces;
using static Codex.Lucene.Search.PagingHelpers;

namespace Codex.Web.Common;

public class WebProgramBase(WebProgramArguments args) : CodexProgramBase, IRepositoryIndexer
{
    public virtual MainController App { get; } = new MainController();
    public virtual IHttpClient Client { get; set; } = new HttpClientWrapper();

    public IndexSourceLocation IndexSource { get; private set; }
    public string ResolvedIndexSourceUrl { get; private set; }
    public virtual HttpMessageHandler MessageHandler { get; } = new HttpClientHandler();
    public virtual WebViewModelController Controller { get; } = new WebViewModelController();

    public async Task RunAsync(ViewModelAddress? address = null)
    {
        Controller.RepositoryIndexer = this;
        var innerClient = GetClient(default);
        innerClient.BaseAddress = GetBaseAddress().EnsureTrailingSlash();
        var client = new QueryAugmentingHttpClientWrapper(innerClient);
        Client = client;
        SdkFeatures.HttpClient = client;
        SdkFeatures.GetClient = GetClient;

        App.CodexService ??= await GetCodexAsync(address);
        App.Controller = Controller;
        //if (address != null)
        //{
        //    await address.NavigateAsync(App, ViewModelAddress.InferMode.Startup);
        //}
    }

    public CodexPage GetPage()
    {
        return new CodexPage(this);
    }

    private Uri GetBaseAddress()
    {
        return args.RootUrl;
    }

    protected virtual IInnerHttpClient GetClient(HttpClientKind kind)
    {
        var client = new HttpClientWrapper();
        return client;
    }

    public async Task<ICodex> GetCodexAsync(ViewModelAddress? address)
    {
        var codex = await GetCodexAsync();

        return codex;
    }

    public virtual async Task<ICodex> GetCodexAsync()
    {
        string lastReloadHeaderValue = null;
        Timestamp lastTimestamp = Timestamp.New();

        // Create a reloadable codex so that 
        var reloadingCodex = new ReloadableCodex(
            LoadCodexAsync: (reloadCodex, args) => GetCodexCoreAsync(configRef =>
            {
                var config = configRef.Value;

                config.OnClientResponse = (kind, response) =>
                {
                    switch (kind)
                    {
                        case HttpClientKind.Index:
                            break;
                        case HttpClientKind.Entity:
                            if (IndexSource?.EntityFilesTriggerReload != true)
                            {
                                return;
                            }
                            break;
                        default:
                            return;
                    }

                    bool shouldReload = false;
                    if (!response.IsSuccessStatusCode)
                    {
                        shouldReload = true;
                    }
                    else if (IndexSource?.ReloadHeader is { } reloadHeaderName)
                    {
                        if (response.Headers.TryGetValues(reloadHeaderName, out var values)
                            || response.Content.Headers.TryGetValues(reloadHeaderName, out values))
                        {
                            var reloadHeaderValue = string.Join("|", values);
                            lastReloadHeaderValue ??= reloadHeaderValue;
                            if (lastReloadHeaderValue != reloadHeaderValue)
                            {
                                lastReloadHeaderValue = reloadHeaderValue;
                                shouldReload = true;
                            }
                        }
                    }

                    if (shouldReload && ReloadableCodex.TryGetToken(out var reloadToken) && reloadToken.InvalidateCodex())
                    {
                        Console.WriteLine($"Triggered reload: Version={reloadToken.Version}");
                    }
                };
            }));

        // Perform initiailization
        Task.Run(() => reloadingCodex.GetBaseCodex(new())).IgnoreAsync();

        return reloadingCodex;
    }

    public virtual async Task<ICodex> GetCodexCoreAsync(RefAction<PagingConfiguration> updateConfiguration = null)
    {
        LuceneConfiguration configuration;
        IndexSource = args.IndexSource ?? await GetIndexSourceAsync();
        var indexSourceUrl = IndexSource.Url = GetIndexSourceUrl(IndexSource);
        ResolvedIndexSourceUrl = indexSourceUrl = indexSourceUrl.ReplaceIgnoreCase("{timestamp}", IndexSource.Timestamp.ToPathString());
        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Loading index: {IndexSource}");
        indexSourceUrl = indexSourceUrl.Trim();
        {
            Console.WriteLine("Get configuration");
            AsyncOut<PagingDirectoryInfo> directoryInfo = new AsyncOut<PagingDirectoryInfo>();
            configuration = await PagingHelpers.CreatePagingConfigurationAsync(
                indexSourceUrl,
                configuration => updateConfiguration.ApplyTo(configuration with
                {
                    GetClient = GetClient,
                    Info = directoryInfo,
                    CacheLimit = SdkFeatures.WebProgramCacheLimit
                }));

            Console.WriteLine($"Got configuration: [{directoryInfo.Value.Entries.Count}]");
        }

        configuration.SourceTextRetriever = new BrowserSourceTextRetriever(Client);
        configuration.DefaultAccessLevel = RepoAccess.Internal;
        var codex = new LuceneCodex(configuration);

        return codex;
    }

    protected virtual async Task<IndexSourceLocation> GetIndexSourceAsync()
    {
        var json = await Client.GetStringAsync(args.IndexSourceJsonUri);
        return json.DeserializeEntity<IndexSourceLocation>();
    }

    protected virtual string GetIndexSourceUrl(IndexSourceLocation indexSource)
    {
        var url = indexSource.Url;
        if (url.Contains(":") || url.AsSpan().IndexOfAny(@"/\") == 0)
        {
            return url;
        }
        else
        {
            var result = args.RootUrl.Combine(args.IndexSourceJsonUri).Combine(url, treatBaseAsFolder: false);
            return result.ToString();
        }
    }

    internal record BrowserSourceTextRetriever(IHttpClient BrowserClient) : HttpClientSourceTextRetriever
    {
        protected override async ValueTask<(HttpResponseMessage response, string content)> GetStringAsync(HttpRequestMessage request)
        {
            var response = BrowserClient.SendMessage(request);
            return (response, response.Content.ReadAsStream().ReadAllText());
        }
    }

    public async Task IndexRepository(SourceControlUri[] repoUris)
    {
        {
            string message = App.CodexService is UpdatableCodex
                ? "No remote index service available"
                : "Local codex is not updatable";
            throw new NotSupportedException(message);
        }
    }
}