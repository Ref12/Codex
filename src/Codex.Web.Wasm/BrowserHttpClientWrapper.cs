using System.Threading;
using System.Threading.Tasks;
using Codex;
using Codex.Web.Common;
using System.Net.Http;
using System.Diagnostics.ContractsLight;

namespace System;

public partial class BrowserAppContext
{
    private record BrowserHttpClientWrapper() : HttpClientWrapper(new HttpClient())
    {
        public override async Task<byte[]> GetByteArrayAsync(StringUri? requestUri, CancellationToken cancellationToken = default)
        {
            await BrowserAppContext.SwitchToMainThread();
            return await base.GetByteArrayAsync(requestUri, cancellationToken);
        }

        public override HttpResponseMessage SendMessage(HttpRequestMessage request, CancellationToken token = default)
        {
            Contract.Assert(!BrowserAppContext.IsMainThread);
            return SendMessageAsync(request, token).GetAwaiter().GetResult();

        }

        public override async Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken token = default)
        {
            await BrowserAppContext.SwitchToMainThread();
            return await base.SendMessageAsync(request, token).ConfigureAwait(false);
        }
    }
}
