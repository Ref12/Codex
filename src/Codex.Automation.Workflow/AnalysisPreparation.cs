using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;

namespace Codex.Automation.Workflow
{
    using static Helpers;

    internal class AnalysisPreparation
    {
        private readonly Arguments arguments;
        private const string MsBuildPath = "msbuild";
        private const string DotNetPath = "dotnet";
        private const string NugetPath = "nuget";

        private enum BuildFile
        {
            prebuild,
            build
        }

        private Dictionary<string, bool> ToolExistsMap { get; } = new(StringComparer.OrdinalIgnoreCase);

        private readonly string binlogDirectory;

        public AnalysisPreparation(Arguments arguments)
        {
            this.arguments = arguments;
            this.binlogDirectory = arguments.BinLogDir;
        }

        public void Run()
        {
            bool sanitizeRemotes = false;

            bool useCommit = !string.IsNullOrEmpty(arguments.Commit);
            if (!arguments.NoClone)
            {
                var repoUrl = arguments.CodexRepoUrl;
                var expansions = new Dictionary<string, string>();

                if (arguments.RepoUri != null)
                {
                    var repoUriBuilder = new UriBuilder(repoUrl);
                    if (arguments.PersonalAccessTokens.TryGetValue(arguments.RepoUri.GetRepoName(), out var pat))
                    {
                        var parVarToken = "%PAT%";
                        repoUriBuilder.UserName = parVarToken;
                        expansions[parVarToken] = pat;
                        repoUrl = repoUriBuilder.ToString();
                        sanitizeRemotes = true;
                        arguments.EncryptOutputs = true;
                    }
                }

                bool successfullyCloned = RunProcess(
                    "git.exe", 
                    new ArgList() 
                    {
                        "clone",
                        useCommit ? "--no-checkout" : "",
                        repoUrl, 
                        arguments.SourcesDirectory
                    }.ToString(),
                    expansions: expansions);
                if (!successfullyCloned)
                {
                    throw new Exception($"Failed to clone {arguments.CodexRepoUrl}");
                }
            }

            if (useCommit)
            {
                if (!RunProcess("git.exe", $"checkout -b local/{Guid.NewGuid():N}/{arguments.Commit} {arguments.Commit}", workingDirectory: arguments.SourcesDirectory))
                {
                    throw new Exception($"Failed to checkout of '{arguments.Commit}' for {arguments.CodexRepoUrl}");
                }
            }

            // Need to enumerate solutions before intializing submodules because we don't want to build submodule solutions
            string[] solutions = arguments.RootProjects.Count != 0
                ? arguments.RootProjects.Select(r => Path.Combine(arguments.SourcesDirectory, r)).ToArray()
                : EnumerateSolutions();

            if (!arguments.NoClone)
            {
                if (!RunProcess("git.exe", "submodule update --init --recursive", workingDirectory: arguments.SourcesDirectory))
                {
                    throw new Exception($"Failed to init submodules for {arguments.CodexRepoUrl}");
                }

                if (sanitizeRemotes)
                {
                    if (!RunProcess("git.exe", "remote remove origin") ||
                        !RunProcess("git.exe", $"remote add origin {arguments.CodexRepoUrl}"))
                    {
                        throw new Exception($"Failed sanitize remotes for {arguments.CodexRepoUrl}");
                    }
                }
            }

            MiscUtilities.UpdateEnvironmentVariable("PATH", path =>
            {
                Console.WriteLine(string.Join(Environment.NewLine, "Prior path:".AsSingle().Concat(path.Split(";"))));
                var updatedPath = string.Join(";", arguments.Settings.PrependedPaths.Concat(
                    new[] { arguments.ToolsDir, path }).Select(Path.GetFullPath));

                return updatedPath;
            });

            var updatedPath = Environment.GetEnvironmentVariable("PATH");
            Console.WriteLine(string.Join(Environment.NewLine, "Updated path:".AsSingle().Concat(updatedPath.Split(";"))));

            // Disable nuget auditing
            Console.WriteLine("Disabling nuget auditing");
            Environment.SetEnvironmentVariable("NugetAudit", "false");

            if (!string.IsNullOrEmpty(arguments.RepoConfigRoot))
            {
                foreach (var buildFile in new[] { BuildFile.prebuild, BuildFile.build })
                {
                    var buildCmdPath = Path.GetFullPath(Path.Combine(arguments.RepoConfigRoot,
                    arguments.Settings?.BuildCmdRelativePath ?? $"{buildFile}.cmd"));

                    bool exists = File.Exists(buildCmdPath);
                    Console.WriteLine($"Searching for '{buildCmdPath}'. Found: {exists}");
                    if (exists)
                    {
                        RunProcess(buildCmdPath, "", new EnvMap(arguments.GetEnvMap())
                        {
                            ["CodexBuildSolutions"] = string.Join(";", solutions),
                        },
                        workingDirectory: arguments.SourcesDirectory);

                        if (buildFile == BuildFile.build)
                        {
                            return;
                        }
                    }
                }
            }

            void checkTool(string tool)
            {
                ToolExistsMap[tool] = RunProcess("cmd", $"/c where {tool}");
                Console.WriteLine($"Searching for '{tool}'. Found: {ToolExistsMap[tool]}");
            }

            checkTool(NugetPath);
            checkTool(DotNetPath);
            checkTool(MsBuildPath);

            // TODO: Rewrite projects?

            TryRestore(solutions);

            TryBuild(solutions, out var unbuildableSolutions);

            arguments.AdditionalCodexArguments.Add(unbuildableSolutions.SelectMany(s => new[] { "--solution", s }));
            arguments.AnalysisRemoveArguments.Add("--noMsBuild");
        }

        private void TryBuild(string[] solutions, out List<string> unbuildableSolutions)
        {
            unbuildableSolutions = new List<string>();
            foreach (var solution in solutions)
            {
                if (arguments.Settings?.Build == false || !TryBuild(solution))
                {
                    // TODO: Try a design-time build?
                    unbuildableSolutions.Add(solution);
                }
            }
        }

        private void UpdatePath()
        {
        }

        private bool TryBuild(string solution)
        {
            Log(solution);

            var binlogName = ComputeBinLogName(solution);

            if (InvokeBuild(MsBuildPath, "/m", $@"/bl:{binlogDirectory}\{binlogName}.binlog", "/p:TreatWarningsAsErrors=false", solution))
            {
                return true;
            }

            return InvokeBuild(DotNetPath, "build", "/m", $@"/bl:{binlogDirectory}\{binlogName}.dn.binlog", "/p:TreatWarningsAsErrors=false", solution);
        }

        public bool InvokeBuild(string processExe, params string[] arguments)
        {
            return InvokeTool(processExe, arguments.Concat(this.arguments.BuildArgs).ToArray());
        }

        private string ComputeBinLogName(string solution)
        {
            return $"{Path.GetFileNameWithoutExtension(solution)}.{solution.GetHashCode()}";
        }

        private void TryRestore(string[] solutions)
        {
            foreach (var solution in solutions)
            {
                TryRestore(solution);
            }
        }

        private void TryRestore(string solution)
        {
            Log(solution);

            InvokeTool(DotNetPath, "workload", "restore", solution);

            InvokeTool(MsBuildPath, "/t:Restore", solution);

            InvokeTool(DotNetPath, "restore", solution);

            InvokeTool(NugetPath, "restore", solution);
        }

        private bool InvokeTool(string processExe, params string[] arguments)
        {
            if (ToolExistsMap[processExe])
            {
                return RunProcess(processExe, new ArgList(arguments).ToString(), this.arguments.Settings?.EnvironmentVariables);
            }
            else
            {
                Console.WriteLine($"Skipping missing tool invocation: {processExe} {new ArgList(arguments)}");
                return false;
            }
        }

        private (string Pattern, SearchOption SearchOption)[] BuildProjSearches = new[]
        {
            // Start with top level traversal projects or solutions
            ("build.proj", SearchOption.TopDirectoryOnly),
            ("dirs.proj", SearchOption.TopDirectoryOnly),
            ("*.sln", SearchOption.TopDirectoryOnly),
            ("*.xln", SearchOption.TopDirectoryOnly),
            ("*.slnx", SearchOption.TopDirectoryOnly),

            // Next try top level traversal projects or solutions under src
            ("src/build.proj", SearchOption.TopDirectoryOnly),
            ("src/dirs.proj", SearchOption.TopDirectoryOnly),
            ("src/*.sln", SearchOption.TopDirectoryOnly),
            ("src/*.xln", SearchOption.TopDirectoryOnly),
            ("src/*.slnx", SearchOption.TopDirectoryOnly),

            // Loose project at root or under src/
            ("*.*proj", SearchOption.TopDirectoryOnly),
            ("src/*.*proj", SearchOption.TopDirectoryOnly),

            // Solutions and traversal projects
            ("*.sln", SearchOption.AllDirectories),
            ("*.xln", SearchOption.AllDirectories),
            //("*.proj", SearchOption.AllDirectories),
            ("*.slnx", SearchOption.AllDirectories),
        };

        private string[] EnumerateSolutions()
        {
            var settings = arguments.Settings;
            string[] getBuildFiles()
            {
                if (settings.BuildFiles?.Count > 0)
                {
                    return settings.BuildFiles.ToArray();
                }

                var hasSrcFolder = Directory.Exists(Path.Combine(arguments.SourcesDirectory, "src"));
                foreach (var buildProjectSearch in BuildProjSearches)
                {
                    if (buildProjectSearch.Pattern.StartsWith("src") && !hasSrcFolder)
                    {
                        continue;
                    }

                    var files = Directory.GetFiles(
                        arguments.SourcesDirectory,
                        buildProjectSearch.Pattern,
                        buildProjectSearch.SearchOption);

                    if (files.Length != 0)
                    {
                        return files;
                    }
                }

                return Array.Empty<string>();
            }

            var additionalBuildFiles = settings.AdditionalBuildFiles ?? new List<string>();
            return getBuildFiles().Concat(additionalBuildFiles)
                .Select(p => Path.GetFullPath(Path.Combine(arguments.SourcesDirectory, p)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(p => File.Exists(p))
                .ToArray();
        }
    }
}
