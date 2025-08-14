namespace Codex.Utilities
{
    public class FileSystem : IDisposable
    {
        public FileSystem()
        {
        }

        public virtual bool FileExists(string filePath) => false;

        public virtual IEnumerable<string> GetFiles()
        {
            return new string[0];
        }

        public virtual IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            return new string[0];
        }

        public virtual Stream OpenFile(string filePath)
        {
            return Stream.Null;
        }

        public virtual Stream OpenFile(string filePath, OpenFileOptions options, out FileProperties properties)
        {
            properties = FileProperties.None;
            return OpenFile(filePath);
        }

        public virtual long GetFileSize(string filePath)
        {
            return 0;
        }

        public virtual void Dispose()
        {
        }
    }

    public enum OpenFileOptions
    {
        None,
    }

    public enum FileProperties
    {
        None,

        /// <summary>
        /// Indicates that the file was retrieved from git rather than disk
        /// </summary>
        FromGit = 1 << 0
    }

    public class SystemFileSystem : FileSystem
    {
        public static SystemFileSystem Instance { get; } = new();

        public override Stream OpenFile(string filePath)
        {
            return File.Open(PreparePath(filePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        public override long GetFileSize(string filePath)
        {
            return new FileInfo(PreparePath(filePath)).Length;
        }

        public override bool FileExists(string filePath)
        {
            return File.Exists(PreparePath(filePath));
        }

        protected virtual string PreparePath(string filePath)
        {
            return filePath;
        }
    }
}