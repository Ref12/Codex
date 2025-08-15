using System;
using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Web;
using Codex.Sdk;
using Codex.Utilities;

namespace Codex.Web.Common;

public interface IHttpClient : IBytesRetriever
{
    Uri BaseAddress { get; }

    HttpResponseMessage SendMessage(HttpRequestMessage request, CancellationToken token = default);

    Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken token = default);

    ReadOnlyMemory<byte> IBytesRetriever.GetBytes(string url, LongExtent extent)
    {
        return this.GetByteRange(url, extent.Start, extent.Length);
    }

    Task<ReadOnlyMemory<byte>> IBytesRetriever.GetBytesAsync(string url, LongExtent? extent = null)
    {
        if (extent == null)
        {
            return this.GetByteArrayAsync(url);
        }
        else
        {
            return this.GetByteRangeAsync(url, extent.Value.Start, extent.Value.Length);
        }
    }

    async Task<long> IBytesRetriever.GetLengthAsync(string url)
    {
        var response = await this.SendMessageAsync(new HttpRequestMessage(HttpMethod.Head, url));

        return response.Content.Headers.ContentLength.Value;
    }
}

public interface IInnerHttpClient : IHttpClient
{
    new Uri BaseAddress { get; set; }
}

public record QueryAugmentingHttpClientWrapper(IHttpClient Inner, bool FileMode = false, Action<HttpResponseMessage> OnResponse = null) : IHttpClient
{
    public Uri BaseAddress { get; } = Inner.BaseAddress?.EnsureTrailingSlash();

    public HttpResponseMessage SendMessage(HttpRequestMessage request, CancellationToken token = default)
    {
        request.RequestUri = GetAugmentedUri(request.RequestUri);
        return HandleResponse(Inner.SendMessage(request, token));
    }

    public async Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        request.RequestUri = GetAugmentedUri(request.RequestUri);
        return HandleResponse(await Inner.SendMessageAsync(request, token));
    }

    private HttpResponseMessage HandleResponse(HttpResponseMessage response)
    {
        OnResponse?.Invoke(response);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private Uri? GetAugmentedUri(Uri? uri)
    {
        if (FileMode && uri?.OriginalString.Contains('?') == true)
        {
            uri = new Uri(uri.OriginalString.Replace('?', '&'), UriKind.RelativeOrAbsolute);
        }

        // Only need to modify uri if both contain query strings
        if (string.IsNullOrEmpty(BaseAddress?.Query) || uri == null) return uri;

        var fullUri = new Uri(BaseAddress, uri);
        var builder = new UriBuilder(fullUri);

        builder.Query = string.IsNullOrEmpty(builder.Query)
            ? BaseAddress.Query
            : $"{builder.Query}&{BaseAddress.Query.AsSpan().TrimStart('?')}";

        return builder.Uri;
    }
}

public enum HttpClientKind
{
    Index,
    Entity
}

public record HttpClientWrapper(HttpClient Inner, HttpMessageHandler Handler = null) : IHttpClient, IInnerHttpClient
{
    public virtual HttpClient Inner { get; } = Inner;

    public HttpClientWrapper() : this(new HttpClient()) { }

    public HttpClientWrapper(HttpMessageHandler handler) : this(new HttpClient(handler), handler) { }

    public virtual Uri BaseAddress { get => Inner.BaseAddress; set => Inner.BaseAddress = value; }

    public virtual Task<byte[]> GetByteArrayAsync(StringUri? requestUri, CancellationToken cancellationToken = default)
    {
        return Inner.GetByteArrayAsync(requestUri, cancellationToken);
    }

    public virtual HttpResponseMessage SendMessage(HttpRequestMessage request, CancellationToken token = default)
    {
        return Inner.Send(request, token);
    }

    public virtual Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken token = default)
    {
        return Inner.SendAsync(request, token);
    }
}

public interface IExposedByteArrayContent
{
    public byte[] Bytes { get; }
}

public class ExposedByteArrayContent : ByteArrayContent, IExposedByteArrayContent
{
    public byte[] Bytes { get; }

    public ExposedByteArrayContent(byte[] bytes)
        : base(bytes)
    {
        Bytes = bytes;
    }
}

public static class HttpEx
{
    public static NameValueCollection NewQueryBuilder() => HttpUtility.ParseQueryString("");

    public static NameValueCollection ParseQuery(string s) => HttpUtility.ParseQueryString(s);

    public static bool TryGetNonEmptyValue(this NameValueCollection query, string name, out string value)
    {
        return !string.IsNullOrEmpty(Out.Var(out value, query[name]));
    }
}

public static class HttpClientExtensions
{
    public static async Task<string> GetStringAsync(
        this IHttpClient client,
        string url,
        bool defaultOnFailure = false)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await client.SendMessageAsync(message);
        if (defaultOnFailure && !response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public static Task<ReadOnlyMemory<byte>> GetByteArrayAsync(this IHttpClient client, StringUri? requestUri, CancellationToken cancellationToken = default)
    {
        return client.GetByteRangeAsync(requestUri?.AsString(), -1, -1);
    }

    public static async Task<ReadOnlyMemory<byte>> GetByteRangeAsync(
        this IHttpClient client,
        string url,
        long position,
        long count,
        CancellationToken token = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, url);
        if (position >= 0 && count >= 0)
        {
            message.Headers.Range = new RangeHeaderValue(position, position + count - 1);
        }

        var response = await client.SendMessageAsync(message, token).ConfigureAwait(false);

        Placeholder.DebugLog("Got response");

        if (response.Content is IExposedByteArrayContent content)
        {
            return content.Bytes;
        }

        return await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
    }

    public static Extent? ExtractRange(this HttpRequestMessage request)
    {
        var range = request.Headers.Range;
        Extent? extent = null;
        if (range != null)
        {
            var firstRange = range.Ranges.First();
            extent = Extent.FromBounds((int)firstRange.From, (int)firstRange.To + 1);
        }

        return extent;
    }

    public static byte[] GetByteRange(
        this IHttpClient client,
        string url,
        long position,
        long count)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Range = new RangeHeaderValue(position, position + count);
        var response = client.SendMessage(message);

        Placeholder.DebugLog("Got response");

        if (response.Content is IExposedByteArrayContent content)
        {
            return content.Bytes;
        }

        return response.Content.ReadAsStream().ReadAllBytes();
    }
}