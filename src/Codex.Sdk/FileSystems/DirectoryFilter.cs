namespace Codex.Utilities
{
    public class DirectoryFileSystemFilter : FileSystemFilter
    {
        public readonly IReadOnlyList<string> ExcludedSegments;

        public bool ExcludingRoots { get; set; }

        public DirectoryFileSystemFilter(params string[] excludedSegments)
            : this((IReadOnlyList<string>)excludedSegments)
        {
        }

        public DirectoryFileSystemFilter(IReadOnlyList<string> excludedSegments)
        {
            ExcludedSegments = excludedSegments;
        }

        public override bool IncludeFile(FileSystem fileSystem, string filePath)
        {
            if (ExcludingRoots)
            {
                return !Exclude(filePath);
            }

            return base.IncludeFile(fileSystem, filePath);
        }

        private bool Exclude(string path)
        {
            foreach (var excludedSegment in ExcludedSegments)
            {
                var index = path.IndexOf(excludedSegment, StringComparison.OrdinalIgnoreCase);
                if (ExcludingRoots ? index == 0 : index >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public override bool IncludeDirectory(FileSystem fileSystem, string directoryPath)
        {
            directoryPath = directoryPath.EnsureTrailingSlash();

            if (Exclude(directoryPath) || (new DirectoryInfo(directoryPath).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                return false;
            }

            return true;
        }
    }
}
