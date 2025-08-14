using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Codex.Utilities;

namespace Codex.Web.Common;

public class NuGetPackageDownloader
{
    private const string NuGetApiUrl = "https://api.nuget.org/v3-flatcontainer";

    public record struct PackageDownloadDetails(string PackageId, string PackageVersion, string DownloadPath);

    public static async Task<PackageDownloadDetails> DownloadLatestPackage(string packageId, string targetPath)
    {
        var originalPackageId = packageId;
        packageId = packageId.ToLowerInvariant();
        string packageVersion = await GetLatestPackageVersion(packageId);

        var downloadPath = await DownloadPackageVersion(packageId, packageVersion, targetPath);

        return new PackageDownloadDetails(originalPackageId, packageVersion, downloadPath);
    }

    public static async Task<string> DownloadPackageVersion(string packageId, string packageVersion, string targetPath, bool targetPathIsFile = false)
    {
        string packageFilePath = targetPathIsFile ? targetPath : Path.Combine(targetPath, $"{packageId}.{packageVersion}.nupkg");
        string packageDownloadUrl = $"{NuGetApiUrl}/{packageId}/{packageVersion}/{packageId}.{packageVersion}.nupkg".ToLowerInvariant();

        //packageDownloadUrl = $"https://www.nuget.org/api/v2/package/{packageId}/{packageVersion}";

        Placeholder.Trace2(targetPath);

        var packageBytes = await SdkFeatures.HttpClient.GetByteArrayAsync(packageDownloadUrl);

        Placeholder.Trace2();
        using (var fs = File.Open(packageFilePath, FileMode.Create))
        {
            await fs.WriteAsync(packageBytes);
        }

        return packageFilePath;
    }

    public static async Task<string> GetLatestPackageVersion(string packageId)
    {
        string url = $"{NuGetApiUrl}/{packageId.ToLower()}/index.json";
        Placeholder.Trace2(url);
        using (HttpClient client = new HttpClient())
        {
            Placeholder.Trace2(url);

            var metadataJson = await SdkFeatures.HttpClient.GetStringAsync(url, defaultOnFailure: true);

            Placeholder.Trace2(url);
            if (!string.IsNullOrEmpty(metadataJson))
            {
                var metadata = JsonNode.Parse(metadataJson);
                return (string)metadata["versions"].AsArray().Last();
            }
        }

        return null;
    }
}
