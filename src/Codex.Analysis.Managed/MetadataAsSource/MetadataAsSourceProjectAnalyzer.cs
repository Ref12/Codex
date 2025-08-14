using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Projects;
using Codex.Import;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis.Managed
{
    public class MetadataAsSourceProjectAnalyzer : RepoProjectAnalyzer
    {
        public IEnumerable<string> assemblies;

        public ConcurrentDictionary<string, string> assemblyFileByMetadataAsSourceProjectPath
            = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public const string MetadataAsSourceRoot = @"\\MetadataAsSource\";

        public bool ScanAssemblies { get; set; } = false;

        public MetadataAsSourceProjectAnalyzer(IEnumerable<string> assemblies)
        {
            this.assemblies = assemblies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public bool IsAssemblyFilePath(string path)
        {
            return path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        public override bool IsCandidateProjectFile(RepoFile repoFile)
        {
            return ScanAssemblies && IsAssemblyFilePath(repoFile.FilePath);
        }
        //public FileSystem WrapFileSystem(FileSystem fs)
        //{
        //    return fs;
        //}

        public override void CreateProjects(RepoFile repoFile)
        {
            CreateProject(repoFile.PrimaryProject.Repo, repoFile.FilePath);
        }

        public override void CreateProjects(Repo repo)
        {
            repo.AddMount("MetadataAsSource", MetadataAsSourceRoot);

            foreach (var assembly in assemblies)
            {
                CreateProject(repo, assembly);
            }
        }

        public void CreateProject(Repo repo, string assembly)
        {
            var projectId = Path.GetFileNameWithoutExtension(assembly);
            var metadataAsSourceProjectDirectory = (MetadataAsSourceRoot + projectId).EnsureTrailingSlash();
            if (assemblyFileByMetadataAsSourceProjectPath.TryAdd(metadataAsSourceProjectDirectory, assembly))
            {
                var project = repo.CreateRepoProject(projectId, metadataAsSourceProjectDirectory);
                project.ProjectKind = ProjectKind.MetadataAsSource;
                project.Analyzer = this;
                project.IsInMemory = true;
            }
        }

        public override async Task Analyze(RepoProject project)
        {
            var assembly = assemblyFileByMetadataAsSourceProjectPath[project.ProjectDirectory];

            try
            {
                var services = project.Repo.AnalysisServices;
                var mas = new MetadataAsSource(assembly, services.Logger, services.FileSystem);
                var solution = await mas.LoadMetadataAsSourceSolution(project.ProjectDirectory);
                if (solution == null)
                {
                    return;
                }

                var proj = solution.Projects.Single();

                var projectInfo = ProjectInfo.Create(proj.Id, VersionStamp.Create(), project.ProjectId, project.ProjectId, LanguageNames.CSharp,
                    documents: proj.Documents.Select(d => DocumentInfo.Create(d.Id, d.Name, filePath: d.FilePath, folders: d.Folders)));

                SolutionProjectAnalyzer.AddSolutionProject(
                    new Lazy<Task<Microsoft.CodeAnalysis.Solution>>(() => Task.FromResult(solution)),
                    projectInfo,
                    project.ProjectFile,
                    project,
                    csharpSemanticServices: new Lazy<SemanticServices>(() => new SemanticServices(solution.Workspace, LanguageNames.CSharp)));

                if (project.Analyzer != this)
                {
                    await project.Analyzer.Analyze(project);
                }
            }
            catch
            {
            }
        }
    }
}
