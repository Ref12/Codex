using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Managed;
using Codex.Build.Tasks;
using Codex.Import;
using Codex.Logging;
using Codex.MSBuild;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis.Projects
{
    using static CompilerArgumentsUtilities;

    public abstract class InvocationSolutionInfoBuilderBase
    {
        public readonly string SolutionName;
        public AdhocWorkspace Workspace;
        public Dictionary<string, CompilerInvocation> InvocationsByProjectPath = new Dictionary<string, CompilerInvocation>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ProjectInfoBuilder> ProjectInfoByAssemblyNameMap = new ConcurrentDictionary<string, ProjectInfoBuilder>(StringComparer.OrdinalIgnoreCase);
        protected Repo repo;
        private Logger logger;

        public virtual bool HasProjects => ProjectInfoByAssemblyNameMap.Count != 0;

        protected InvocationSolutionInfoBuilderBase(string solutionName, Repo repo)
        {
            this.SolutionName = solutionName;
            this.repo = repo;
            this.logger = repo.AnalysisServices.Logger;

            Workspace = new AdhocWorkspace(MefHostServices.DefaultHost);
        }

        public void StartLoadProject(CompilerInvocation invocation)
        {
            if (!repo.AnalysisServices.AnalysisIgnoreProjectFilter.IncludeFile(
                        repo.AnalysisServices.FileSystem,
                        invocation.ProjectFile))
            {
                logger.LogMessage($"Excluding invocation from '{invocation.ProjectFile}' due to filter.");
                return; 
            }

            InvocationsByProjectPath[invocation.ProjectFile] = invocation;

            string[] args = Array.Empty<string>();

            try
            {
                var projectPath = invocation.ProjectFile;
                var projectName = Path.GetFileNameWithoutExtension(projectPath);

                args = TrimCompilerExeFromCommandLine(invocation.GetCommandLineArguments(), GetCompilerKind(invocation.Language));
                ProjectInfo projectInfo = GetCommandLineProject(projectPath, projectName, invocation.Language, args);
                var assemblyName = Path.GetFileNameWithoutExtension(projectInfo.OutputFilePath);
                ProjectInfoBuilder info = GetProjectInfo(assemblyName);
                info.ProjectInfo = projectInfo;
            }
            catch (Exception ex) when (TraceException(ex, invocation, args))
            {
            }
        }

        private bool TraceException(Exception ex, CompilerInvocation invocation, string[] args)
        {
            logger.LogExceptionError($"Failed loading project '{invocation.ProjectFile}' with args '{string.Join(Environment.NewLine, args)}'", ex);

            return true;
        }

        private ProjectInfoBuilder GetProjectInfo(string assemblyName)
        {
            return ProjectInfoByAssemblyNameMap.GetOrAdd(assemblyName, k => new ProjectInfoBuilder(k));
        }

        protected ProjectInfo GetCommandLineProject(string projectPath, string projectName, string languageName, string[] args)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            string outputPath;
            CommandLineArguments commonArgs;
            string extension;
            if (languageName == LanguageNames.VisualBasic)
            {
                var vbArgs = VB.VisualBasicCommandLineParser.Default.Parse(args, projectDirectory, sdkDirectory: null);
                commonArgs = vbArgs;
                extension = "*.vb";
            }
            else
            {
                var csArgs = CS.CSharpCommandLineParser.Default.Parse(args, projectDirectory, sdkDirectory: null);
                commonArgs = csArgs;
                extension = "*.cs";
            }

            outputPath = Path.Combine(commonArgs.OutputDirectory, commonArgs.OutputFileName);
            var generatedFiles = GetGeneratedFiles(commonArgs.GeneratedFilesOutputDirectory, extension);
            if (generatedFiles.Length != 0)
            {
                logger.LogDiagnostic($"Project '{projectName}' loaded {generatedFiles.Length} generated files from '{commonArgs.GeneratedFilesOutputDirectory}'");
            }

            Placeholder.Todo("Add analyzers as well for use with source generators");
            var argsWithoutExcluded = args.Where(arg => !IsExcludedArg(arg)).Concat(generatedFiles).ToArray();

            var projectInfo = CommandLineProject.CreateProjectInfo(
                projectName: projectName,
                language: languageName,
                commandLineArgs: argsWithoutExcluded,
                projectDirectory: projectDirectory,
                workspace: Workspace);

            // Add additional document with command line args
            projectInfo = projectInfo.WithAdditionalDocuments(
                projectInfo.AdditionalDocuments.Concat(CreateCommandLineArgumentsDocument(args, projectName)));

            projectInfo = projectInfo.WithOutputFilePath(outputPath).WithFilePath(projectPath);
            return projectInfo;
        }

        private string[] GetGeneratedFiles(string generatedFilesOutputDirectory, string extension)
        {
            if (!string.IsNullOrEmpty(generatedFilesOutputDirectory) && Directory.Exists(generatedFilesOutputDirectory))
            {
                return Directory.GetFiles(generatedFilesOutputDirectory, extension, SearchOption.AllDirectories);
            }
            else
            {
                return Array.Empty<string>();
            }
        }

        private IEnumerable<DocumentInfo> CreateCommandLineArgumentsDocument(string[] args, string projectName)
        {
            var argumentsText = string.Join(Environment.NewLine, args);

            yield return DocumentInfo.Create(
                DocumentId.CreateNewId(ProjectId.CreateNewId()),
                "CompilerArguments.txt",
                loader: new StaticTextLoader(argumentsText),
                filePath: CodexConstants.GetMetadataFilePath("CompilerArguments.txt"),
                isGenerated: true);
        }

        private bool IsExcludedArg(string arg)
        {
            return arg.StartsWith("/a:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/analyzer:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-a:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-analyzer:", StringComparison.OrdinalIgnoreCase);
        }

        public virtual SolutionInfo Build(bool linkProjects = false)
        {
            List<ProjectInfo> projects = new List<ProjectInfo>();
            foreach (var project in ProjectInfoByAssemblyNameMap.Values)
            {
                if (project.HasProjectInfo)
                {
                    if (linkProjects)
                    {
                        project.Link(this);
                    }

                    projects.Add(project.ProjectInfo);
                }
            }

            return SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default, SolutionName, projects);
        }

        private class ProjectInfoBuilder
        {
            public bool HasProjectInfo => ProjectInfo != null;
            public string AssemblyName;
            private ProjectInfo projectInfo;
            public ProjectInfo ProjectInfo
            {
                get
                {
                    return projectInfo;
                }
                set
                {
                    if (projectInfo == null)
                    {
                        projectInfo = value;
                    }
                    else
                    {
                        projectInfo = GetBestProjectInfo(projectInfo, value);
                    }
                }
            }

            private ProjectInfo GetBestProjectInfo(ProjectInfo projectInfo1, ProjectInfo projectInfo2)
            {
                projectInfo1.TryGetTargetFramework(out var tf1);
                projectInfo2.TryGetTargetFramework(out var tf2);

                var compareResult = ProjectTargetFramework.Compare(tf1, tf2);
                if (compareResult < 0) return projectInfo2;
                else if (compareResult > 0) return projectInfo1;

                // Heuristic: Project with most documents is the best project info
                if (projectInfo1.Documents.Count > projectInfo2.Documents.Count)
                {
                    return projectInfo1;
                }

                return projectInfo2;
            }

            public ProjectInfoBuilder(string assemblyName)
            {
                AssemblyName = assemblyName;
            }

            public void Link(InvocationSolutionInfoBuilderBase solutionInfo)
            {
                if (ProjectInfo != null)
                {
                    List<ProjectReference> projectReferences = new List<ProjectReference>();
                    List<MetadataReference> metadataReferences = new List<MetadataReference>();
                    foreach (var reference in ProjectInfo.MetadataReferences)
                    {
                        string path = null;
                        if (reference is PortableExecutableReference)
                        {
                            path = ((PortableExecutableReference)reference).FilePath;
                        }
                        else if (reference is UnresolvedMetadataReference)
                        {
                            path = ((UnresolvedMetadataReference)reference).Reference;
                        }

                        if (string.IsNullOrEmpty(path))
                        {
                            metadataReferences.Add(reference);
                            continue;
                        }

                        var assemblyName = Path.GetFileNameWithoutExtension(path);

                        ProjectInfoBuilder referencedProject;
                        if (solutionInfo.ProjectInfoByAssemblyNameMap.TryGetValue(assemblyName, out referencedProject) && referencedProject.HasProjectInfo)
                        {
                            projectReferences.Add(new ProjectReference(
                                referencedProject.ProjectInfo.Id,
                                reference.Properties.Aliases,
                                reference.Properties.EmbedInteropTypes));
                        }
                        else
                        {
                            metadataReferences.Add(reference);
                        }
                    }

                    ProjectInfo = ProjectInfo.WithMetadataReferences(metadataReferences);
                    ProjectInfo = ProjectInfo.WithProjectReferences(projectReferences);
                }
            }
        }
    }
}
