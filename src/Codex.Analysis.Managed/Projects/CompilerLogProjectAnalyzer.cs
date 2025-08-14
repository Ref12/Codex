using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Basic.CompilerLog.Util;
using Codex.Analysis.Managed;
using Codex.Import;
using Codex.Logging;
using Codex.MSBuild;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis.Projects
{
    public class CompilerLogProjectAnalyzer : RepoProjectAnalyzerBase
    {
        private readonly Func<string, string[]> compilerLogFinder;
        private readonly Logger logger;
        private readonly string[] compilerLogSearchPaths;
        public bool RequireProjectFilesExist { get; set; }

        public CompilerLogProjectAnalyzer(Logger logger,
            string[] compilerLogSearchPaths, 
            Func<string, string[]> compilerLogFinder = null)
        {
            this.logger = logger;
            this.compilerLogSearchPaths = compilerLogSearchPaths;
            logger.LogMessage($"CompilerLog search search paths:{Environment.NewLine}{string.Join(Environment.NewLine, compilerLogSearchPaths)}");
            this.compilerLogFinder = compilerLogFinder ?? FindCompilerLogs;
        }

        public override void CreateProjects(Repo repo)
        {
            foreach (var compilerLogSearchPath in compilerLogSearchPaths)
            {
                var compilerLogs = compilerLogFinder(compilerLogSearchPath);
                if (compilerLogs.Length != 0)
                {
                    logger.LogMessage($"Found {compilerLogs.Length} compiler logs at compiler log search path '{compilerLogSearchPath}':{Environment.NewLine}{string.Join(Environment.NewLine, compilerLogs)}");
                }
                else
                {
                    logger.LogMessage($"No {compilerLogs.Length} compiler log found at compiler log search path '{compilerLogSearchPath}'.");
                }

                foreach (var compilerLog in compilerLogs)
                {
                    SolutionInfoBuilder builder = new SolutionInfoBuilder(compilerLog, repo);
                    if (builder.HasProjects)
                    {
                        SolutionProjectAnalyzer.AddSolutionProjects
                            (repo, 
                            () => Task.FromResult(builder.Build()),
                            workspace: builder.Workspace,
                            requireProjectExists: RequireProjectFilesExist, 
                            solutionName: builder.SolutionName);
                    }
                }
            }
        }

        public static string[] FindCompilerLogs(string compilerLogSearchPath)
        {
            return BinLogProjectAnalyzer.FindFiles(compilerLogSearchPath,
                "*.compilerlog", "*.complog");
        }

        public class SolutionInfoBuilder : InvocationSolutionInfoBuilderBase
        {
            private string compilerLogPath;
            private SolutionReader reader;

            public SolutionInfoBuilder(string compilerLogFilePath, Repo repo)
                : base(compilerLogFilePath, repo)
            {
                this.compilerLogPath = compilerLogFilePath;
                Initialize();
            }

            public override bool HasProjects => reader?.ProjectCount > 0;

            public void Initialize()
            {
                if (compilerLogPath == null)
                {
                    return;
                }

                Placeholder.Todo("Dispose reader?");
                reader = SolutionReader.Create(compilerLogPath);
            }

            public override SolutionInfo Build(bool linkProjects = false)
            {
                return reader.ReadSolutionInfo();
            }

        }
    }
}
