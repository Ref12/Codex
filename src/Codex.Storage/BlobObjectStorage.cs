using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Codex.Logging;
using Codex.Utilities;

namespace Codex.Storage;

public record BlobObjectStorage(Logger Logger, string BlobSasUrl) : IObjectStorage
{
    public BlobClient Folder { get; set; } = new BlobClient(new Uri(BlobSasUrl));

    public string FolderPath => PathUtilities.UriCombine(Folder.BlobContainerName, Folder.Name);
    public void Dispose()
    {
    }

    public void Finalize(string message)
    {
    }


    public void Initialize()
    {
        var blobUri = new BlobUriBuilder(new Uri(BlobSasUrl));

    }

    private BlobClient GetBlobClient(string relativePath)
    {
        var relativePathUrl = PathUtilities.UriCombine(Folder.Name, PathUtilities.AsUrlRelativePath(relativePath, encode: false));
        return Folder.GetParentBlobContainerClient().GetBlobClient(relativePathUrl);
    }

    public Stream Load(string relativePath)
    {
        throw new NotImplementedException();
    }

    public string Write(string relativePath, MemoryStream stream)
    {
        throw new NotImplementedException();
    }

    public async ValueTask<string> WriteAsync(string relativePath, MemoryStream stream)
    {
        Placeholder.Todo("Need to handle errors");
        Placeholder.Todo("Use space preserving storage (i.e. reuse chunks) when used in a historical indexing context");
        Placeholder.Todo("Avoid uploading if blob is unchanged");
        stream.Position = 0;

        var blobClient = GetBlobClient(relativePath);

        var response = await blobClient.UploadAsync(stream, overwrite: true);

        return blobClient.Name;
    }

    public async Task UploadDirectory(string localDirectory, IEnumerable<string> excludedFiles = null)
    {
        var files = Directory.EnumerateFiles(localDirectory, "*",
            new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden,
                RecurseSubdirectories = true,
            });

        excludedFiles ??= Array.Empty<string>();

        var excludesFilesSet = excludedFiles
            .Select(p => Path.GetFullPath(Path.Combine(localDirectory, p.NormalizeSlashes())))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(files, async (file, token) =>
        {
            var relativePath = PathUtilities.GetRelativePath(localDirectory, file);
            if (excludesFilesSet.Contains(file))
            {
                Logger.LogMessage($"Skipping: {relativePath}");
                return;
            }

            var blobClient = GetBlobClient(relativePath);


            Logger.LogMessage($"Uploading: {relativePath}");
            await blobClient.UploadAsync(file, overwrite: true);
            if (MimeMapping.Instance.TryGetContentType(relativePath, out var contentType))
            {
                await blobClient.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders()
                {
                    ContentType = contentType
                });
            }

            Logger.LogMessage($"Uploaded: {relativePath}");
        });
    }
}
