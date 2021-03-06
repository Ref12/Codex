using CommandLine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.IO.Compression;
using CommandLine.Text;
using Newtonsoft.Json;
using Codex.Downloader;
using Codex.Storage;
using System.Collections;

namespace Codex.Ingester
{
    class Program
    {
        class Options
        {
            [Option("file", HelpText = "The location of a json file containing definition of repo analysis output locations.")]
            public string DefinitionsFile { get; set; }

            [Option("out", Required = false, HelpText = "The temporary location to store analysis outputs before uploading.")]
            public string OutputFolder { get; set; }

            [Option("incremental", HelpText = "Specifies if files should not be downloaded if already present at output location.")]
            public bool Incremental { get; set; }

            [Option("name", Required = false, HelpText = "The name of the repository to create.")]
            public string RepoName { get; set; }

            [Option("es", HelpText = "The URL of the elasticsearch instance.")]
            public string ElasticSearchUrl { get; set; }

            [Option("newBackend", HelpText = "Specifies if new elasticsearch backend should be used.")]
            public bool NewBackend { get; set; }

            [Option("preview", HelpText = "Specifies whether to preview downloading of build results.")]
            public bool Preview { get; set; }

            [Option("debug", HelpText = "Launches debugger on start.")]
            public bool Debug { get; set; }

            [Option("printEnv", HelpText = "Prints all environment variables.")]
            public bool PrintEnvironmentVariables { get; set; }

            [Option("noResultFilter", HelpText = "Do not filter builds by result. Only use 'CodexOutputs' tag.")]
            public bool IgnoreResultFilter { get; set; }
        }

        static void Main(string[] args)
        {
            new CommandLine.Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.CaseSensitive = false;
                settings.HelpWriter = Console.Out;
            }).ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<Options>((errs) => HandleParseError(errs));
        }

        private static async Task RunAsync(Options options)
        {
            if (options.PrintEnvironmentVariables)
            {
                Console.WriteLine($"Environment Variables:");

                foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
                {
                    Console.WriteLine($"{envVar.Key}={envVar.Value}");
                }

                Console.WriteLine($"Done printing environment variables.");
            }

            if (options.OutputFolder == null)
            {
                options.Preview = true;
            }

            if (options.Debug)
            {
                Debugger.Launch();
            }

            if (!string.IsNullOrEmpty(options.DefinitionsFile))
            {
                // Read the json file
                RepoList repoList = JsonConvert.DeserializeObject<RepoList>(File.ReadAllText(options.DefinitionsFile));

                // Download each of the repos to a subfolder with the repo name
                // (maybe also in-proc)

                if (!options.Preview)
                {
                    Directory.CreateDirectory(options.OutputFolder);
                }

                foreach (var repo in repoList.repos)
                {
                    string destination = null;
                    if (!options.Preview)
                    {
                        destination = Path.Combine(options.OutputFolder, repo.name + ".zip");
                        if (options.Incremental && File.Exists(destination))
                        {
                            continue;
                        }
                    }

                    await DownloaderProgram.RunAsync(new DownloaderProgram.VSTSBuildOptions()
                    {
                        BuildDefinitionId = repo.id,
                        CollectionUri = repo.url,
                        Destination = destination,
                        ProjectName = repo.project,
                        PersonalAccessToken = GetPersonalAccessToken(options, repo.pat),
                        Preview = options.Preview,
                        IgnoreResultFilter = options.IgnoreResultFilter || repo.ignoreResultFilter || repoList.ignoreResultFilter
                    });
                }
            }

            // Run codex.exe (maybe just launch in-proc)
            // specifying the root folder and a mode indicating that
            // they all go into the same partition

            if (!options.Preview && options.ElasticSearchUrl != null && options.RepoName != null)
            {
                new CodexStorageApplication().Ingest(
                    options.RepoName,
                    options.ElasticSearchUrl,
                    options.OutputFolder,
                    true,
                    useNewBackend: options.NewBackend);
            }

            // TODO: Make disable finalize command line option
            // TODO: Maybe have handling to ensure that same code is not uploaded more
            // than once
        }

        private static string GetPersonalAccessToken(Options options, string pat)
        {
            var result = Environment.ExpandEnvironmentVariables(pat);
            return result;
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            var sb = SentenceBuilder.Create();
            foreach (var error in errors)
            {
                if (error.Tag != ErrorType.HelpRequestedError)
                {
                    Console.Error.WriteLine(sb.FormatError(error));
                }
            }
        }

        private static void RunOptionsAndReturnExitCode(Options options)
        {
            RunAsync(options).GetAwaiter().GetResult();
        }
    }
}
