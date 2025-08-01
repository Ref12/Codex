using System.Diagnostics;
using System.Runtime.CompilerServices;
using Codex.Application.Verbs;
using Codex.Lucene.Search;
using Codex.Sdk;
using Codex.Sdk.Search;
using Codex.Search;
using Codex.Storage;
using Codex.Utilities;
using Codex.View;
using Codex.Web.Common;
using Codex.Web.Wasm;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;
using static Codex.Lucene.Search.PagingHelpers;

namespace Codex.Integration.Tests;

[TestCaseOrderer("Codex.Integration.Tests.CodexTestOrderer", "Codex.Integration.Tests")]
public record CodexTestBase : IDisposable
{
    public IConfiguration Secrets { get; protected set; }

    public Action<AnalyzeOperation> UpdateAnalyze { get; set; }

    public bool CleanTestDir { get; set; }
    public ITestOutputHelper Output { get; }
    public TestLogger Logger { get; }
    private string _testOutputDirectory;
    public string TestRoot { get; set; } = string.Empty;

    public CodexTestBase(ITestOutputHelper output)
    {
        Output = new TimerOutputHelper(output);
        Logger = new TestLogger(output);
        Console.SetOut(Logger.Writer);
        Console.SetError(Logger.Writer);
        SdkFeatures.AmbientLogger.EnableLocal(Logger);
        SdkFeatures.GlobalLogger.EnableGlobal(Logger);
        SdkFeatures.TestLogger.EnableGlobal(Logger);

        Features.IsTest.EnableGlobal(true);

        var builder = new ConfigurationBuilder()
            .AddUserSecrets<CodexTestBase>();

        Secrets = builder.Build();

        Initialize();
    }

    protected virtual void Initialize()
    {
    }


    private record TimerOutputHelper(ITestOutputHelper Inner) : ITestOutputHelper
    {
        private Stopwatch _sw = Stopwatch.StartNew();

        public void WriteLine(string message)
        {
            Inner.WriteLine(PrependTime(message));
        }

        public void WriteLine(string format, params object[] args)
        {
            Inner.WriteLine(PrependTime(format), args);
        }

        private string PrependTime(string value)
        {
            return $"{_sw.Elapsed:mm\\:ss\\.fff}: {value}";
        }
    }

    public void Dispose()
    {
        Logger.Writer.Flush();

        Logger.Dispose();
    }

    public string GetTestOutputDirectory(object args = null, [CallerMemberName] string testName = null, bool clean = false)
    {
        if (_testOutputDirectory == null)
        {
            clean |= CleanTestDir;
        }

        _testOutputDirectory ??= getPath();
        if (clean && Directory.Exists(_testOutputDirectory))
        {
            PathUtilities.ForceDeleteDirectory(_testOutputDirectory);
        }

        Directory.CreateDirectory(_testOutputDirectory);

        Logger.LogMessage($"Test output directory:\n{_testOutputDirectory}");
        return _testOutputDirectory;

        string getPath()
        {
            var testRootDir = Path.GetFullPath(Path.Combine(TestRoot, "tests", GetType().Name));
            if (args == null)
            {
                return Path.Combine(testRootDir, testName);
            }
            else
            {
                return Path.Combine(testRootDir, $"{testName}.{args.GetHashCode()}");
            }
        }
    }

    public CodexPage CreateCodexApp(IngestOperation indexOperation,
        Func<CodexAppOptions, CodexAppOptions> updateOptions = null)
    {
        string indexUrl = indexOperation.OutputDirectory;

        return CreateCodexApp(indexUrl, updateOptions);
    }

    public async Task<CodexPage> IndexAsync(
        string directoryToAnalyze,
        Action<AnalyzeOperation> updateAnalyze = null,
        Action<IngestOperation> updateIngest = null,
        bool searchOnly = false,
        [CallerMemberName] string caller = null)
    {
        var outputPath = GetTestOutputDirectory(testName: caller);

        var operation = new AnalyzeOperation()
        {
            RepoName = caller,
            RootDirectory = directoryToAnalyze,
            OutputDirectory = Path.Combine(outputPath, "analyze"),
            DisableOptimization = true,
            Clean = !searchOnly,
            DisableEnumeration = true,
            DisableMsBuild = true,
        };

        updateAnalyze?.Invoke(operation);
        await operation.RunAsync(initializeOnly: searchOnly);

        var ingest = new IngestOperation()
        {
            RepoName = operation.RepoName,
            InputPath = operation.OutputDirectory,
            OutputDirectory = Path.Combine(outputPath, "ingest"),
            Clean = !searchOnly,
            UseStoredFilters = true,
        };

        updateIngest?.Invoke(ingest);

        await ingest.RunAsync(initializeOnly: searchOnly);

        return CreateCodexApp(ingest);
    }

    public IReadOnlyStableIdStorage ReadStableIdStorage(string storageDirectory, [CallerMemberName] string caller = null)
    {
        string outputDir = GetTestOutputDirectory(testName: caller);
        string dbStorageStageDir = Path.Combine(outputDir, "stagedb");

        var storage = new ZoneTreeStableIdStorage(
            storageDirectory,
            dbStorageStageDir);

        storage.Initialize(new StableIdStorageHeader());

        return storage;
    }

    public CodexPage CreateCodexApp(string indexDirectory,
        Func<CodexAppOptions, CodexAppOptions> updateOptions = null)
    {
        var options = new CodexAppOptions();
        options = updateOptions?.Invoke(options) ?? options;

        if (PathUtilities.ToUriOrPath(indexDirectory, out var uri, out var path))
        {
            // For uri's we need to use paging logic
            options.UsePaging = true;
            options.ConfigurePaging = options.ConfigurePaging.ApplyBefore(pc => pc with
            {
                CacheLimit = 100_000,
            });
        }

        ICodex getCodex()
        {
            {
                LuceneConfiguration configuration;

                if (options.UsePaging
                    || options.TrackingClient != null)
                {
                    if (options.ValidatePaging)
                    {
                        var configurePaging = options.ConfigurePaging ?? (pc => pc);
                        options.ConfigurePaging = pc =>
                        {
                            pc = pc with { Validating = true, ValidatingDirectory = options.ValidatingIndexDirectory ?? indexDirectory };
                            pc = configurePaging.Invoke(pc);
                            return pc;
                        };
                    }

                    if (options.TrackingClient != null)
                    {
                        var configurePaging = options.ConfigurePaging ?? (pc => pc);
                        options.ConfigurePaging = pc =>
                        {
                            pc = pc with
                            {
                                UpdateClient = client =>
                                {
                                    var trackingClient = options.TrackingClient;
                                    trackingClient.Inner = client.Value;
                                    client.Value = trackingClient;
                                }
                            };
                            pc = configurePaging.Invoke(pc);

                            return pc;
                        };
                    }

                    configuration = CreatePagingConfigurationAsync(
                        indexDirectory,
                        options.ConfigurePaging).GetAwaiter().GetResult();
                }
                else
                {
                    configuration = new LuceneConfiguration(indexDirectory);
                }

                if (options.AddSourceRetriever)
                {
                    configuration.SourceTextRetriever = new HttpClientSourceTextRetriever();
                }

                configuration.DefaultAccessLevel = ObjectModel.RepoAccess.Internal;

                var codex = new LuceneCodex(configuration);
                return codex;
            }
        }

        var codex = getCodex();

        var app = new MainController();
        app.Controller = new WebViewModelController(app) { TrackSourceReferenceHtml = true };
        app.CodexService = codex;

        return new CodexPage(codex, app, app.Controller.ViewModel);
    }

    public record CodexAppOptions
    {
        public bool AddSourceRetriever { get; set; }
        public string ValidatingIndexDirectory { get; set; }
        public bool UsePaging { get; set; }
        public bool ValidatePaging { get; set; }
        public Func<PagingConfiguration, PagingConfiguration> ConfigurePaging = null;
        public TrackingHttpClient TrackingClient { get; set; }
    }
}
