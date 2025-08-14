using System.Collections.Concurrent;
using System.Collections.Immutable;
using Codex.Utilities;
using Codex.Web.Common;

namespace Codex.Integration.Tests
{
    public record TrackingHttpClient : IHttpClient
    {
        public IHttpClient Inner { get; set; }

        public ConcurrentDictionary<(string, Extent?), int> Requests { get; } = new();
        public ConcurrentDictionary<string, (int count, ImmutableHashSet<Extent?> set)> RequestsByPath { get; } = new();

        public Uri BaseAddress => Inner.BaseAddress;

        public Task<ReadOnlyMemory<byte>> GetByteArrayAsync(StringUri? requestUri, CancellationToken cancellationToken = default)
        {
            Track(requestUri?.AsString(), null);
            return Inner.GetByteArrayAsync(requestUri, cancellationToken);
        }

        public HttpResponseMessage SendMessage(HttpRequestMessage request, CancellationToken token = default)
        {
            Track(request);
            return Inner.SendMessage(request, token);
        }

        public Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken token = default)
        {
            Track(request);
            return Inner.SendMessageAsync(request, token);
        }

        private void Track(HttpRequestMessage request)
        {
            var uri = request.RequestUri?.ToString();
            Extent? extent = request.ExtractRange();

            Track(uri, extent);
        }

        private void Track(string uri, Extent? range)
        {
            RequestsByPath.AddOrUpdate(uri, (k, a) => (1, ImmutableHashSet<Extent?>.Empty), (k, v, a) => (v.count + 1, v.set.Add(a)), range);
            Requests.AddOrUpdate((uri, range), 1, (k, v) => v + 1);
        }
    }
}