using System.Numerics;

namespace Codex.Utilities
{
    public class FileSystemFilter : IAdditionOperators<FileSystemFilter, FileSystemFilter, FileSystemFilter>
    {
        public virtual bool IncludeDirectory(FileSystem fileSystem, string directoryPath) => true;

        public virtual bool IncludeFile(FileSystem fileSystem, string filePath) => true;

        public static FileSystemFilter operator +(FileSystemFilter left, FileSystemFilter right)
        {
            return left.Combine(right);
        }
    }

    public class DelegateFileSystemFilter : FileSystemFilter
    {
        public Func<FileSystem, string, bool> ShouldIncludeDirectory;
        public Func<FileSystem, string, bool> ShouldIncludeFile;

        public override bool IncludeDirectory(FileSystem fileSystem, string directoryPath)
            => ShouldIncludeDirectory?.Invoke(fileSystem, directoryPath) ?? true;

        public override bool IncludeFile(FileSystem fileSystem, string filePath)
            => ShouldIncludeFile?.Invoke(fileSystem, filePath) ?? true;

    }

    public static class FileSystemFilterExtensions
    {
        public static FileSystemFilter Combine(this FileSystemFilter f1, FileSystemFilter f2)
        {
            if (f1 == null) return f2;
            else if (f2 == null) return f1;
            else
            {
                return new MultiFileSystemFilter(f1, f2);
            }
        }
    }
}
