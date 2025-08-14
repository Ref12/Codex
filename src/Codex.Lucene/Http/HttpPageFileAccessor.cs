using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Codex.Lucene.Search;
using Codex.ObjectModel.Implementation;
using Codex.Utilities;
using DotNext.IO;
using Lucene.Net.Codecs;
namespace Codex.Web.Common
{
    using static Placeholder;

    public record HttpPageFileAccessor : IPageFileAccessor
    {
        private string AppendSuffix { get; }

        public string IndexRootUrl { get; init; }

        public IBytesRetriever Client { get; }

        public string Suffix { get; init; }

        public HttpPageFileAccessor(string indexRootUrl, IBytesRetriever client, string suffix = null)
        {
            IndexRootUrl = indexRootUrl;
            Client = client;
            Suffix = suffix;

            var queryIndex = IndexRootUrl.IndexOf("?");
            if (queryIndex >= 0 && string.IsNullOrEmpty(suffix))
            {
                Suffix = IndexRootUrl.Substring(queryIndex);
                IndexRootUrl = IndexRootUrl.Substring(0, queryIndex);
            }

            AppendSuffix = Suffix?.StartsWith('?') == true ? $"&{Suffix.AsSpan().Slice(1)}" : Suffix;
        }

        public IPageFileState CreateState(string path, PagingFileEntry entry)
        {
            DebugLog($"Creating page file: Length={entry.Length} Path={path}");
            path = GetPath(path);
            return new HttpPageFileState(this, path, entry);
        }

        public async Task<Stream> OpenStreamAsync(string path, Codex.Utilities.Extent? range = default, bool writable = false)
        {
            if (writable)
            {
                throw new NotSupportedException($"{nameof(HttpPageFileAccessor)}: Cannot open writable stream for path {path}");
            }

            path = GetPath(path);
            try
            {
                var bytes = await Client.GetBytesAsync(path, range?.ToLong());
                return bytes.AsStream();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Stream.Null;
            }
        }

        private string GetPath(string path)
        {
            path = PathUtilities.UriCombine(IndexRootUrl, path.Replace('\\', '/')) + (path.Contains('?') ? AppendSuffix : Suffix);
            return path;
        }

        public record HttpPageFileState(HttpPageFileAccessor Accessor, string Path, PagingFileEntry Entry) : IPageFileState
        {
            public long Length => Entry.Length;

            public void Dispose()
            {
            }

            public Lazy<PageFileSegment> GetSegment(long position, int length)
            {
                return new Lazy<PageFileSegment>(() =>
                {
                    try
                    {
                        var bytes = Accessor.Client.GetBytes(Path, new LongExtent(position, length));
                        return new PageFileSegment(position, bytes);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        throw new OutOfMemoryException($"OOM [{Path}:{Entry.Length}]({position}, {length})", ex);
                    }
                });
            }

            public async ValueTask<PageFileSegment> GetSegmentAsync(long position, int length)
            {
                var bytes = await Accessor.Client.GetBytesAsync(Path, new LongExtent(position, Length));
                return new PageFileSegment(position, bytes);
            }
        }
    }
}
