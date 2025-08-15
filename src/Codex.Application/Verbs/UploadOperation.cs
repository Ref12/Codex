using Codex.Lucene.Search;
using System.ComponentModel;
using Codex.Storage.Store;
using Codex.Utilities;
using Codex.Sdk;
using Codex.Storage;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.ContractsLight;

namespace Codex.Application.Verbs;

[Verb("upload", HelpText = "Upload index files to Azure storage.")]
public record UploadOperation : IndexReadOperationBase
{
    [Option('s', "source", Required = true, HelpText = "The source directory to upload.")]
    public string UploadDirectory { get; set; }

    [Option('t', "target", Required = true, HelpText = "Sas url to upload to.")]
    public string BlobContainerSasUrl { get; set; }

    [Option('e', "exclude", HelpText = "Specifies files to exclude from upload")]
    public IList<string> ExcludedFiles { get; set; } = new List<string>();

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        UploadDirectory = Path.GetFullPath(UploadDirectory);

        if (!Uri.TryCreate(BlobContainerSasUrl, UriKind.Absolute, out var blobUri))
        {
            var builder = new ConfigurationBuilder()
                .AddUserSecrets<UploadOperation>();

            var config = builder.Build();
            var secretName = BlobContainerSasUrl;
            BlobContainerSasUrl = config[secretName];

            Contract.Check(!string.IsNullOrEmpty(BlobContainerSasUrl))
                ?.Assert($"Blob sas url is required. Could not find '{secretName}' in secrets configuration.");

            Logger.LogMessage("Found secret.");
        }
    }

    protected override async ValueTask<int> ExecuteAsync()
    {
        if (!string.IsNullOrEmpty(BlobContainerSasUrl))
        {
            var storage = new BlobObjectStorage(Logger, BlobContainerSasUrl);
            storage.Initialize();

            Logger.LogMessage($"Uploading to '{storage.Folder.AccountName}' blob folder '{storage.Folder.BlobContainerName}'");

            await storage.UploadDirectory(UploadDirectory, ExcludedFiles);
        }

        return 0;
    }
}
