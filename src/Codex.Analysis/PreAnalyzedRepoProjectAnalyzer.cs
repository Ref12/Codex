using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Files;
using Codex.Import;
using Codex.ObjectModel;
using Codex.Storage.Store;

namespace Codex.Analysis
{
    public class PreAnalyzedRepoProjectAnalyzer : RepoProjectAnalyzer
    {
        private ConcurrentDictionary<string, (IStoredAnalyzedProject StoredProject, AnalyzedProjectInfo Info)> projectsById = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> projectDataDirectories;
        private List<IAnalyzedProjectProvider> ProjectProviders { get; } = new();

        public PreAnalyzedRepoProjectAnalyzer(IList<string> projectDataDirectories)
        {
            this.projectDataDirectories = projectDataDirectories.Select(p => Path.GetFullPath(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public override void CreateProjects(Repo repo)
        {
            var interceptStore = new RepositoryStore(this, repo.AnalysisServices.RepositoryStore);

            var logger = repo.AnalysisServices.Logger;
            foreach (var projectDataDir in projectDataDirectories)
            {
                logger.LogMessage($"Opening project data directory: '{projectDataDir}'");
                var store = new DirectoryCodexStore(projectDataDir, logger);
                ProjectProviders.Add(store.GetProjectProvider());
            }

            foreach (var provider in ProjectProviders)
            {
                var projects = provider.GetProjects();
                foreach (var project in projects)
                {
                    var loadedProject = project.Load();

                    if (projectsById.TryGetValue(loadedProject.ProjectId, out var existingProject))
                    {
                        if (!IsCandidateBetter(existingProject.Info, candidate: loadedProject))
                        {
                            // existingProject is better than loaded project. Just continue with existing project
                            continue;
                        }
                    }

                    logger.LogMessage($"Assigning project data: {loadedProject.ProjectId}= (TargetFx: {loadedProject.TargetFramework?.Identifier}) {project.Key}");
                    projectsById[loadedProject.ProjectId] = (project, loadedProject);
                }
            }

            foreach ((var stored, var project) in projectsById.Values)
            {
                if (project.ProjectId == repo.DefaultRepoProject.ProjectId)
                {
                    continue;
                }

                var projectFile = GetProjectFile(repo, project);

                var repoProject = repo.CreateRepoProject(
                    project.ProjectId,
                    GetProjectDirectory(repo, project),
                    projectFile);

                interceptStore.ActiveProject = repoProject;
                repoProject.Analyzer = this;

                foreach (var fileRef in stored.GetFiles())
                {
                    fileRef.AddToAsync(interceptStore);
                }

                // Clear files list since it will be recomputed
                project.Files.Clear();

                repoProject.InitializeProjectContext(project);
            }

            foreach (var provider in ProjectProviders)
            {
                provider.Dispose();
            }
        }

        protected override Task FinalizeProject(RepoProject project)
        {
            // No need for project finalization since we add a preanalyzed project
            return Task.CompletedTask;
        }

        private bool IsCandidateBetter(AnalyzedProjectInfo existingProject, AnalyzedProjectInfo candidate)
        {
            return (candidate.TargetFramework?.Priority ?? 0) > (existingProject.TargetFramework?.Priority ?? 0);
        }

        private static string GetRepoPath(Repo repo, string repoRelativePath)
        {
            return Path.Combine(repo.DefaultRepoProject.ProjectDirectory, repoRelativePath);
        }

        private RepoFile GetProjectFile(Repo repo, AnalyzedProjectInfo project)
        {
            if (project.PrimaryFile == null)
            {
                return null;
            }

            return repo.DefaultRepoProject.AddFile(GetRepoPath(repo, project.PrimaryFile.RepoRelativePath));
        }

        private string GetProjectDirectory(Repo repo, AnalyzedProjectInfo project)
        {
            if (project.PrimaryFile == null)
            {
                // TODO: Is this ok?
                return $@"\\Projects\{project.ProjectId}";
            }

            return Path.Combine(repo.DefaultRepoProject.ProjectDirectory, Path.GetDirectoryName(project.PrimaryFile.RepoRelativePath));
        }

        public ICodexRepositoryStore CreateRepositoryStore(ICodexRepositoryStore innerStore)
        {
            return new RepositoryStore(this, innerStore);
        }

        private record RepositoryStore(PreAnalyzedRepoProjectAnalyzer Analyzer, ICodexRepositoryStore InnerStore) 
            : WrapperCodexRepositoryStore(InnerStore)
        {
            public RepoProject ActiveProject { get; set; }

            public override Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
            {
                foreach (var file in files)
                {
                    var repoFile = ActiveProject.AddFile(GetRepoPath(ActiveProject.Repo, file.RepoRelativePath), file.ProjectRelativePath);

                    repoFile.MarkAnalyzed();

                    RepoFileAnalyzer.RepoFileUpload(repoFile, file);
                }

                return base.AddBoundFilesAsync(files);
            }

            public override Task FinalizeAsync()
            {
                // TODO: Should we allow finalize?
                //return innerStore.WriteBlockListAsync();
                return Task.CompletedTask;
            }
        }
    }
}