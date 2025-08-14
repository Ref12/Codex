using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.ContractsLight;
using Codex.Utilities;
using Codex.Web.Common;
using Extent = Codex.Utilities.Extent;

namespace Codex.Lucene.Search
{
    public class FileSystemPageFileAccessor : IPageFileAccessor, IHttpClient
    {
        public string RootDirectory { get; }

        public Uri BaseAddress { get; }

        private IDictionary<FileSystemPageState, DebugState> OpenPages = new ConcurrentDictionary<FileSystemPageState, DebugState>(); 

        public FileSystemPageFileAccessor(string rootDirectory)
        {
            RootDirectory = rootDirectory;
            BaseAddress = new Uri(RootDirectory);
        }

        public IPageFileState CreateState(string path, PagingFileEntry entry)
        {
            var fullPath = GetFullPath(path);
            var pageState = new FileSystemPageState(this, fullPath, OpenStreamCore(fullPath, writable: false));
            if (Features.TrackOpenFiles)
            {
                OpenPages.Add(pageState, new DebugState(new StackTrace().ToString()));
            }

            return pageState;
        }

        public async Task<long> GetLengthAsync(string url)
        {
            var fullPath = GetFullPath(url);
            var info = new FileInfo(fullPath);
            return info.Length;
        }

        private string GetFullPath(string path)
        {
            return Path.Combine(RootDirectory, path);
        }

        public void DeleteError(string path)
        {
            var fullPath = GetFullPath(path);
            var openHandles = OpenPages.Where(e => e.Key.FullPath.EqualsIgnoreCase(fullPath)).ToArray();
        }

        private void DisposePageState(FileSystemPageState pageState)
        {
            OpenPages.Remove(pageState);
        }

        private record DebugState(string StackTrace);

        private static FileStream OpenStreamCore(string fullPath, bool writable = false)
        {
            return File.Open(fullPath,
                writable ? FileMode.OpenOrCreate : FileMode.Open, 
                writable ? FileAccess.ReadWrite : FileAccess.Read, 
                FileShare.Read);
        }

        public async Task<Stream> OpenStreamAsync(string path, Extent? range, bool writable = false)
        {
            return await OpenStreamCoreAsync(path, range, writable: writable, httpMode: false);   
        }

        public async ValueTask<Stream> OpenStreamCoreAsync(string path, Extent? range, bool writable = false,
            bool httpMode = false, bool async = true)
        {
            var fullPath = GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return Stream.Null;
            }
            else if (range?.Length == 0)
            {
                return SerializationUtilities.EmptyStream;
            }

            var stream = OpenStreamCore(fullPath, writable);
            if (httpMode)
            {
                range ??= new Extent(0, (int)stream.Length);
            }

            if (range != null)
            {
                stream.Position = range.Value.Start;
                var bytes = new byte[range.Value.Length];
                if (async)
                {
                    await stream.ReadAsync(bytes);
                }
                else
                {
                    stream.Read(bytes);
                }
                var memoryStream = new MemoryStream(bytes, 0, bytes.Length, false, publiclyVisible: true);
                stream.Dispose();
                return memoryStream;
            }

            return stream;
        }

        public HttpResponseMessage SendMessage(HttpRequestMessage request, CancellationToken token = default)
        {
            return SendMessageAsync(request, token).GetAwaiter().GetResult();
        }

        public async Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken token = default)
        {
            var range = request.ExtractRange();

            using var stream = await OpenStreamCoreAsync(
                request.RequestUri.OriginalString.AsSpan().SubstringFromFirstIndexOfAny("?").ToString(),
                range,
                writable: false,
                httpMode: true) as MemoryStream;

            if (stream == null)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ExposedByteArrayContent(stream.GetBuffer())
            };
        }

        public Task<ReadOnlyMemory<byte>> GetByteArrayAsync(StringUri? requestUri, CancellationToken cancellationToken = default)
        {
            return this.GetByteRangeAsync(requestUri?.AsString(), -1, -1);
        }

        private record FileSystemPageState(FileSystemPageFileAccessor Owner, string FullPath, FileStream Stream, bool IsClone = false) : IPageFileState
        {
            public void Dispose()
            {
                if (!IsClone)
                {
                    Stream.Dispose();
                    Owner.DisposePageState(this);
                }
            }

            IPageFileState IPageFileState.CreateClone()
            {
                return this with { IsClone = true };
            }

            public Lazy<PageFileSegment> GetSegment(long position, int length)
            {
                return new(() => GetSegmentCore(position, length));
            }

            public async ValueTask<PageFileSegment> GetSegmentAsync(long position, int length)
            {
                return GetSegmentCore(position, length);
            }

            private PageFileSegment GetSegmentCore(long position, int length)
            {
                lock (Stream)
                {
                    Stream.Position = position;

                    byte[] buffer = new byte[length];
                    Stream.Read(buffer, 0, buffer.Length);

                    return new PageFileSegment(position, buffer);
                }
            }
        }
    }
}
