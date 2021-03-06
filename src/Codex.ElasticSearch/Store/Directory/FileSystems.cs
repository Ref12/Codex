﻿using Codex.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public class DirectoryFileSystem : SystemFileSystem
    {
        public readonly string RootDirectory;
        private readonly string SearchPattern;

        public DirectoryFileSystem(string rootDirectory, string searchPattern = "*.*")
        {
            RootDirectory = rootDirectory;
            SearchPattern = searchPattern;
        }

        public override IEnumerable<string> GetFiles()
        {
            return Directory.GetFiles(RootDirectory, SearchPattern, SearchOption.AllDirectories);
        }

        public override IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            var path = Path.Combine(RootDirectory, relativeDirectoryPath);
            if (Directory.Exists(path))
            {
                return Directory.GetFiles(path, SearchPattern, SearchOption.AllDirectories);
            }
            else
            {
                return new string[0];
            }
        }

        public override Stream OpenFile(string filePath)
        {
            filePath = Path.Combine(RootDirectory, filePath);
            return base.OpenFile(filePath);
        }
    }

    public class FlattenDirectoryFileSystem : SystemFileSystem
    {
        public readonly string RootDirectory;
        private readonly string SearchPattern;

        public FlattenDirectoryFileSystem(string rootDirectory, string searchPattern = "*.*")
        {
            RootDirectory = rootDirectory;
            SearchPattern = searchPattern;
        }

        public override IEnumerable<string> GetFiles()
        {
            return Directory.GetFiles(RootDirectory, SearchPattern, SearchOption.AllDirectories);
        }

        public override IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            List<string> files = new List<string>();

            foreach (var subDirectory in Directory.GetDirectories(RootDirectory))
            {
                var path = Path.Combine(subDirectory, relativeDirectoryPath);
                if (Directory.Exists(path))
                {
                    files.AddRange(Directory.GetFiles(path, SearchPattern, SearchOption.AllDirectories));
                }
            }

            return files;
        }

        public override Stream OpenFile(string filePath)
        {
            filePath = Path.Combine(RootDirectory, filePath);
            return base.OpenFile(filePath);
        }
    }

    public class ZipFileSystem : FileSystem
    {
        public readonly string ArchivePath;
        private ZipArchive zipArchive;

        public ZipFileSystem(string archivePath)
        {
            ArchivePath = archivePath;
            zipArchive = new ZipArchive(File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read, leaveOpen: false);
            
            // Ensure entries are read
            var entries = zipArchive.Entries;
        }

        public override Stream OpenFile(string filePath)
        {
            lock (zipArchive)
            {
                MemoryStream memoryStream = new MemoryStream();

                using (var entryStream = zipArchive.GetEntry(filePath).Open())
                {
                    entryStream.CopyTo(memoryStream);
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
        }

        public override long GetFileSize(string filePath)
        {
            return zipArchive.GetEntry(filePath).Length;
        }

        public override IEnumerable<string> GetFiles()
        {
            return zipArchive.Entries.Where(e => e.Length != 0).Select(e => e.FullName).ToArray();
        }

        public override IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            relativeDirectoryPath = PathUtilities.EnsureTrailingSlash(relativeDirectoryPath, '\\');
            var files = GetFiles();
            return files.Where(n => n.Replace('/', '\\').StartsWith(relativeDirectoryPath));
        }

        public override void Dispose()
        {
            zipArchive.Dispose();
            zipArchive = null;
        }
    }
}