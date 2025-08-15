using System.Diagnostics;
using System.Diagnostics.ContractsLight;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Codex.Application;
using Codex.Application.Verbs;
using Codex.Build.Tasks;
using Codex.Lucene;
using Codex.Lucene.Formats;
using Codex.Lucene.Search;
using Codex.ObjectModel;
using Codex.Sdk;
using Codex.Sdk.Search;
using Codex.Storage.BlockLevel;
using Codex.Utilities;
using Codex.View;
using CodexTestBProject;
using CodexTestProject;
using LibGit2Sharp;
using Mono.Cecil.Cil;
using Xunit.Abstractions;

namespace Codex.Integration.Tests;

public record AnalyzeTestProjectBase(ITestOutputHelper Output) : CodexTestBase(Output)
{
    public record AnalyzeTestProjectOptions
    {
        public List<ITestProjectData> Projects { get; set; } = new()
        {
            TestProjects.A
        };

        public bool ZipEncryptAnalysisOutput = false;

        // Disabling till we fix zip upload / reading to yield results consistent with
        // file upload. Seems like content is changed somehow.
        public bool DisableBlockZipUpload = true;
        public bool UploadFiles = false;
        public int ProjectIndex = 0;
        public ITestProjectData Project => Projects[ProjectIndex];
        public string ProjectDirectory => Project.ProjectDirectory;
        public string ProjectPath => Project.ProjectPath;

        public int IngestCount { get; set; } = 1;
        public bool SearchOnly { get; set; } = Environment.GetEnvironmentVariable("CodexTest_SearchOnly") == "1";
        public bool CleanIndex { get; set; } = true;
        public string TemplateReplacement { get; set; }
        public string Caller { get; set; }
        public Func<string, bool> IsAllowedTestFile;
        public Action<IngestOperation> ConfigureIngest;
        public Action<AnalyzeOperation> ConfigureAnalyze;

        public string AnalyzedDirectoryQualifier = "";
        public bool IncludeSecondaryProject
        {
            set
            {
                Contract.Assert(value);
                if (value && !Projects.Contains(TestProjects.B))
                {
                    Projects.Add(TestProjects.B);
                }
            }
        }

        public string OutputPath { get; set; }
    }

    public Task<IngestOperation> RunAnalyzeTestProject(bool searchOnly, bool cleanIndex, string templateReplacement = null, [CallerMemberName] string caller = null)
    {
        return RunAnalyzeTestProject(o => o with
        {
            SearchOnly = searchOnly,
            CleanIndex = cleanIndex,
            TemplateReplacement = templateReplacement,
            Caller = caller
        });
    }

    protected virtual void PreconfigureOptions(AnalyzeTestProjectOptions options)
    {

    }

    public async Task<AnalyzeOperation> RunAnalyzeTestProjectAnalysis(
        Func<AnalyzeTestProjectOptions, AnalyzeTestProjectOptions> configureOptions = null,
        AsyncOut<AnalyzeTestProjectOptions> optionsOut = null,
        [CallerMemberName] string caller = null)
    {
        var options = new AnalyzeTestProjectOptions();
        PreconfigureOptions(options);
        options = configureOptions?.Invoke(options) ?? options;
        optionsOut?.Set(options);

        bool searchOnly = options.SearchOnly;
        bool cleanIndex = options.CleanIndex;
        caller = options.Caller ?? caller;
        string templateReplacement = options.TemplateReplacement;

        var outputPath = options.OutputPath = GetTestOutputDirectory(testName: caller);
        options.OutputPath = outputPath;

        var args = File.ReadAllLines(Path.Combine(options.ProjectDirectory, "csc.args.txt")).AsEnumerable();

        bool isAllowedTestFile(string path)
        {
            return path.ContainsIgnoreCase("TestCases") && (options.IsAllowedTestFile?.Invoke(path) ?? true);
        }

        args = args.Where(a => a.StartsWith('/') || isAllowedTestFile(a));

        if (templateReplacement != null)
        {
            var templateCodeFile = args.Where(a => a.ContainsIgnoreCase("TemplateCode.cs")).First();

            var templateCode = File.ReadAllText(Path.Combine(options.ProjectDirectory, templateCodeFile));
            templateCode = templateCode.Replace("Template", templateReplacement);
            var processedTemplateFilePath = Path.Combine(outputPath, "template.out.cs");
            File.WriteAllText(processedTemplateFilePath, templateCode);
            args = args.Concat(new[] { processedTemplateFilePath });
        }

        var argsPath = Path.Combine(outputPath, "csc.args.txt");
        File.WriteAllLines(argsPath,
            $"{CompilerArgumentsUtilities.ProjectFilePrefix}{options.ProjectPath}".ToCollection()
            .Concat(args));

        var analyze = new AnalyzeOperation()
        {
            RootDirectory = TestProject.RepoDirectory,
            DetectGit = false,
            CompilerArgumentsSearchPaths =
            {
                argsPath
            },
            OutputDirectory = Path.Combine(outputPath, "analyze", options.AnalyzedDirectoryQualifier),
            DisableOptimization = true,
            Clean = !searchOnly,
            DisableEnumeration = true,
            DisableMsBuild = true,
            Logger = Logger
        };

        options.ConfigureAnalyze?.Invoke(analyze);

        await analyze.RunAsync(initializeOnly: searchOnly, logErrors: false);

        return analyze;
    }

    protected const string PrimaryProjectRepoName = "testproj/A";
    protected const string SecondaryProjectRepoName = "testproj/B";

    public async Task<IngestOperation> RunAnalyzeTestProject(
        Func<AnalyzeTestProjectOptions, AnalyzeTestProjectOptions> configureOptions = null,
        [CallerMemberName] string caller = null)
    {
        int index = 0;
        configureOptions = configureOptions.ApplyBefore(o =>
        {
            o.ProjectIndex = index;
            if (o.Projects.Count > 1)
            {
                var qualifier = o.Project.Name;

                o.AnalyzedDirectoryQualifier = Path.Combine(o.AnalyzedDirectoryQualifier ?? string.Empty, qualifier);
                o.ConfigureAnalyze = o.ConfigureAnalyze.ApplyBefore(a =>
                {
                    a.RepoName = o.Project.RepoName;
                });

                o.ConfigureIngest = o.ConfigureIngest.ApplyBefore(i =>
                {
                    i.Scan = true;
                    i.InputPath = Path.GetDirectoryName(i.InputPath);
                });
            }

            return o;
        });

        var analyze = await RunAnalyzeTestProjectAnalysis(configureOptions, AsyncOut.Var<AnalyzeTestProjectOptions>(out var optionsOut), caller);
        var options = optionsOut.Value;

        for (int i = 1; i < options.Projects.Count; i++)
        {
            index = i;
            await RunAnalyzeTestProjectAnalysis(configureOptions, null, caller);
        }

        return await RunAnalyzeTestProjectIngest(analyze, options, caller);
    }

    protected virtual void ConfigureIngest(IngestOperation ingest) { }

    public async Task<IngestOperation> RunAnalyzeTestProjectIngest(
        AnalyzeOperation analyze,
        AnalyzeTestProjectOptions options,
        [CallerMemberName] string caller = null)
    {
        bool searchOnly = options.SearchOnly;
        bool cleanIndex = options.CleanIndex;
        var outputPath = options.OutputPath = GetTestOutputDirectory(testName: caller);

        var analysisOutputPath = analyze.OutputDirectory;
        string privateKey = null;

        if (options.ZipEncryptAnalysisOutput)
        {
            var keys = EncryptionUtilities.GenerateAsymmetricKeys();
            privateKey = keys.PrivateKey;
            analysisOutputPath = analysisOutputPath + ".zip";
            MiscUtilities.CreateZipFromDirectory(
                analyze.OutputDirectory,
                analysisOutputPath,
                publicKey: keys.PublicKey,
                generatedPassword: new(out var generatedPassword),
                encryptedPassword: new(out var encryptedPassword));

            Output.WriteLine(JsonSerializationUtilities.SerializeEntity(new
            {
                encryptedPassword,
                generatedPassword,
                keys.PublicKey,
                keys.PrivateKey
            }, flags: JsonFlags.Indented));
        }

        using var _ = SdkFeatures.DefaultZipStorePasswordPrivateKey.EnableGlobal(privateKey);

        var ingest = new IngestOperation()
        {
            UseStoredFilters = true,
            InputPath = analysisOutputPath,
            OutputDirectory = Path.Combine(outputPath, "ingest"),
            Clean = !searchOnly && cleanIndex,
            Logger = Logger,
        };

        Logger.LogMessage("Ingesting to:");
        Logger.LogMessage(ingest.OutputDirectory);

        ConfigureIngest(ingest);
        options.ConfigureIngest?.Invoke(ingest);


        await ingest.RunAsync(initializeOnly: searchOnly, logErrors: false);


        return ingest;
    }
}