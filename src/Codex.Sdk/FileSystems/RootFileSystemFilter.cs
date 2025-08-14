using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;

namespace Codex.Utilities
{
    public class RootFileSystemFilter : FileSystemFilter
    {
        public readonly string[] IncludedRoots;

        public RootFileSystemFilter(params string[] includedRoots)
        {
            IncludedRoots = includedRoots.Select(i => i.EnsureTrailingSlash()).ToArray();
        }

        public override bool IncludeDirectory(FileSystem fileSystem, string directoryPath)
        {
            directoryPath = directoryPath.EnsureTrailingSlash();
            return IncludeFile(fileSystem, directoryPath);
        }

        public override bool IncludeFile(FileSystem fileSystem, string filePath)
        {
            foreach (var root in IncludedRoots)
            {
                if (filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
