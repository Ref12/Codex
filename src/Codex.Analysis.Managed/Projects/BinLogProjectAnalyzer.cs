using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    public class BinLogProjectAnalyzer : RepoProjectAnalyzerBase
    {
        private readonly Func<string, string[]> binLogFinder;
        private readonly Logger logger;
        private readonly string[] binlogSearchPaths;
        public bool RequireProjectFilesExist { get; set; }

        public BinLogProjectAnalyzer(Logger logger,
            string[] binlogSearchPaths, 
            Func<string, string[]> binLogFinder = null)
        {
            this.logger = logger;
            this.binlogSearchPaths = binlogSearchPaths;
            logger.LogMessage($"Binlog search search paths:{Environment.NewLine}{string.Join(Environment.NewLine, binlogSearchPaths)}");
            this.binLogFinder = binLogFinder ?? FindBinLogs;
        }

        public override void CreateProjects(Repo repo)
        {
            foreach (var binlogSearchPath in binlogSearchPaths)
            {
                var binlogs = binLogFinder(binlogSearchPath);
                if (binlogs.Length != 0)
                {
                    logger.LogMessage($"Found {binlogs.Length} binlogs at bin log search path '{binlogSearchPath}':{Environment.NewLine}{string.Join(Environment.NewLine, binlogs)}");
                }
                else
                {
                    logger.LogMessage($"No {binlogs.Length} binlog found at bin log search path '{binlogSearchPath}'.");
                }

                foreach (var binlog in binlogs)
                {
                    SolutionInfoBuilder builder = new SolutionInfoBuilder(binlog, repo);
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

        public static string[] FindFiles(string searchPath, params string[] fileTypes)
        {
            bool recursive = false;
            if (searchPath.EndsWith("*"))
            {
                searchPath = searchPath.TrimEnd('*');
                recursive = true;
            }

            if (string.IsNullOrWhiteSpace(searchPath))
            {
                return Array.Empty<string>();
            }

            if (File.Exists(searchPath))
            {
                return new[] { searchPath };
            }

            if (Directory.Exists(searchPath))
            {
                return fileTypes.SelectMany(fileType => Directory.GetFiles(searchPath, fileType, 
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    .ToArray();
            }

            return Array.Empty<string>();
        }

        public static string[] FindBinLogs(string binlogSearchPath)
        {
            return FindFiles(binlogSearchPath, "*.binlog");
        }

        private bool TryCandidateBinLogPath(string candidate, string searchDirectory = null)
        {
            var exists = candidate != null ? File.Exists(candidate) : false;
            candidate = candidate ?? (searchDirectory.EnsureTrailingSlash() + "*.binlog");
            logger.LogMessage($"Looking for binlog at '{candidate}'. Found = {exists}");
            return exists;
        }

        public class SolutionInfoBuilder : InvocationSolutionInfoBuilderBase
        {
            private string binLogPath;

            public SolutionInfoBuilder(string binLogFilePath, Repo repo)
                : base(binLogFilePath, repo)
            {
                this.binLogPath = binLogFilePath;
                Initialize();
            }

            public void Initialize()
            {
                if (binLogPath == null)
                {
                    return;
                }

                foreach (var invocation in BinLogReader.ExtractInvocations(binLogPath))
                {
                    StartLoadProject(invocation);
                }
            }
        }
    }
}
