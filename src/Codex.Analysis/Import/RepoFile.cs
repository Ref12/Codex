using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codex.Analysis;
using Codex.Analysis.Files;

namespace Codex.Import
{
    public class RepoFile : IRepoFile, IProjectFileScopeEntity
    {
        private string logicalPath;

        private string filePath;

        public string FilePath
        {
            get
            {
                if (filePath == null)
                {
                    throw new InvalidOperationException();
                }

                return filePath;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("FilePath");
                }

                filePath = value;
            }
        }

        public string RepoRelativePath
        {
            get
            {
                var repoRoot = PrimaryProject.Repo.DefaultRepoProject.ProjectDirectory;
                return PrimaryProject.Repo.DefaultRepoProject.GetLogicalPath(FilePath);
            }
        }

        public string LogicalPath
        {
            get
            {
                if (logicalPath == null)
                {
                    logicalPath = PrimaryProject.GetLogicalPath(FilePath);
                }

                return logicalPath;
            }

            set
            {
                logicalPath = value;
            }
        }

        public RepoProject PrimaryProject;
        public RepoFileAnalyzer Analyzer;
        public BoundSourceFileBuilder InMemorySourceFileBuilder;
        public string InMemoryContent;
        public bool Ignored { get; set; }
        public bool HasExplicitAnalyzer { get; set; }

        IRepo IRepoFile.Repo
        {
            get
            {
                return PrimaryProject.Repo;
            }
        }

        string IProjectFileScopeEntity.ProjectRelativePath => LogicalPath;

        string IProjectScopeEntity.ProjectId => PrimaryProject.ProjectId;

        string IRepoScopeEntity.RepositoryName => PrimaryProject.Repo.RepositoryName;

        private int m_analyzed = 0;

        public RepoFile(RepoProject project, string filePath, string logicalPath = null)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(filePath);
            }

            PrimaryProject = project;
            FilePath = filePath;
            this.logicalPath = logicalPath;
        }

        public override string ToString()
        {
            var list = new List<string>();

            list.Add(filePath ?? "FilePath:null");
            list.Add(logicalPath ?? "LogicalPath:null");
            list.Add(RepoRelativePath ?? "RepoRelativePath:null");

            return string.Join(";", list);
        }

        public void MarkAnalyzed()
        {
            m_analyzed = 1;
        }

        public Task Analyze()
        {
            if (Interlocked.Increment(ref m_analyzed) == 1)
            {
                var services = PrimaryProject.Repo.AnalysisServices;
                if (services.AnalysisIgnoreFileFilter.IncludeFile(services.FileSystem, FilePath))
                {
                    var fileAnalyzer = Analyzer ?? PrimaryProject.Repo.AnalysisServices.GetDefaultAnalyzer(FilePath);
                    return fileAnalyzer?.Analyze(this) ?? Task.CompletedTask;
                }
            }

            return Task.CompletedTask;
        }
    }
}
