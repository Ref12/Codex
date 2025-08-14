using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Codex.Analysis;
using Codex.Analysis.Files;
using Codex.Logging;
using Codex.Utilities;

namespace Codex.Import
{
    public class AnalysisServices
    {
        public Func<RepoProject, bool> IncludeRepoProject = (rp => true);
        public FileSystem FileSystem;
        public FileSystemFilter AnalysisIgnoreProjectFilter;
        public FileSystemFilter AnalysisIgnoreFileFilter;
        public Logger Logger = Logger.Null;
        public string TargetIndex { get; }
        public ICodexRepositoryStore RepositoryStore;

        public bool ParallelProcessProjectFiles
        {
            get => parallelProcessProjectFiles;
            set
            {
                TaskDispatcher.SetAllowedTaskType(TaskType.File, value);
                parallelProcessProjectFiles = value;
            }
        }

        private bool parallelProcessProjectFiles;

        public List<RepoFileAnalyzer> FileAnalyzers { get; set; } = new List<RepoFileAnalyzer>();
        public List<IAnalysisProcessor> Processors { get; set; } = new();

        public List<RepoProjectAnalyzer> ProjectAnalyzers { get; set; } = new List<RepoProjectAnalyzer>();
        public Dictionary<string, RepoFileAnalyzer> FileAnalyzerByExtension { get; set; } = new Dictionary<string, RepoFileAnalyzer>();
        public TaskDispatcher TaskDispatcher { get; set; } = new TaskDispatcher();
        public readonly List<NamedRoot> NamedRoots = new List<NamedRoot>();

        public ConcurrentDictionary<string, Repo> ReposByName = new ConcurrentDictionary<string, Repo>(StringComparer.OrdinalIgnoreCase);

        public AnalysisServices(string targetIndex, FileSystem fileSystem, RepoFileAnalyzer[] analyzers = null)
        {
            Debug.Assert(!string.IsNullOrEmpty(targetIndex));
            TargetIndex = targetIndex;
            FileSystem = fileSystem;
            AnalysisIgnoreProjectFilter = new GitIgnoreFilter(".cdxignore") {  Logger = Logger };
            AnalysisIgnoreFileFilter = AnalysisIgnoreProjectFilter;
            if (analyzers != null)
            {
                FileAnalyzers.AddRange(analyzers);

                foreach (var analyzer in FileAnalyzers)
                {
                    foreach (var extension in analyzer.SupportedExtensions)
                    {
                        FileAnalyzerByExtension[extension] = analyzer;
                    }
                }
            }

            // If default is changed, be sure to update the setters which might disable this unintentionally
            ParallelProcessProjectFiles = false;
        }

        public Repo CreateRepo(string name, string root = null)
        {
            Repo repo;
            bool added = false;

            if (!ReposByName.TryGetValue(name, out repo))
            {
                repo = ReposByName.GetOrAdd(name, k =>
                {
                    added = true;
                    return new Repo(name, root ?? $@"\\{name}\", this);
                });
            }

            return repo;
        }

        public virtual RepoFileAnalyzer GetDefaultAnalyzer(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            RepoFileAnalyzer fileAnalyzer;
            if (FileAnalyzerByExtension.TryGetValue(extension, out fileAnalyzer))
            {
                return fileAnalyzer;
            }

            return RepoFileAnalyzer.Default;
        }

        public string ReadAllText(string filePath, out SourceEncodingInfo encodingInfo, out int size)
        {
            using var stream = FileSystem.OpenFile(filePath);
            return SerializationUtilities.ReadAllText(stream, out encodingInfo, out size);
        }
    }

    public class TaskTracker
    {
        public bool IsTaskCompleted(string taskName) => false;

        public void CompleteTask(string taskName) { }
    }

    public enum TaskType
    {
        Project,
        File,
        Analysis,
        Upload,
    }
}
