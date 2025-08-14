using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Files;
using Codex.Analysis.Managed;
using Codex.Import;
using Codex.ObjectModel;
using Codex.ObjectModel.Implementation;
using Codex.Utilities;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis.Projects
{
    public class ManagedProjectAnalyzer : RepoProjectAnalyzerBase
    {
        public readonly SemanticServices semanticServices;
        private readonly Lazy<Task<Solution>> lazySolution;
        private Project Project;
        private CompilationServices CompilationServices;
        private Compilation Compilation;
        private AnalyzedProjectContext ProjectContext;
        private readonly ProjectId ProjectId;
        private CompletionTracker CompletionTracker = new CompletionTracker();

        public ManagedProjectAnalyzer(
            SemanticServices semanticServices,
            RepoProject repoProject,
            ProjectId projectId,
            Lazy<Task<Solution>> lazySolution)
        {
            this.semanticServices = semanticServices;
            ProjectId = projectId;
            this.lazySolution = lazySolution;
            ProjectContext = repoProject.ProjectContext;
        }

        public override async Task Analyze(RepoProject project)
        {
            var logger = project.Repo.AnalysisServices.Logger;
            try
            {
                var services = project.Repo.AnalysisServices;
                logger.LogMessage("Loading project: " + project.ProjectId);

                var solution = await lazySolution.Value;
                Project = solution.GetProject(ProjectId);
                if (Project == null)
                {
                    logger.LogError($"Can't find project for {ProjectId} in {solution}");
                }
                else
                {
                    Project = Project.WithMetadataReferences(Project.MetadataReferences.Where(m => !(m is UnresolvedMetadataReference)));

                    Compilation = await Project.GetCompilationAsync();
                    CompilationServices = new CompilationServices(Compilation);

                    foreach (var reference in Compilation.ReferencedAssemblyNames)
                    {
                        var referencedProject = new ReferencedProject()
                        {
                            ProjectId = reference.Name,
                            DisplayName = reference.GetDisplayName(),
                            Properties = new PropertyMap()
                                {
                                    { "PublicKey", string.Concat(reference.PublicKey.Select(b => b.ToString("X2"))) }
                                }
                        };

                        ProjectContext.ReferencedProjects.TryAdd(referencedProject.ProjectId, referencedProject);
                    }

                    if (Project.TryGetTargetFramework(out var targetFramework))
                    {
                        ProjectContext.Project.TargetFramework = targetFramework;
                    }
                }

                await base.Analyze(project);

                project.Analyzer = RepoProjectAnalyzer.Null;
            }
            catch (Exception ex)
            {
                logger.LogExceptionError($"Loading project {project.ProjectId}", ex);
            }
        }

        public RepoFileAnalyzer CreateFileAnalyzer(DocumentInfo document)
        {
            return new FileAnalyzer(this, document);
        }

        private class FileAnalyzer : RepoFileAnalyzer
        {
            public override bool LoadContent => false;

            private ManagedProjectAnalyzer ProjectAnalyzer;
            private readonly DocumentInfo DocumentInfo;

            public FileAnalyzer(ManagedProjectAnalyzer projectAnalyzer, DocumentInfo documentInfo)
            {
                if (projectAnalyzer == null)
                {
                    throw new ArgumentNullException(nameof(projectAnalyzer));
                }

                if (documentInfo == null)
                {
                    throw new ArgumentNullException(nameof(documentInfo));
                }

                ProjectAnalyzer = projectAnalyzer;
                DocumentInfo = documentInfo;
            }

            public override SourceFileInfo AugmentSourceFileInfo(SourceFileInfo info)
            {
                info.Language = ProjectAnalyzer.Project.Language;
                info.EncodingInfo = (DocumentInfo.TextLoader as FileSystemTextLoader)?.EncodingInfo
                                ?? SourceEncodingInfo.Default;
                return info;
            }

            protected override async Task AnnotateFileAsync(AnalysisServices services, RepoFile file, BoundSourceFileBuilder binder)
            {
                var project = ProjectAnalyzer.Project;
                var document = project.GetDocument(DocumentInfo.Id);
                var text = await document.GetTextAsync();
                binder.SourceText = text;

                DocumentAnalyzer analyzer = new DocumentAnalyzer(
                    ProjectAnalyzer.semanticServices,
                    document,
                    ProjectAnalyzer.CompilationServices,
                    file.LogicalPath,
                    ProjectAnalyzer.ProjectContext,
                    binder);

                await analyzer.PopulateBoundSourceFileAsync();
            }

            protected override void BeforeUpload(AnalysisServices services, RepoFile file, BoundSourceFile boundSourceFile)
            {
                ManagedAnalysisHost.Instance.OnDocumentFinished(boundSourceFile);
                ProjectAnalyzer.ProjectContext.ReportDocument(boundSourceFile, file);
                base.BeforeUpload(services, file, boundSourceFile);
            }

            protected override async Task Analyze(AnalysisServices services, RepoFile file)
            {
                try
                {
                    var project = ProjectAnalyzer.Project;
                    if (project == null)
                    {
                        file.PrimaryProject.Repo.AnalysisServices.Logger.LogError("Project is null");
                        return;
                    }

                    await base.Analyze(services, file);
                }
                finally
                {
                    file.Analyzer = RepoFileAnalyzer.Null;
                    ProjectAnalyzer = null;
                }
            }
        }
    }
}
