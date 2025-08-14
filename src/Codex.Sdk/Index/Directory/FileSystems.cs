using Codex.Utilities;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public class DirectoryFileSystem : SystemFileSystem
    {
        public readonly string RootDirectory;
        protected readonly string SearchPattern;

        public DirectoryFileSystem(string rootDirectory, string searchPattern = "*")
        {
            RootDirectory = rootDirectory;
            SearchPattern = searchPattern;
        }

        public override IEnumerable<string> GetFiles()
        {
            return GetFiles("");
        }

        public override IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            var path = Path.Combine(RootDirectory, relativeDirectoryPath);
            if (Directory.Exists(path))
            {
                return PathUtilities.GetAllRelativeFilesRecursive(directory: path, rootDirectory: RootDirectory);
            }
            else
            {
                return [];
            }
        }

        protected override string PreparePath(string filePath)
        {
            filePath = Path.Combine(RootDirectory, filePath);
            return base.PreparePath(filePath);
        }
    }

    public class FlattenDirectoryFileSystem : DirectoryFileSystem
    {
        public FlattenDirectoryFileSystem(string rootDirectory, string searchPattern = "*")
            : base(rootDirectory, searchPattern)
        {
        }

        public override IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            List<string> files = new List<string>();

            foreach (var subDirectory in Directory.GetDirectories(RootDirectory))
            {
                files.AddRange(GetFiles(subDirectory));
            }

            return files;
        }
    }

    public class ZipFileSystem : FileSystem
    {
        public readonly string ArchivePath;
        private ZipFile zipArchive;

        public ZipFileSystem(string archivePath, string password = null, string privateKey = null)
        {
            ArchivePath = archivePath;
            zipArchive = new ZipFile(File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read), leaveOpen: false);
            zipArchive.Password = MiscUtilities.TryGetZipPassword(zipArchive, privateKey) ?? password;
            // Ensure entries are read
            var entries = GetEntries();
        }

        public IEnumerable<ZipEntry> GetEntries()
        {
            for (int i = 0; i < zipArchive.Count; i++)
            {
                yield return zipArchive[i];
            }
        }

        public override Stream OpenFile(string filePath)
        {
            lock (zipArchive)
            {
                MemoryStream memoryStream = new MemoryStream();

                using (var entryStream = zipArchive.GetInputStream(zipArchive.GetEntry(filePath)))
                {
                    entryStream.CopyTo(memoryStream);
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
        }

        public override bool FileExists(string filePath)
        {
            return zipArchive.GetEntry(filePath) != null;
        }

        public override long GetFileSize(string filePath)
        {
            return zipArchive.GetEntry(filePath).Size;
        }

        public override IEnumerable<string> GetFiles()
        {
            return GetEntries().Where(e => e.Size != 0).Select(e => e.Name).ToArray();
        }

        public override IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            relativeDirectoryPath = PathUtilities.EnsureTrailingSlash(relativeDirectoryPath, '\\');
            var files = GetFiles();
            return files.Where(n => n.Replace('/', '\\').StartsWith(relativeDirectoryPath));
        }

        public override void Dispose()
        {
            zipArchive.Close();
            zipArchive = null;
        }
    }
}