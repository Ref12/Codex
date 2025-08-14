using System.ComponentModel;
using System.Runtime.Serialization;
using Codex.Storage;
using Codex.Utilities;
using Codex.Utilities.Zip;

namespace Codex.Lucene.Search
{
    public record struct PagingFileInfo(string RelativePath, long Length);

    public record PagingDirectoryInfo : IndexDirectoryInfo
    {
        public const string DirectoryInfoFileName = "dir.json";
        public const string DirectoryPrecacheIndexFileName = "dir.precache.json";
        public const string DirectoryPrecacheIndexPackFileName = "dir.precache.mpk";
        public const string DirectoryPrecacheFileName = "dir.precache.bin";

        public static PagingDirectoryInfo CreateFromFiles(string directory, string rootDirectory = null)
        {
            if (!Directory.Exists(directory))
            {
                return new PagingDirectoryInfo();
            }

            rootDirectory ??= directory;
            return CreateFromFiles(PathUtilities.GetAllRelativeFilesRecursive(directory, rootDirectory)
                .Select(relativePath => new PagingFileInfo(relativePath, new FileInfo(Path.Combine(rootDirectory, relativePath)).Length)));
        }

        public static PagingDirectoryInfo CreateFromFiles(IEnumerable<PagingFileInfo> filesWithLength)
        {
            return new PagingDirectoryInfo()
            {
                Entries =
                {
                    filesWithLength
                    .ToDictionary(t => t.RelativePath.Replace('\\', '/'), t => new PagingFileEntry() { Length = t.Length })
                }
            };
        }

        public Dictionary<string, PagingFileEntry> Entries { get; set; } = new Dictionary<string, PagingFileEntry>(StringComparer.OrdinalIgnoreCase);
    }

    [DataContract]
    public record PageCachingIndex
    {
        [DataMember(Order = 0)]
        public List<CachedSegmentEntry> CachedEntries { get; set; } = new List<CachedSegmentEntry>();
        [DataMember(Order = 1)]
        public byte[] Content { get; set; }
    }

    [DataContract]
    public class CachedSegmentEntry
    {
        [DataMember(Order = 0)]
        public string Path { get; set; }
        [DataMember(Order = 1)]
        public long StartPosition { get; set; }
        [DataMember(Order = 2)]
        public int Length { get; set; }
        [DataMember(Order = 3)]
        public string ContentType { get; set; }
    }

    public class PagingFileEntry
    {
        public long Length { get; init; }

        public string RealPath { get; set; }
    }
}
