using System.Text.RegularExpressions;
using static Codex.Utilities.NugetPackageUri;

namespace Codex.Utilities;

using System;
using System.Text.RegularExpressions;
using Codex.ObjectModel;

public record NugetPackageUri(string Id, string? Version = null, string? Source = null)
{
    public static char FileNameSafeSlash = (char)10744; /* '⧸' big solidus character */

    private static readonly Regex RegexPattern = new Regex(
        @"^(?:https?://)?(?:www\.)?(?<service>(dev\.azure\.com)|(visualstudio\.com)|(github\.com))/((?<organization>[^/]+)/(?<project>[^/]+)(?:/_git/(?<repository>[^/]+))?(?:\.git)?)(/.*)?$", RegexOptions.IgnoreCase);

    public static NugetPackageUri Parse(string inputUrl, bool checkShortFormat = true)
    {
        if (TryParse(inputUrl, out NugetPackageUri uri, checkShortFormat))
        {
            return uri;
        }
        else
        {
            throw new ArgumentException($"Invalid URL: '{inputUrl}'");
        }
    }

    public static bool HasUserInfo(string inputUrl, out Uri uri)
    {
        return Uri.TryCreate(inputUrl, UriKind.Absolute, out uri) && !string.IsNullOrEmpty(uri.UserInfo);
    }

    public static string RemoveUserInfo(string inputUrl)
    {
        if (HasUserInfo(inputUrl, out var uri))
        {
            return new UriBuilder(uri)
            {
                UserName = null,
                Password = null
            }.Uri.ToString();
        }

        return inputUrl;
    }

    public static bool TryParse(string inputUrl, out NugetPackageUri uri, bool checkShortFormat = true)
    {
        uri = null;
        if (string.IsNullOrEmpty(inputUrl))
        {
            return false;
        }

        inputUrl = inputUrl.ToLowerInvariant();

        if (inputUrl.StartsWithIgnoreCase("https://"))
        {
            inputUrl = RemoveUserInfo(inputUrl);

            Match match = RegexPattern.Match(inputUrl);
            if (match.Success)
            {
                string service = match.Groups["service"].Value;
                string organization = match.Groups["organization"].Value;
                string project = match.Groups["project"].Value;
                string repository = match.Groups["repository"].Value;

                SourceControlKind kind;
                if (service.ContainsIgnoreCase("dev.azure.com"))
                {
                    kind = SourceControlKind.AzureDevOps;
                }
                else if (service.ContainsIgnoreCase("github.com"))
                {
                    kind = SourceControlKind.GitHub;
                    repository = project;
                }
                else
                {
                    kind = SourceControlKind.Vsts;
                }

                uri = new NugetPackageUri(organization, project, repository);
                return true;

            }
        }
        else if (checkShortFormat)
        {
            var parts = inputUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                uri = new(Id: parts[0]);
            }
            else if (parts.Length == 2)
            {
                uri = new(Id: parts[0], Version: parts[1]);
            }
        }

        return uri != null;
    }

    public string GetBuildTag()
    {
        return "repo=" + GetRepoName().ToLower();
    }

    public string GetUnicodeRepoFileName()
    {
        return GetUnicodeRepoFileName(GetRepoName());
    }

    public static string GetUnicodeRepoFileName(string repoName)
    {
        return repoName?.Replace('\\', '/').Replace('/', FileNameSafeSlash);
    }

    public string GetRepoName()
    {
        if (string.IsNullOrEmpty(Version))
        {
            return Id;
        }

        return $"{Id}/{Version}";

    }

    public string GetUrl()
    {
        return "";
    }
}

