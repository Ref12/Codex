using System.Text.Json;
using Codex.Sdk;
using Codex.Storage;
using Codex.Storage.BlockLevel;
using Codex.Utilities;
using Codex.Utilities.Zip;
using Codex.Web.Common;
using Lucene.Net.Store;

using IODirectory = System.IO.Directory;

namespace Codex.Lucene.Search
{
    public static class PagingHelpers
    {
        public static LuceneConfiguration CreatePagingConfiguration(string directory, bool store = false)
        {
            return CreatePagingConfigurationAsync(directory, c => c with { Store = store }).GetAwaiter().GetResult();
        }

        public static async Task<LuceneConfiguration> CreatePagingConfigurationAsync(
            string directory,
            Func<PagingConfiguration, PagingConfiguration> configure = null)
        {
            configure = configure.ApplyBefore(configuration =>
            {
                return configuration with
                {
                    Info = configuration.Info ?? new()
                };
            });

            var pagingDirectory = await CreatePagingDirectoryAsync(directory, configure, pagingConfiguration: new(out var pagingConfiguration));

            return LuceneConfiguration.CreateFromFactory(pagingDirectory).Apply(c =>
            {
                c.PageFileAccessor = pagingConfiguration.Value.Accessor.Value;
                c.ExternalRetrievalClient = pagingConfiguration.Value.ExternalRetrievalClient;
            });
        }

        public record PagingConfiguration(
            bool Store = false,
            bool Validating = false,
            bool LoadPrecache = false,
            AsyncOut<IPageFileAccessor> Accessor = null,
            AsyncOut<PagingDirectoryInfo> Info = null,
            int CacheLimit = 0,
            int? PageLimit = null,
            int PageSize = 4096,
            bool? Update = null,
            Func<IPageSegmentCache> CreateSegmentCache = null,
            string ValidatingDirectory = null,
            PageCachingIndex PageCachingIndex = null,
            IExternalRetrievalClient ExternalRetrievalClient = null,
            AsyncOut<CachingPageFileProvider> CachingProvider = null,
            RefAction<IHttpClient> UpdateClient = null)
        {
            public Func<HttpClientKind, IInnerHttpClient> GetClient { get; set; } = SdkFeatures.GetClient;
            public bool ShouldUpdate() => Update ?? Store;
        }

        public static async Task<Func<string, IndexDirectory>> CreatePagingDirectoryAsync(
            string directoryOrUrl,
            Func<PagingConfiguration, PagingConfiguration> configure = null,
            AsyncOut<PagingConfiguration> pagingConfiguration = null)
        {
            var configuration = new PagingConfiguration();
            configuration = configure?.Invoke(configuration) ?? configuration;
            directoryOrUrl = directoryOrUrl.Replace('\\', '/');

            configuration = configuration with
            {
                Info = configuration.Info ?? new(),
                Accessor = configuration.Accessor ?? new()// new FileSystemPageFileAccessor(directory)
            };

            IHttpClient client = getClient(directoryOrUrl, HttpClientKind.Index);

            // TODO: Should this be called on call clients
            configuration.UpdateClient?.Invoke(new(ref client));

            IBytesRetriever retriever = client;
            configuration.Accessor.Set(new HttpPageFileAccessor(
                indexRootUrl: "",
                retriever));

            var info = await ReadInfoAsync("", configuration.Accessor.Value);

            IHttpClient getClient(string directoryOrUrl, HttpClientKind kind)
            {
                bool fileMode = false;
                var client = Out.Invoke<IHttpClient>(() =>
                {
                    if (PathUtilities.ToUriOrPath(directoryOrUrl, out var uri, out var directory))
                    {
                        fileMode = false;
                        var client = configuration.GetClient?.Invoke(kind) ?? new HttpClientWrapper();
                        client.BaseAddress = GetAddress(uri);
                        return client;
                    }
                    else
                    {
                        fileMode = true;
                        return new FileSystemPageFileAccessor(directory);
                    }
                });

                return new QueryAugmentingHttpClientWrapper(client, fileMode);
            }

            configuration.Info.Set(info);

            pagingConfiguration?.Set(configuration);

            Placeholder.Trace();
            var provider = new CachingPageFileProvider(configuration.Accessor.Value, cacheLimit: configuration.CacheLimit, pageSize: configuration.PageSize);

            configuration.CachingProvider?.Set(provider);
            provider.SegmentPreCache = configuration.CreateSegmentCache?.Invoke() ?? provider.SegmentPreCache;
            if (provider.SegmentPreCache is SegmentCacheMap cacheMap)
            {
                cacheMap.Owner = provider;
            }

            var precacheFile = PathUtilities.UriCombine(directoryOrUrl, PagingDirectoryInfo.DirectoryPrecacheFileName);
            var precacheIndex = GetPrecacheIndexFilePath(directoryOrUrl);
            PageCachingIndex cachingIndex = configuration.PageCachingIndex;
            if (configuration.LoadPrecache &&
                (await ReadAsync<PageCachingIndex>(precacheIndex, configuration.Accessor.Value)) is PageCachingIndex cachingInfo)
            {
                cachingInfo.Content = await configuration.Accessor.Value.OpenStreamAsync(precacheFile).SelectAsync(s => s.ReadAllBytes(dispose: true));
                cachingIndex ??= cachingInfo;
            }

            if (cachingIndex != null)
            {
                provider.LoadPrecache(cachingIndex.CachedEntries, cachingIndex.Content);
            }

            return name =>
            {
                if (configuration.PageLimit != null)
                {
                    provider.MaxPageRetrievalCount = configuration.PageLimit.Value;
                }

                var pagingDirectory = new ScopedDirectory(new PagingDirectory(info, provider), name);
                return configuration.Validating
                    ? new ValidatingDirectory(pagingDirectory, new SimpleFSDirectory(PathUtilities.UriCombine(configuration.ValidatingDirectory ?? directoryOrUrl, name)))
                    : pagingDirectory;
            };
        }

        private static Url GetAddress(Uri uri)
        {
            Url result = uri.EnsureTrailingSlash();
            return result;
        }

        public static async Task<PagingDirectoryInfo> CreateFromInfoFromDirectory(string directory, PagingConfiguration configuration)
        {
            var info = await ReadInfoAsync(directory, configuration.Accessor.Value);

            if (configuration.ShouldUpdate())
            {
                info = info with
                {
                    Entries = PagingDirectoryInfo.CreateFromFiles(CodexConstants.GetIndicesDirectory(directory), directory).Entries
                };
            }

            if (configuration.Store)
            {
                StoreInfo(directory, info);
            }

            return info;
        }

        public static async Task<PagingDirectoryInfo> ReadInfoAsync(string directory, IPageFileAccessor accessor)
        {
            string path = GetDirectoryInfoFilePath(directory);
            return (await ReadAsync<PagingDirectoryInfo>(path, accessor)) ?? new PagingDirectoryInfo();
        }

        public static async Task<T> ReadAsync<T>(string path, IPageFileAccessor accessor)
        {
            var text = await accessor.OpenStreamAsync(path).SelectAsync(s => s.ReadAllText());
            if (string.IsNullOrEmpty(text))
            {
                return default;
            }

            return JsonSerializationUtilities.DeserializeEntity<T>(text);
        }

        public static void CopySanitizedPagingDirectory(string source, string target, bool cleanTarget, bool useCasLayout = false)
        {
            var readText = File.ReadAllText(GetDirectoryInfoFilePath(source));
            var info = JsonSerializationUtilities.DeserializeEntity<PagingDirectoryInfo>(readText);

            if (cleanTarget && IODirectory.Exists(target))
            {
                IODirectory.Delete(target, true);
            }

            var hashMap = new Dictionary<string, (PagingFileEntry entry, bool shared)>();

            foreach (var (relativePath, entry) in info.Entries.ToList())
            {
                // Clean out top level files and skip copying
                if (!relativePath.Contains('/'))
                {
                    info.Entries.Remove(relativePath);
                    continue;
                }

                var sourcePath = Path.Combine(source, relativePath);

                var fileName = Path.GetFileName(sourcePath);
                var relativeFolder = relativePath.Substring(0, relativePath.Length - fileName.Length);
                if (useCasLayout)
                {
                    using var fs = File.OpenRead(sourcePath);
                    var hasher = new Murmur3();
                    var hash = hasher.ComputeHash(fs.StreamSegments());
                    var hashString = hash.ToString();
                    var extension = Path.GetExtension(fileName);
                    if (hashMap.TryGetValue(hashString, out var otherMapping))
                    {
                        if (otherMapping.shared)
                        {
                            entry.RealPath = otherMapping.entry.RealPath;
                            continue;
                        }
                        else
                        {
                            entry.RealPath = $"commoncas/{hashString}{extension}";
                            otherMapping.entry.RealPath = entry.RealPath;
                            hashMap[hashString] = (entry, shared: true);
                        }
                    }
                    else
                    {
                        entry.RealPath = $"{relativeFolder}{hashString}{extension}";
                        hashMap[hashString] = (entry, shared: false);
                    }
                }
                else
                {
                    if (!char.IsAsciiLetterOrDigit(fileName[0]))
                    {
                        entry.RealPath = relativeFolder + "a" + fileName;
                    }
                }

                var targetPath = Path.Combine(target, entry.RealPath ?? relativePath);
                IODirectory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(Path.Combine(source, relativePath), targetPath, overwrite: true);
            }

            foreach (var (relativePath, entry) in info.Entries.ToList())
            {
                var sourcePath = Path.Combine(source, relativePath);
                var targetPath = Path.Combine(target, entry.RealPath ?? relativePath);
                IODirectory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(sourcePath, targetPath, overwrite: true);
            }

            // Copy top level files
            foreach (var file in IODirectory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            }

            StoreInfo(target, info);
        }

        public static void StoreInfo(string directory, PagingDirectoryInfo info)
        {
            File.WriteAllText(GetDirectoryInfoFilePath(directory), JsonSerializationUtilities.SerializeEntity(info, flags: JsonFlags.Indented));
        }

        public static string GetDirectoryInfoFilePath(string directory)
        {
            return Path.Combine(directory, PagingDirectoryInfo.DirectoryInfoFileName);
        }

        private static string GetPrecacheIndexFilePath(string directory)
        {
            return Path.Combine(directory, PagingDirectoryInfo.DirectoryPrecacheIndexFileName);
        }
    }
}