using System.Diagnostics;
using System.Net;
using Codex.Sdk;

namespace Codex.Utilities
{
    using static PathUtilities;

    /// <summary>
    /// Utility methods for paths and Uris (including relativizing and derelativizing)
    /// </summary>
    public static partial class SdkPathUtilities
    {
        public record struct FileCopyEvent(string RelativePath, bool IsDelete, long? Size = null, TimeSpan? Elapsed = default);

        public record struct FileSystemSpec(FileSystem FS)
        {
            public static implicit operator FileSystemSpec(string path)
            {
                if (File.Exists(path))
                {
                    return new ZipFileSystem(path);
                }
                else
                {
                    return new DirectoryFileSystem(path);
                }
            }

            public static implicit operator FileSystemSpec(FileSystem fs) => new(fs);

            public override string ToString()
            {
                return FS.ToString();
            }
        }

        public static async Task CopyFilesRecursiveAsync(
            FileSystemSpec sourceDirectory,
            string targetDirectory,
            IEnumerable<string> deletedRelativeTargetFiles = null,
            int bufferSize = 1 << 20,
            Action<FileCopyEvent> handleFileCopy = null,
            Func<string, bool> shouldCopy = null,
            Action<string> logCopy = null,
            string maskingDirectory = null,
            bool deleteMissingTargetFiles = false)
        {
            var files = sourceDirectory.FS.GetFiles();

            ByteCount copiedBytes = 0, deletedBytes = 0, skippedBytes = 0;

            var deletedFilesSet = deletedRelativeTargetFiles.EmptyIfNull().Select(d => NormalizePath(d)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (deleteMissingTargetFiles)
            {
                var targetFiles = GetAllRelativeFilesRecursive(targetDirectory);

                // Add all target files to deletion set. Files which appear in source
                // will be removed.
                deletedFilesSet.UnionWith(targetFiles);
            }

            deletedFilesSet.ExceptWith(files);

            shouldCopy ??= _ => true;

            logCopy?.Invoke($"Applying '{sourceDirectory}' to '{targetDirectory}'{maskingDirectory?.FluidSelect(d => $" (masking '{d}')")}");

            handleFileCopy = handleFileCopy.ApplyBefore(fileEvent =>
            {
                if (fileEvent.Elapsed == null)
                {
                    var verb = fileEvent.IsDelete ? "Deleting" : "Applying";
                    logCopy?.Invoke($"{verb} '{fileEvent.RelativePath}' ...");
                }
                else
                {
                    var verb = fileEvent.IsDelete ? "Deleted" : "Applied";
                    logCopy?.Invoke($"{verb} '{fileEvent.RelativePath}' [Size:{fileEvent.Size}] (Elapsed: {fileEvent.Elapsed})");
                }
            });

            await Parallel.ForEachAsync(files.Concat(deletedFilesSet), async (file, token) =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                long? size = sourceDirectory.FS.FileExists(file) ? sourceDirectory.FS.GetFileSize(file) : null;

                if (!shouldCopy(file))
                {
                    skippedBytes.InterlockedAdd(size ?? 0);
                    logCopy?.Invoke($"Skipped '{file}'");
                    return;
                }

                bool isDelete = deletedFilesSet.Contains(file);

                handleFileCopy?.Invoke(new FileCopyEvent(file, isDelete));
                var baseFile = file;
                bool isAppend = false;
                if (file.EndsWith(AppendSuffix))
                {
                    isAppend = true;
                    baseFile = file.TrimEndIgnoreCase(AppendSuffix);
                }
                string targetPath = Path.Combine(targetDirectory, baseFile);

                if (deletedFilesSet.Contains(file))
                {
                    if (File.Exists(targetPath))
                    {
                        size = new FileInfo(targetPath).Length;
                        deletedBytes.InterlockedAdd(size ?? 0);
                        File.Delete(targetPath);
                    }
                }
                else
                {
                    bool isMasked = maskingDirectory != null && !File.Exists(Path.Combine(maskingDirectory, file));
                    if (isMasked)
                    {
                        skippedBytes.InterlockedAdd(size ?? 0);
                        logCopy?.Invoke($"Skipped '{file}' (masked)");
                        return;
                    }

                    using var source = sourceDirectory.FS.OpenFile(file);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                    using var target = File.Open(targetPath,
                        isAppend ? FileMode.Append : FileMode.Create,
                        FileAccess.Write,
                        FileShare.Delete);
                    size = source.Length;
                    copiedBytes.InterlockedAdd(size ?? 0);

                    if (source.Length > 0)
                    {
                        bufferSize = (int)Math.Min(bufferSize, source.Length);
                        await source.CopyToAsync(target, bufferSize: bufferSize, token);
                    }
                }

                handleFileCopy?.Invoke(new FileCopyEvent(file, isDelete, size, sw.Elapsed));
            });

            logCopy?.Invoke($"Applied '{sourceDirectory}' to '{targetDirectory}'{maskingDirectory?.FluidSelect(d => $" (masking '{d}')")} (copied: {copiedBytes}, skipped: {skippedBytes}, deleted: {deletedBytes})");
        }

    }
}