using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Codex.Utilities;
using DotNext.Collections.Generic;

namespace Codex.Automation.Workflow
{
    using static Codex.CodexConstants;
    using static Helpers;

    public class WorkflowProgram
    {
        public delegate bool RunCodexProcessDelegate(string exePath, string arguments, AsyncOut<int> exitCode);
        public static RunCodexProcessDelegate RunCodexProcess = (exePath, arguments, exitCode) => Helpers.RunProcess(exePath, arguments, exitCode: exitCode);

        private static Mode GetMode(ref string[] args)
        {
            if (args[0].StartsWith("/"))
            {
                // No mode specified. Use default mode.
                return Mode.FullAnalyze;
            }
            else
            {
                var modeArgument = args[0];
                args = args.Skip(1).ToArray();

                if (!Enum.TryParse<Mode>(modeArgument, ignoreCase: true, result: out var mode))
                {
                    throw new ArgumentException("Invalid mode: " + modeArgument);
                }

                return mode;
            }
        }

        private static bool HasModeFlag(Mode mode, Mode flag)
        {
            return (mode & flag) == flag;
        }

        public static int Main(string[] args)
        {
            return Run(args, out _);
        }

        public static int Run(string[] args, out Arguments arguments)
        {
            var exitCode = new AsyncOut<int>();
            arguments = null;
            if (args.Length == 0)
            {
                // TODO: Add help text
                Console.WriteLine("No arguments specified.");
                return 0;
            }

            Mode mode = GetMode(ref args);

            if (mode == Mode.GetLocation)
            {
                Console.WriteLine(Assembly.GetExecutingAssembly().Location);
                return 0;
            }

            arguments = mode == Mode.Codex || mode == Mode.Cli
                ? new Arguments()
                : Arguments.Parse(args);

            bool success = RunMode(mode, arguments, args, exitCode);

            if (!success)
            {
                Console.WriteLine("##vso[task.complete result=Failed;]DONE");
            }

            return !success && exitCode.Value == 0 ? -1 : exitCode.Value;
        }

        private static bool RunMode(Mode mode, Arguments arguments, string[] args, AsyncOut<int> exitCode)
        {
            if (mode == Mode.Cli)
            {
                return new CliProgram().RunAsync(args).GetAwaiter().GetResult() == 0;
            }

            string codexBinDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            arguments.CodexBinDir = codexBinDirectory;
            string executablePath = Path.Combine(codexBinDirectory, "Codex.exe");

            if (mode == Mode.Codex)
            {
                return RunCodexProcess(executablePath, new ArgList(args).ToString(), exitCode: exitCode);
            }

            bool success = true;
            if (string.IsNullOrEmpty(arguments.RepoName))
            {
                arguments.RepoName = GetRepoName(arguments);
            }
            else if (string.IsNullOrEmpty(arguments.CodexRepoUrl))
            {
                if (arguments.RepoUri != null 
                    || SourceControlUri.TryParse(arguments.RepoName, out arguments.RepoUri, checkRepoNameFormat: true))
                {
                    arguments.CodexRepoUrl = arguments.RepoUri.GetUrl();
                }
            }

            if (HasModeFlag(mode, Mode.BuildOnly))
            {
                UpdateBuildNumber(arguments);
            }

            if (!string.IsNullOrEmpty(arguments.CodexOutputRoot)
                && string.IsNullOrEmpty(arguments.SourcesDirectory))
            {
                arguments.SourcesDirectory = Path.Combine(arguments.CodexOutputRoot, "src");
            }

            arguments.CodexOutputRoot ??= "";

            if (!arguments.NoBuildTag && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RELEASE_RELEASENAME")))
            {
                Console.WriteLine($"##vso[build.addbuildtag]{BuildTags.CodexEnabled}");
                Console.WriteLine($"##vso[build.addbuildtag]{BuildTags.FormatVersion}");

                if (HasModeFlag(mode, Mode.IndexOnly))
                {
                    Console.WriteLine($"##vso[build.addbuildtag]{BuildTags.CodexIndexEnabled}");
                }

                if (SourceControlUri.TryParse(arguments.CodexRepoUrl, out var uri))
                {
                    Console.WriteLine($"##vso[build.addbuildtag]{uri.GetBuildTag()}");
                }
            }

            string createOutDir(string name)
            {
                var dir = Path.Combine(arguments.CodexOutputRoot, name);
                Directory.CreateDirectory(dir);
                return dir;
            }

            arguments.BinLogDir ??= createOutDir("binlogs");
            string binlogDirectory = arguments.BinLogDir;
            arguments.ProjectDataDir = createOutDir("projectdata");
            arguments.DebugDir = createOutDir("debug");
            arguments.ToolsDir = createOutDir("tools");

            arguments.BuildTempDir = createOutDir("bldtmp");

            arguments.ProjectDataArgs = new()
            {
                executablePath,
                "analyze",
                "-o",
                arguments.ProjectDataDir,
                "--scenario",
                "ProjectData",
                "--path",
                arguments.Settings?.RepoRoot ?? arguments.SourcesDirectory,
                "--name",
                arguments.RepoUri?.GetRepoName() ?? arguments.RepoName ?? "unknown",
            };

            string analysisOutputDirectory = (arguments.AnalyzeOutputDirectory ??= createOutDir("store"));
            string indexOutputDirectory = (arguments.IngestOutputDirectory ??= createOutDir("index"));
            arguments.ExtractionRoot = createOutDir("extract");

            var envMap = arguments.GetEnvMap();

            if (arguments.RepoConfigRoot is string repoConfigRoot 
                && (File.Exists(Out.Var(out var analysisSettingsPath, Path.Combine(repoConfigRoot, $"{arguments.Qualifier}.analyze.settings.json")))
                || File.Exists(Out.Var(out analysisSettingsPath, Path.Combine(repoConfigRoot, "analyze.settings.json")))))
            {
                var text = File.ReadAllText(analysisSettingsPath);
                var processedText = MiscUtilities.ExpandVariableTokens(text, name =>
                {
                    return (envMap.TryGetValue(name, out var value) ? value : Environment.GetEnvironmentVariable(name))?.Replace(@"\", @"\\");
                });
                var settings = JsonSerializationUtilities.DeserializeEntity<AnalyzeSettings>(processedText);
                arguments.AdditionalCodexArguments.Add(settings.AddArguments);
                arguments.AnalysisRemoveArguments.Add(settings.RemoveArguments);
                arguments.Settings = settings;
                arguments.EncryptOutputs |= settings.EncryptOutputs;
            }

            Func<string> getAnalysisArguments = () => string.Join(" ", new ArgList(new[] {
                    "analyze",
                    arguments.Clean ? "--clean" : "",
                    "--noMsBuild",
                    "--out",
                    analysisOutputDirectory,
                    "--path",
                    arguments.Settings?.RepoRoot ?? arguments.SourcesDirectory,
                    "--binLogSearchDirectory",
                    arguments.BinLogDir + "*",
                    "--projectData",
                    arguments.ProjectDataDir,
                    }
                    .Except(arguments.AnalysisRemoveArguments, StringComparer.OrdinalIgnoreCase)
                    .Concat(arguments.AdditionalCodexArguments)
                    .Select(s => envMap.ReplaceTokens(s))));
            var analysisArguments = getAnalysisArguments();
            string indexArguments = string.Join(" ", new ArgList(new[] {
                    "ingest",
                    arguments.Clean ? "--clean" : "",
                    "--storedFilters",
                    "--external",
                    "--in",
                    arguments.IngestInputDirectory ?? analysisOutputDirectory,
                    "--out",
                    indexOutputDirectory,
                    }
                    .Except(arguments.IndexRemoveArguments, StringComparer.OrdinalIgnoreCase)
                    .Concat(arguments.AdditionalIndexArguments)
                    .Select(s => envMap.ReplaceTokens(s))));

            if (HasModeFlag(mode, Mode.Prepare))
            {
                // download files
                Console.WriteLine($"##vso[task.setvariable variable=CodexBinDir;]{codexBinDirectory}");
                Console.WriteLine($"##vso[task.setvariable variable=CodexAnalysisOutDir;]{analysisOutputDirectory}");
                Console.WriteLine($"##vso[task.setvariable variable=CodexExePath;]{executablePath}");
                Console.WriteLine($"##vso[task.setvariable variable=CodexAnalysisArguments;]{analysisArguments}");

                if (!string.IsNullOrEmpty(arguments.RepoName))
                {
                    Console.WriteLine($"##vso[task.setvariable variable=CodexRepoName;]{arguments.RepoName}");
                }
            }

            if (HasModeFlag(mode, Mode.GC))
            {
                // run exe
                var gcArguments = $"gc --es {arguments.ElasticSearchUrl}";
                success &= RunCodexProcess(executablePath, gcArguments, exitCode);
            }

            if (HasModeFlag(mode, Mode.BuildOnly))
            {
                var analysisPreparation = new AnalysisPreparation(arguments);
                analysisPreparation.Run();
                analysisArguments = getAnalysisArguments();

                Directory.CreateDirectory(arguments.ProjectDataDir);

                Console.WriteLine($"##vso[task.setvariable variable=CodexAnalysisArguments;]{analysisArguments}");

                // Set build number again here because build may have changed the build number.
                UpdateBuildNumber(arguments);
            }

            if (HasModeFlag(mode, Mode.AnalyzeOnly))
            {
                Directory.CreateDirectory(arguments.ProjectDataDir);

                // run exe
                success &= RunCodexProcess(executablePath, analysisArguments, exitCode);
            }

            if (HasModeFlag(mode, Mode.IndexOnly))
            {
                // run exe
                if (!string.IsNullOrEmpty(arguments.CodexOutputRoot))
                {
                    Directory.CreateDirectory(arguments.CodexOutputRoot);
                }

                success &= RunCodexProcess(executablePath, indexArguments, exitCode);
            }

            if (HasModeFlag(mode, Mode.UploadOnly))
            {
                foreach (var (directory, _outputName) in new[] {
                    (analysisOutputDirectory, BuildAnalysisArtifactName), 
                    (indexOutputDirectory, BuildIndexArtifactName),
                    (arguments.DebugDir, "DebugDir")})
                {
                    if (Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any())
                    {
                        // get json files and zip
                        var outputName = _outputName;

                        var zipDirectory = createOutDir(outputName);

                        string outputZipFileName = arguments.RepoUri?.GetUnicodeRepoFileName() ?? Path.GetFileName(directory.TrimTrailingSlash());

                        if (arguments.Commit != null)
                        {
                            outputZipFileName += "." + Guid.NewGuid().ToString("N").Substring(0, 8);
                        }

                        string outputZip = Path.Combine(zipDirectory, outputZipFileName + ".zip");

                        Console.WriteLine($"Zipping analysis files: '{directory}' -> '{outputZip}' ");

                        MiscUtilities.CreateZipFromDirectory(sourceDirectory: directory, outputZip,
                            password: arguments.EncryptOutputs ? SdkFeatures.DefaultZipStorePassword : (string)null,
                            publicKey: arguments.EncryptOutputs ? SdkFeatures.DefaultZipStorePasswordPublicKey : (string)null);

                        if (HasModeFlag(mode, Mode.Test))
                        {
                            outputName += "Test";
                        }

                        // publish to a vsts build
                        Console.WriteLine($"Publishing to Build: '{outputZip}' -> '{outputName}'");
                        Console.WriteLine($"##vso[artifact.upload artifactname={outputName};]{outputZip}");
                        Console.WriteLine($"##vso[build.addbuildtag]{outputName}");
                    }
                    else
                    {
                        Console.WriteLine($"Could not find files in directory '{directory}'.");
                    }
                }

                //foreach (var debugFile in PathUtilities.GetDirectoryFilesSafe(arguments.DebugDir, "*", SearchOption.TopDirectoryOnly))
                //{
                //    Console.WriteLine($"##vso[artifact.upload artifactname={DebugArtifactName};]{debugFile}");
                //}
            }

            if (arguments.UploadBinlogs)
            {
                foreach (var binlog in
                    PathUtilities.GetDirectoryFilesSafe(binlogDirectory, "*.binlog", SearchOption.TopDirectoryOnly)
                    .Concat(PathUtilities.GetDirectoryFilesSafe(binlogDirectory, "*.complog", SearchOption.TopDirectoryOnly)))
                {
                    Console.WriteLine($"##vso[artifact.upload artifactname={BinLogArtifactName};]{binlog}");
                }
            }

            if (HasModeFlag(mode, Mode.Ingest))
            {

            }

            return success;
        }

        private static void UpdateBuildNumber(Arguments arguments)
        {
            if (!string.IsNullOrEmpty(arguments.BuildName))
            {
                var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID");
                var invalidChars = new char[] { '"', '/', ':', '<', '>', '\\', '|', '?', '@', '*' };
                var buildNumber = $"{arguments.BuildName}{arguments.Timestamp:yyyyMMdd.hhmmsss}#{buildId}";
                invalidChars.ForEach(c => buildNumber = buildNumber.Replace(c, '_'));
                Console.WriteLine(
                    $"##vso[build.updatebuildnumber]{buildNumber}");
            }
        }

        private static string GetRepoName(Arguments arguments)
        {
            var repoName = arguments.CodexRepoUrl;

            if (!string.IsNullOrEmpty(repoName))
            {
                if (arguments.RepoUri != null || SourceControlUri.TryParse(repoName, out arguments.RepoUri))
                {
                    repoName = arguments.RepoUri.GetRepoName().Replace('/', '_');
                    arguments.CodexRepoUrl = arguments.RepoUri.GetUrl();
                }
                else
                {
                    repoName = repoName.TrimEnd('/');
                    var lastSlashIndex = repoName.LastIndexOf('/');
                    if (lastSlashIndex > 0)
                    {
                        repoName = repoName.Substring(lastSlashIndex + 1);
                    }
                }
            }

            return repoName;
        }
    }
}
