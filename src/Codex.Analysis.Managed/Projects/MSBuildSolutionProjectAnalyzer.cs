using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Import;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;

namespace Codex.Analysis.Projects
{
    public class MSBuildSolutionProjectAnalyzer : SolutionProjectAnalyzer
    {
        MSBuildProjectLoader loader;

        public MSBuildSolutionProjectAnalyzer(string[] includedSolutions = null, bool disableDirectoryBuildFiles = false)
            : base(includedSolutions)
        {
            var workspace = MSBuildWorkspace.Create();
            
            workspace.WorkspaceFailed += Workspace_WorkspaceFailed;
            var propertiesOpt = ImmutableDictionary<string, string>.Empty;

            // Explicitly add "CheckForSystemRuntimeDependency = true" property to correctly resolve facade references.
            // See https://github.com/dotnet/roslyn/issues/560
            propertiesOpt = propertiesOpt.Add("CheckForSystemRuntimeDependency", "true");
            propertiesOpt = propertiesOpt.Add("VisualStudioVersion", "16.0");
            propertiesOpt = propertiesOpt.Add("AlwaysCompileMarkupFilesInSeparateDomain", "false");

            if (disableDirectoryBuildFiles)
            {
                propertiesOpt = propertiesOpt.Add("ImportDirectoryBuildTargets", "false");
                propertiesOpt = propertiesOpt.Add("ImportDirectoryBuildProps", "false");
            }

            loader = new MSBuildProjectLoader(workspace, propertiesOpt)
            {
                SkipUnrecognizedProjects = true,
            };
        }

        private void Workspace_WorkspaceFailed(object sender, WorkspaceDiagnosticEventArgs e)
        {
            //throw new Exception(e.Diagnostic.Message);
        }

        protected override async Task<SolutionInfo> GetSolutionInfoAsync(RepoFile repoFile)
        {
            if (IsSolutionFile(repoFile))
            {
                return await loader.LoadSolutionInfoAsync(repoFile.FilePath);
            }
            else
            {
                var projectInfo = await loader.LoadProjectInfoAsync(repoFile.FilePath);
                return SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default, repoFile.FilePath, projectInfo);
            }
        }
    }
}
