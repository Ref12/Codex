using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.ObjectModel
{
    /// <summary>
    /// Information for creating an ICodexRepositoryStore
    /// </summary>
    public interface IRepositoryStoreInfo
    {
        /// <summary>
        /// The repository being stored
        /// </summary>
        IRepository Repository { get; }

        /// <summary>
        /// The branch being stored
        /// </summary>
        IBranch Branch { get; }

        /// <summary>
        /// The commit being stored
        /// </summary>
        ICommit Commit { get; }
    }

    public enum DirectoryStoreFormat
    { 
        Json,
        Block,
        BlockWithIndex
    }

    public record AnalysisExportSettings(string Directory)
    {
        public string IndexDirectory { get; } = Path.Combine(Directory, "index");

        public string BlocksDirectory { get; } = Path.Combine(Directory, "blocks");

        public string FiltersDirectory { get; } = Path.Combine(Directory, "filters");
    }

    public static class DirectoryStoreFormatExtensions
    {
        public static bool ShouldWriteBlocks(this DirectoryStoreFormat format) => format == DirectoryStoreFormat.Block || format == DirectoryStoreFormat.BlockWithIndex;
    }

    public interface IDirectoryRepositoryStoreInfo : IRepositoryStoreInfo
    {
        public DirectoryStoreFormat Format { get; }
    }
}
