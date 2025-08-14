using System;
using System.Text;
using Codex.ObjectModel;
using Codex.Utilities;

namespace Codex.Analysis
{
    public interface IRepo
    {
        string RepositoryName { get; }

        string TargetIndex { get; }
    }

    public interface IRepoProject
    {
        ProjectKind ProjectKind { get; }
        IRepo Repo { get; }
    }

    public interface IRepoFile
    {
        IRepo Repo { get; }

        /// <summary>
        /// Repo relative path. May be null if file not under repo root.
        /// </summary>
        string RepoRelativePath { get; }
    }

    public interface IAnalysisProcessor
    {
        void BeforeBuild(BoundSourceFileBuilder file) { }
    }
}
