﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Import;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;

namespace Codex.Analysis.Projects
{
    public abstract class SolutionProjectAnalyzer : RepoProjectAnalyzerBase
    {
        private readonly HashSet<string> includedSolutions;

        public bool RequireProjectFilesExist { get; set; } = true;

        public SolutionProjectAnalyzer(string[] includedSolutions = null)
        {
            this.includedSolutions = includedSolutions == null ?
                null :
                new HashSet<string>(includedSolutions, StringComparer.OrdinalIgnoreCase);
        }

        public override void CreateProjects(Repo repo)
        {
        }

        public override bool IsCandidateProjectFile(RepoFile repoFile)
        {
            if (repoFile.FilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                if (includedSolutions == null || includedSolutions.Contains(repoFile.FilePath))
                {
                    return true;
                }
            }

            return false;
        }

        public override void CreateProjects(RepoFile repoFile)
        {
            if (!repoFile.FilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var repo = repoFile.PrimaryProject.Repo;
            var logger = repo.AnalysisServices.Logger;

            Func<Task<SolutionInfo>> solutionInfoLoader = () => GetSolutionInfoAsync(repoFile);

            AddSolutionProjects(
                repo, 
                solutionInfoLoader, 
                RequireProjectFilesExist, 
                solutionName: repoFile.FilePath);
        }

        protected abstract Task<SolutionInfo> GetSolutionInfoAsync(RepoFile repoFile);

        private static SolutionInfo DedupeReferences(SolutionInfo solution)
        {
            return SolutionInfo.Create(solution.Id, solution.Version, solution.FilePath,
                solution.Projects.Select(DedupeReferences));
        }

        private static ProjectInfo DedupeReferences(ProjectInfo project)
        {
            return project.WithProjectReferences(project.ProjectReferences.Distinct());
        }

        internal static void AddSolutionProjects(Repo repo, Func<Task<SolutionInfo>> solutionInfoLoader,
            bool requireProjectExists = true, string solutionName = "Anonymous solution", AdhocWorkspace workspace = null)
        {
            workspace = workspace ?? new AdhocWorkspace(MefHostServices.DefaultHost);

            AddSolutionProjects(
                repo, 
                workspace,
                solutionInfoLoader,
                solutionInfo => workspace.AddSolution(DedupeReferences(solutionInfo)),
                requireProjectExists, 
                solutionName);
        }

        public static void AddSolutionProjects(
            Repo repo, 
            Workspace workspace, 
            Func<Task<SolutionInfo>> solutionInfoLoader, 
            Func<SolutionInfo, Solution> solutionSelector,
            bool requireProjectExists = true, 
            string solutionName = "Anonymous solution")
        {
            var dispatcher = repo.AnalysisServices.TaskDispatcher;
            var logger = repo.AnalysisServices.Logger;

            dispatcher.QueueInvoke(async () =>
            {
                try
                {
                    logger.LogMessage("Loading solution: " + solutionName);

                    var csharpSemanticServices = new Lazy<SemanticServices>(() => new SemanticServices(workspace, LanguageNames.CSharp));
                    var visualBasicSemanticServices = new Lazy<SemanticServices>(() => new SemanticServices(workspace, LanguageNames.VisualBasic));

                    var solutionInfo = await solutionInfoLoader();

                    Lazy<Task<Solution>> lazySolution = new Lazy<Task<Solution>>(() =>
                    {
                        var solution = solutionSelector(solutionInfo);
                        return Task.FromResult(solution);
                    }, isThreadSafe: true);

                    logger.LogMessage($"Found {solutionInfo.Projects.Count} projects in solution '{solutionName}'");

                    foreach (var projectInfo in solutionInfo.Projects)
                    {
                        logger.LogMessage($"Processing project '{projectInfo.FilePath}' for solution '{solutionName}'");

                        RepoFile projectFile = null;
                        if (!string.IsNullOrEmpty(projectInfo.FilePath))
                        {
                            projectFile = repo.DefaultRepoProject.AddFile(projectInfo.FilePath);
                        }

                        if (requireProjectExists && projectFile == null)
                        {
                            logger.LogMessage($"Project '{projectInfo.FilePath}' does not exist for solution '{solutionName}'");
                            continue;
                        }

                        if (repo.ProjectsById.ContainsKey(projectInfo.AssemblyName))
                        {
                            logger.LogMessage($"Project '{projectInfo.AssemblyName}' with path '{projectInfo.FilePath}' already has analyzer other than for solution '{solutionName}'");
                            continue;
                        }

                        var repoProject = repo.CreateRepoProject(
                            projectInfo.AssemblyName,
                            projectFile != null ? Path.GetDirectoryName(projectFile.FilePath) : string.Empty,
                            projectFile);

                        logger.LogMessage($"Adding project '{projectInfo.FilePath}' for solution '{solutionName}'");
                        AddSolutionProject(lazySolution, projectInfo, projectFile, repoProject, csharpSemanticServices, visualBasicSemanticServices);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogExceptionError($"Loading solution: {solutionName}", ex);
                }
            });
        }

        public static void AddSolutionProject(
            Lazy<Task<Solution>> lazySolution,
            ProjectInfo projectInfo,
            RepoFile projectFile,
            RepoProject repoProject,
            Lazy<SemanticServices> csharpSemanticServices = null,
            Lazy<SemanticServices> visualBasicSemanticServices = null)
        {
            var semanticServices = projectInfo.Language == LanguageNames.CSharp ?
                                        csharpSemanticServices :
                                        visualBasicSemanticServices;

            Contract.Assert(semanticServices.Value != null);

            var projectAnalyzer = new ManagedProjectAnalyzer(
                semanticServices.Value,
                repoProject,
                projectInfo.Id,
                lazySolution);

            if (projectFile != null)
            {
                projectFile.HasExplicitAnalyzer = true;
            }

            repoProject.Analyzer = projectAnalyzer;

            foreach (var document in projectInfo.Documents)
            {
                if (document == null)
                {
                    throw new ArgumentNullException($"Project {projectInfo.Id} has a null document.");
                }

                if (Path.GetFileName(document.FilePath).StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var file = AddDocumentToProject(repoProject, document);
                if (file != null && projectAnalyzer != null)
                {
                    file.Analyzer = projectAnalyzer.CreateFileAnalyzer(document);
                }
            }

            foreach (var document in projectInfo.AdditionalDocuments)
            {
                var inMemoryContent = document.TextLoader is StaticTextLoader loader ? loader.Content : null;
                var file = AddDocumentToProject(repoProject, document, inMemory: inMemoryContent != null);
                if (file != null)
                {
                    file.InMemoryContent = inMemoryContent;
                }
            }
        }

        private static RepoFile AddDocumentToProject(RepoProject project, DocumentInfo document, bool inMemory = false)
        {
            try
            {
                if (document.FilePath == null || (!inMemory && !File.Exists(document.FilePath)))
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

            string logicalPath = null;
            if (!project.InProjectDirectory(document.FilePath))
            {
                logicalPath = GetLogicalPath(document);
                if (logicalPath.Contains(":"))
                {
                    logicalPath = null;
                }
            }

            return project.AddFile(document.FilePath, logicalPath);
        }

        private static string GetLogicalPath(DocumentInfo file)
        {
            return Path.Combine(Path.Combine(file.Folders.ToArray()), Path.GetFileName(file.FilePath));
        }
    }
}
