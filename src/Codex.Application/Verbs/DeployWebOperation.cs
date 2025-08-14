using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using Codex.Configuration;
using Codex.Sdk;
using Codex.Utilities;
using Microsoft.Net.Http.Headers;

namespace Codex.Application.Verbs;

using static CodexConstants;
using static SdkPathUtilities;

[Verb("deployweb", HelpText = "Deploy Codex web files.")]
public record DeployWebOperation : OperationBase
{
    public enum AppContentHost
    {
        cloudflare,
        pages
    }

    public enum AppMode
    {
        wasm,
        server
    }

    [Option('s', "source", Required = true, HelpText = "The source directory to copy from. (corresponds to merge directory for linux overlay mount)")]
    public string SourceDirectory { get; set; }

    [Option('t', "target", Required = true, HelpText = "The target directory to copy to. (corresponds to lower directory for linux overlay mount)")]
    public string TargetDirectory { get; set; }

    [Option('h', "host", Required = true, Default = AppContentHost.cloudflare, HelpText = "The host for the sites static content")]
    public AppContentHost Host { get; set; } = AppContentHost.cloudflare;

    [Option('m', "mode", Required = true, HelpText = "The execution mode")]
    public AppMode Mode { get; set; }

    [Option('i', "index", Required = true, HelpText = "The source for index files or api. If --use-custom-source is specified, this points to a file containing the index source json.")]
    public string IndexSource { get; set; }

    [Option("use-custom-source", Required = false, HelpText = "Indicates that IndexSource points to a file containing custom index source json info.")]
    public bool UseCustomSource { get; set; }

    [Option('g', "ignore", Required = false, HelpText = "The ignore file indicating files not copy")]
    public string? IgnoreFile { get; set; }

    [Option('c', "clean", Required = false, Default = true, HelpText = "Clean the target prior to deployment")]
    public bool Clean { get; set; } = true;

    public FileSystemSpec SourceFs;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        SourceDirectory = Path.GetFullPath(SourceDirectory);
        TargetDirectory = Path.GetFullPath(TargetDirectory);

        SourceFs = SourceDirectory;
    }

    protected override async ValueTask<int> ExecuteAsync()
    {
        if (Clean)
        {
            PathUtilities.ForceDeleteDirectory(TargetDirectory);
        }

        Directory.CreateDirectory(TargetDirectory);

        var copies = new List<Action>();
        var ignoreLines = new List<string>();
        if (!string.IsNullOrEmpty(IgnoreFile))
        {
            ignoreLines.AddRange(File.ReadLines(IgnoreFile));
        }

        ignoreLines.Add("/overlay/");

        var location = new IndexSourceLocation()
        {
            Url = IndexSource,
            ReloadHeader = HeaderNames.LastModified,
        };

        if (Mode == AppMode.server)
        {
            // Remove managed assemblies
            ignoreLines.Add("/_framework/");
            ignoreLines.Add("/managed/");
            ignoreLines.Add("*.dll*");
            ignoreLines.Add("*.pdb*");
            ignoreLines.Add("*.wasm");

            IndexSource = IndexSource.TrimEnd('/');
            copies.Add(() => CopyFile("overlay/clientmain.js", "main.js", transformText: s => s.Replace("$(PageApiRoot)", IndexSource)));
        }
        else
        {
            if (Host == AppContentHost.cloudflare)
            {
                copies.Add(() => CopyFile("overlay/cloudflare.wasm.headers.txt", "_headers", generateCompressionVariants: false, transformText: s =>
                {
                    return s.ReplaceIgnoreCase("$(LastModified)", HeaderUtilities.FormatDate(location.Timestamp, quoted: false));
                }));
            }
            else
            {
                copies.Add(() => CopyFile("ts/js/serviceworker.boot.js", "serviceworker.boot.js"));
            }
        }


        copies.Add(() => CopyFile(
            UseCustomSource ? IndexSource : CodexSourceFileName,
            CodexSourceFileName,
            transformText: s => location.SerializeEntity(flags: JsonFlags.Indented)));

        var ignore = GitIgnore.Parse(ignoreLines);

        await CopyFilesRecursiveAsync(
            sourceDirectory: SourceFs,
            targetDirectory: TargetDirectory,
            logCopy: Logger.WriteLine,
            shouldCopy: ignore.Includes);

        foreach (var copy in copies)
        {
            copy();
        }

        return 0;
    }

    protected void CopyFile(string source, string? target = null, Func<string, string>? transformText = null, bool generateCompressionVariants = true)
    {
        target ??= source;

        var targetFile = Path.Combine(TargetDirectory, target);

        Logger.WriteLine($"Copying '{source}' to '{targetFile}'...");
        if (transformText != null)
        {
            var text = SourceFs.FS.OpenFile(source).ReadAllText();
            text = transformText.Invoke(text);
            File.WriteAllText(targetFile, text);
        }
        else
        {
            using var sourceStream = SourceFs.FS.OpenFile(source);
            using var targetStream = File.OpenWrite(targetFile);
            sourceStream.CopyTo(targetStream, 1 << 16);
        }

        Logger.WriteLine($"Copied '{source}' to '{targetFile}'.");

        if (generateCompressionVariants)
        {
            void compress(Func<Stream, Stream> getCompressionStream, string ext)
            {
                var cmpTargetFile = $"{targetFile}.{ext}";
                using var sourceStream = File.OpenRead(targetFile);
                using var targetStream = getCompressionStream(File.OpenWrite(cmpTargetFile));

                sourceStream.CopyTo(targetStream, 1 << 16);

                Logger.WriteLine($"Created compressed file: '{cmpTargetFile}'.");
            }

            compress(s => new BrotliStream(s, CompressionLevel.Optimal), "br");
            compress(s => new GZipStream(s, CompressionLevel.Optimal), "gz");
        }
    }

    public const string DefaultIngore = """
    _framework/
    managed/
    *.pdb.*
    *.dll.*
    *.wasm.*

    """;
}

