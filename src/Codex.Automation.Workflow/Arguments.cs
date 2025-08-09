using System;
using System.Collections;
using System.Collections.ObjectModel;
using Codex.Utilities;
using CommandLine;

namespace Codex.Automation.Workflow
{
    public class Arguments
    {
        public DateTime Timestamp = DateTime.UtcNow;
        public ArgList AdditionalCodexArguments = new ArgList();
        public ArgList AdditionalIndexArguments = new ArgList();

        public AnalyzeSettings Settings = new AnalyzeSettings();
        public ArgList BuildArgs = new ArgList();
        public ArgList RootProjects = new ArgList();

        public ArgList ProjectDataArgs = new ArgList();

        public List<string> AnalysisRemoveArguments = new List<string>();
        public List<string> IndexRemoveArguments = new List<string>();
        public string CodexOutputRoot;
        public string BinLogDir;
        public string ProjectDataDir;
        public string CodexRepoUrl;
        public SourceControlUri RepoUri;

        public string RepoConfigRoot
        {
            get
            {
                if (string.IsNullOrEmpty(ConfigRoot) || RepoUri == null)
                {
                    return null;
                }

                return Path.GetFullPath(Path.Combine(
                    ConfigRoot,
                    "repos",
                    RepoUri.GetKindPathFragment(),
                    RepoUri.GetRepoName()));
            }
        }

        public string ConfigRoot;
        public string SourcesDirectory;
        public string RepoName;
        public bool EncryptOutputs;

        private string _buildName;
        public string BuildName => _buildName.AsNotEmptyOrNull() ?? RepoName;

        public string CodexBinDir { get; internal set; }
        public string DebugDir { get; internal set; }
        public string ToolsDir { get; internal set; }
        public string BuildTempDir { get; internal set; }
        public string ExtractionRoot { get; internal set; }

        public string Commit;
        public string Qualifier;

        public string ElasticSearchUrl;
        public string JsonFilePath;
        public string IngestInputDirectory;
        public string IngestOutputDirectory;
        public string AnalyzeOutputDirectory;
        public bool NoClone;
        public bool Clean;
        public bool NoBuildTag;
        public bool UploadBinlogs;
        public bool GenerateCompilerLogs;
        public Dictionary<string, string> PersonalAccessTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private void ExtractCommitAndTag(string argValue, ref string target)
        {
            Commit = null;
            Qualifier = null;
            ExtractCore("@", ref argValue, ref Commit);
            ExtractCore("#", ref argValue, ref Qualifier);
            ExtractCore("@", ref argValue, ref Commit);
            Qualifier ??= Commit;
            target = argValue;
        }

        private static void ExtractCore(string separator, ref string argValue, ref string extractTarget)
        {
            if (argValue.Contains(separator) && argValue.Split(separator) is var parts)
            {
                argValue = parts[0];
                extractTarget = parts[1];
            }
        }

        public static Arguments Parse(string[] args)
        {
            var context = new ArgumentContext();
            Arguments newArgs = new Arguments();
            if (Environment.GetEnvironmentVariable("SYSTEM_ARTIFACTSDIRECTORY") is string artifactDirectory
                && !string.IsNullOrEmpty(artifactDirectory))
            {
                newArgs.CodexOutputRoot = Path.Combine(artifactDirectory, "cdxout");
            }

            bool foundMatch = false;
            foreach (string arg in args)
            {
                context.IsActiveIteration = false;
                foundMatch = true;

                string argValue;
                bool switchValue;
                if (SetMatchArg(arg, "Commit", ref newArgs.Commit)) { }
                else if (MatchArg(arg, "SourcesDirectory", out argValue))
                {
                    newArgs.SourcesDirectory = argValue;
                }
                else if (MatchArg(arg, "RepoAlias", out argValue)) 
                {
                    newArgs.AdditionalCodexArguments.Add("--alias");
                    newArgs.AdditionalCodexArguments.Add(argValue);
                }
                else if (MatchArg(arg, "IngestInputDirectory", out argValue))
                {
                    newArgs.IngestInputDirectory = argValue;
                }
                else if (MatchArg(arg, "ConfigRoot", out argValue))
                {
                    newArgs.ConfigRoot = argValue;
                }
                else if (MatchSwitch(arg, "EncryptOutputs", ref newArgs.EncryptOutputs)) { }
                else if (MatchArg(arg, "BuildName", out argValue))
                {
                    newArgs._buildName = argValue;
                }
                else if (MatchArg(arg, "IngestOutputDirectory", out argValue) || MatchArg(arg, "IngestOut", out argValue))
                {
                    newArgs.IngestOutputDirectory = argValue;
                }
                else if (MatchArg(arg, "AnalyzeOutputDirectory", out argValue) || MatchArg(arg, "AnalyzeOut", out argValue))
                {
                    newArgs.AnalyzeOutputDirectory = argValue;
                }
                else if (MatchListArg(arg, "BuildArgs", newArgs.BuildArgs, context)) { }
                else if (MatchListArg(arg, "RootProjects", newArgs.RootProjects, context)) { }
                else if (MatchListArg(arg, "AdditionalCodexArguments", newArgs.AdditionalCodexArguments, context)) { }
                else if (MatchListArg(arg, "AdditionalIndexArguments", newArgs.AdditionalIndexArguments, context)) { }
                else if (MatchListArg(arg, "AnalysisRemoveArguments", newArgs.AnalysisRemoveArguments, context)) { }
                else if (MatchListArg(arg, "IndexRemoveArguments", newArgs.IndexRemoveArguments, context)) { }
                else if (MatchArg(arg, "CodexOutputRoot", out argValue))
                {
                    newArgs.CodexOutputRoot = Path.GetFullPath(argValue);
                }
                else if (MatchArg(arg, "BinLogDir", out argValue))
                {
                    newArgs.BinLogDir = Path.GetFullPath(argValue);
                }
                else if (MatchSwitch(arg, "GenerateCompilerLogs", ref newArgs.GenerateCompilerLogs)
                    || MatchSwitch(arg, "gencomplog", ref newArgs.GenerateCompilerLogs)) { }
                else if (MatchSwitch(arg, "UploadBinlogs", ref newArgs.UploadBinlogs)) { }
                else if (MatchSwitch(arg, "NoClone", ref newArgs.NoClone)) { }
                else if (MatchSwitch(arg, "NoBuildTag", ref newArgs.NoBuildTag)) { }
                else if (MatchSwitch(arg, "Clean", ref newArgs.Clean)) { }
                else if (MatchArg(arg, "CodexRepoUrl", out argValue))
                {
                    newArgs.ExtractCommitAndTag(argValue, ref newArgs.CodexRepoUrl);
                }
                else if (MatchArg(arg, "RepoName", out argValue))
                {
                    newArgs.ExtractCommitAndTag(argValue, ref newArgs.RepoName);
                }
                else if (MatchArg(arg, "ElasticSearchUrl", out argValue))
                {
                    newArgs.ElasticSearchUrl = argValue;
                }
                else if (MatchArg(arg, "JsonFilePath", out argValue))
                {
                    newArgs.JsonFilePath = argValue;
                }
                else if (MatchArg(arg, "Pat", out argValue))
                {
                    var pair = argValue.Split('=');
                    Console.WriteLine($"Adding PAT: '{pair[0]}'='{string.Empty.PadRight(Math.Max(3, pair[1].Length), '*')}'");
                    newArgs.PersonalAccessTokens[pair[0]] = pair[1];
                }
                else if (MatchArg(arg, "PrintEnv", out argValue))
                {
                    Console.WriteLine($"Environment Variables:");

                    foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
                    {
                        Console.WriteLine($"{envVar.Key}={envVar.Value}");
                    }

                    Console.WriteLine($"Done printing environment variables.");
                }
                else
                {
                    foundMatch = false;
                    if (context.IsActive)
                    {
                        context.ArgumentValues.Add(arg);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid Arguments: " + arg);
                    }
                }

                if (foundMatch)
                {
                    context.IsActive = context.IsActiveIteration;
                }
            }

            return newArgs;
        }

        private static bool MatchListArg(string arg, string argName, IList<string> values, ArgumentContext context)
        {
            if (MatchArg(arg, argName, out var value))
            {
                values.Add(value);
            }
            else if (arg.EqualsIgnoreCase($"--{argName}"))
            {
                context.SetActive();
                context.ArgumentName = argName;
                context.ArgumentValues = values;
                return true;
            }

            return false;
        }

        private static bool SetMatchArg(string arg, string argName, ref string argValue)
        {
            if (MatchArg(arg, argName, out var value))
            {
                argValue = value;
                return true;
            }

            return false;
        }

        private static bool MatchArg(string arg, string argName, out string argValue)
        {
            if (arg.StartsWith($"/{argName}:", StringComparison.OrdinalIgnoreCase))
            {
                argValue = arg.Substring(argName.Length + 2);
                return true;
            }

            argValue = null;
            return false;
        }

        private static bool MatchSwitch(string arg, string argName, ref bool argValue)
        {
            var prefix = $"/{argName}";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (arg.Length == prefix.Length)
                {
                    argValue = true;
                    return true;
                }
                else if (arg.Length == (prefix.Length + 1))
                {
                    switch (arg[prefix.Length])
                    {
                        case '-':
                            argValue = false;
                            return true;
                        case '+':
                            argValue = true;
                            return true;
                    }
                }
            }

            return false;
        }

        public EnvMap GetEnvMap()
        {
            return GetEnvMap(this);
        }

        private static EnvMap GetEnvMap(Arguments arguments)
        {
            return new()
            {
                ["CodexConfigRoot"] = arguments.ConfigRoot,
                ["CodexRepoConfigRoot"] = arguments.RepoConfigRoot,
                ["CodexReposConfigRoot"] = Path.Combine(arguments.ConfigRoot ?? "", "repos"),
                ["CodexBuildConfigRoot"] = Path.Combine(arguments.ConfigRoot ?? "", "repos", ".build"),
                ["SrcDir"] = arguments.SourcesDirectory,
                ["BinLogDir"] = arguments.BinLogDir,
                ["CodexDebugDir"] = arguments.DebugDir,
                ["CodexToolsDir"] = arguments.ToolsDir,
                ["CodexBuildTempDir"] = arguments.BuildTempDir,
                ["CodexBinDir"] = arguments.CodexBinDir,
                ["CodexExtractTargets"] = Path.Combine(arguments.CodexBinDir ?? "", "Codex.Managed.Extractor.targets"),
                ["CodexCommonArgs"] = arguments.ProjectDataArgs?.ToString(),
                ["CodexProjectExtractionRoot"] = arguments.ExtractionRoot,
            };
        }
    }


    public class EnvMap : Dictionary<string, string>
    {
        public EnvMap() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public EnvMap(EnvMap map) : base(map, map.Comparer)
        {

        }

        public string ReplaceTokens(string input)
        {
            foreach ((var key, var value) in this)
            {
                if (input.ContainsIgnoreCase(key))
                {
                    input = input.ReplaceIgnoreCase($"{{{key}}}", value);
                }
            }

            return input;
        }
    }

    public class ArgList : Collection<string>
    {
        public ArgList() { }

        public ArgList(params string[] args)
        {
            Add(args);
        }

        public ArgList(IEnumerable<string> args)
        {
            Add(args);
        }

        public void Add(IEnumerable<string> args)
        {
            foreach (var arg in args)
            {
                Add(arg);
            }
        }

        public override string ToString()
        {
            return string.Join(" ", this.Select(s => s.QuoteIfNeeded()));
        }

        protected override void InsertItem(int index, string item)
        {
            if (string.IsNullOrEmpty(item)) return;
            base.InsertItem(index, item);
        }
    }

    public class ArgumentContext
    {
        public bool IsActiveIteration { get; set; }
        public bool IsActive { get; set; }
        public string ArgumentName { get; set; }
        public IList<string> ArgumentValues { get; set; }
        public string ArgumentValue { get; set; }

        public void SetActive()
        {
            IsActiveIteration = true;
        }
    }
}
