using System.Net.Http.Headers;
using Codex.ObjectModel;

namespace Codex.Search
{
    public record SourceTextResult(string Content)
    { 
        public string SourceControlVersionId { get; init; }
        public HttpRequestMessage Request { get; init; }
        public HttpResponseMessage Response { get; init; }
    }

    public interface ISourceTextRetriever
    {
        ValueTask<SourceTextResult> GetSourceTextAsync(ISourceFileInfo fileInfo, SourceControlUri repoUri, Extent? range = null);
    }

    public record HttpClientSourceTextRetriever : ISourceTextRetriever
    {
        public HttpClient Client { get; } = new HttpClient();

        public async ValueTask<SourceTextResult> GetSourceTextAsync(
            ISourceFileInfo fileInfo, 
            SourceControlUri repoUri, 
            Extent? range = null)
        {
            try
            {
                if (fileInfo == null) return null;
                if (repoUri.Kind == SourceControlKind.GitHub)
                {
                    if (TryGetGitHubContentUrl(fileInfo, repoUri, out var contentUrl, requireTextRange: range != null))
                    {
                        if (range != null) 
                        {
                            var response = await GetStringAsync(contentUrl, req =>
                            {
                                AddGitHubInfo<string>(req);
                                req.Headers.Range = new RangeHeaderValue(
                                    from: range?.Start,
                                    to: range?.Length > 0 ? range?.Last : null);
                            });
                            return new SourceTextResult(response.content) { Request = response.request, Response = response.response };
                        }

                        var contentResult = await GetJsonObjectAsync<GitHubContentResult>(contentUrl, AddGitHubInfo<GitHubContentResult>);
                        if (contentResult == null) return null;

                        if (fileInfo is SourceFileInfo sourceFileInfo)
                        {
                            sourceFileInfo.SourceControlContentId ??= contentResult.Sha;
                            sourceFileInfo.DownloadAddress ??= contentResult.Download_Url;
                            sourceFileInfo.WebAddress ??= contentResult.Html_Url;
                        }

                        var base64Content = contentResult.Content.Replace("\n", "");

                        using var reader = new StreamReader(new MemoryStream(Convert.FromBase64String(base64Content)));
                        return new SourceTextResult(reader.ReadToEnd());
                    }
                }

                Placeholder.Todo("Implement for ADO");
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public bool TryGetGitHubContentUrl(ISourceFileInfo fileInfo, SourceControlUri repoUri, out string contentUrl, bool requireTextRange = false)
        {
            if (fileInfo.CommitId != null && fileInfo.RepoRelativePath != null)
            {
                if (requireTextRange)
                {
                    contentUrl = repoUri.GetContentByCommit(commit: fileInfo.CommitId, repoRelativePath: fileInfo.RepoRelativePath);
                }
                else
                {
                    contentUrl = repoUri.GetApiUrlByCommit(commit: fileInfo.CommitId, repoRelativePath: fileInfo.RepoRelativePath);
                }
                return true;
            }
            else if (fileInfo.SourceControlContentId != null)
            {
                contentUrl = repoUri.GetApiUrl(fileInfo.SourceControlContentId);
                return true;
            }

            contentUrl = null;
            return false;
        }

        private static void AddGitHubInfo<T>(HttpRequestMessage request)
        {
            if (typeof(T) == typeof(string))
            {
                request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/vnd.github.v3.raw"));
            }
            else
            {
                request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/vnd.github+json"));
            }

            Placeholder.Todo("Get raw bytes instead");

            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Codex", null));
        }

        protected virtual async ValueTask<T> GetJsonObjectAsync<T>(string url, Action<HttpRequestMessage> updateMessage)
        {
            var json = await GetStringAsync(url, updateMessage);
            return JsonSerializationUtilities.DeserializeEntity<T>(json.content.AsSpan().Trim());
        }

        protected virtual async ValueTask<(HttpRequestMessage request, HttpResponseMessage response, string content)> GetStringAsync(string url, Action<HttpRequestMessage> updateMessage)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            updateMessage?.Invoke(request);
            var result = await GetStringAsync(request);
            return (request, result.response, result.content);
        }

        protected virtual async ValueTask<(HttpResponseMessage response, string content)> GetStringAsync(HttpRequestMessage request)
        {
            Placeholder.Todo("Error handling");

            var response = await Client.SendAsync(request);
            return (response, await response.Content.ReadAsStringAsync());
        }

        public class GitHubContentResult
        {
            public string Sha { get; set; }

            public long Size { get; set; }

            public string Download_Url { get; set; }

            public string Html_Url { get; set; }

            public string Content { get; set; }
        }
    }
}
