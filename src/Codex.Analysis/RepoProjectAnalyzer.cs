using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codex.Import;

namespace Codex.Analysis
{

    public class RepoProjectAnalyzerBase : RepoProjectAnalyzer
    {
    }

    public class NullRepoProjectAnalzyer : RepoProjectAnalyzer
    {
        public override Task Analyze(RepoProject project)
        {
            return Task.CompletedTask;
        }
    }

    public class RepoProjectAnalyzer
    {
        public static readonly RepoProjectAnalyzer Default = new RepoProjectAnalyzer();

        public static readonly RepoProjectAnalyzer Null = new NullRepoProjectAnalzyer();

        public virtual void CreateProjects(Repo repo) { }

        public virtual async Task Analyze(RepoProject project)
        {
            var analysisServices = project.Repo.AnalysisServices;
            analysisServices.Logger.WriteLine($"Analyzing project {project.ProjectId}");
            List<Task> fileTasks = new List<Task>();

            foreach (var file in project.Files)
            {
                if (file.PrimaryProject == project)
                {
                    if (analysisServices.ParallelProcessProjectFiles)
                    {
                        fileTasks.Add(analysisServices.TaskDispatcher.Invoke(() => AnalyzeFile(file), TaskType.File));
                    }
                    else
                    {
                        await AnalyzeFile(file);
                    }
                }
            }

            await Task.WhenAll(fileTasks);

            await UploadProject(project);
        }

        private static async Task AnalyzeFile(RepoFile file)
        {
            try
            {
                await file.Analyze();
            }
            catch (Exception ex)
            {
                var logger = file.PrimaryProject.Repo.AnalysisServices.Logger;
                logger.LogExceptionError($"Analyzing file ({file.PrimaryProject.ProjectId}::{file.FilePath}):", ex);
            }
        }

        public virtual void CreateProjects(RepoFile repoFile) { }

        public virtual bool ShouldAddProjectFileLink(RepoFile repoFile) => true;

        public virtual bool IsCandidateProjectFile(RepoFile repoFile) => false;

        protected async Task UploadProject(RepoProject project)
        {
            await FinalizeProject(project);

            var analyzedProject = project.ProjectContext.Project;

            if (project.ProjectFile != null)
            {
                analyzedProject.PrimaryFile = new ProjectFileLink()
                {
                    RepoRelativePath = project.ProjectFile.RepoRelativePath,
                    ProjectRelativePath = project.ProjectFile.LogicalPath
                };
            }

            analyzedProject.ProjectKind = project.ProjectKind;
            foreach (var file in project.Files
                .OrderBy(f => f.LogicalPath).ThenBy(f => f.RepoRelativePath))
            {
                if (ShouldAddProjectFileLink(file))
                {
                    analyzedProject.Files.Add(new ProjectFileLink()
                    {
                        RepoRelativePath = file.RepoRelativePath,
                        ProjectRelativePath = file.LogicalPath
                    });
                }
            }

            await project.Repo.AnalysisServices.RepositoryStore.AddProjectsAsync(new[] { analyzedProject });
        }

        protected virtual Task FinalizeProject(RepoProject project)
        {
            return project.ProjectContext.Finish(project);
        }
    }
}
