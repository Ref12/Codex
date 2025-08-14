using System.Text.RegularExpressions;
using static Codex.Utilities.SourceControlUri;

namespace Codex.Utilities;

using System;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;

public enum SourceControlKind
{
    AzureDevOps,
    Vsts,
    GitHub
}

public record SourceControlUri(string Organization, string Project, string Repository, SourceControlKind Kind)
{
    public static char FileNameSafeSlash = (char)10744; /* '⧸' big solidus character */

    private static readonly Regex RegexPattern = new Regex(
        @"^(?:https?://)?((?:www\.)|(?<organization>[^\.]+))?(?<service>(dev\.azure\.com)|(\.visualstudio\.com)|(github\.com))/((?<organization>[^/]+)/(?<project>[^/]+)(?:/_git/(?<repository>[^/]+))?(?:\.git)?)(/.*)?$", RegexOptions.IgnoreCase);

    private static readonly Regex VstsRegexPattern = new Regex(
        @"^(?:https?://)?(?<organization>[^\.]+)\.visualstudio\.com/((?<project>[^/]+)(?:/_git/(?<repository>[^/]+))?(?:\.git)?)(/.*)?$", RegexOptions.IgnoreCase);


    private static readonly Regex RepoNameRegex = new Regex(
        @"^(?<organization>[^/]+)(/(?<project>[^/]+))?/(?<repository>[^/]+)$", RegexOptions.IgnoreCase);

    public static SourceControlUri ParseName(string inputUrl)
    {
        return Parse(inputUrl, checkRepoNameFormat: true);
    }

    public static SourceControlUri Parse(string inputUrl, bool checkRepoNameFormat = false)
    {
        if (TryParse(inputUrl, out SourceControlUri uri, checkRepoNameFormat))
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
    public static bool TryParseName(string inputUrl, out SourceControlUri uri)
    {
        return TryParse(inputUrl, out uri, checkRepoNameFormat: true);
    }

    public static bool TryParse(string inputUrl, out SourceControlUri uri, bool checkRepoNameFormat = false)
    {
        uri = null;
        if (string.IsNullOrEmpty(inputUrl))
        {
            return false;
        }

        inputUrl = inputUrl.ToLowerInvariant();

        if (inputUrl.StartsWithIgnoreCase("h"))
        {
            inputUrl = RemoveUserInfo(inputUrl);

            Match match = inputUrl.ContainsIgnoreCase(".visualstudio.com/") 
                ? VstsRegexPattern.Match(inputUrl)
                : RegexPattern.Match(inputUrl);
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

                uri = new SourceControlUri(organization, project, repository, kind);
                return true;

            }
        }

        if (checkRepoNameFormat)
        {
            Match match = RepoNameRegex.Match(inputUrl);
            if (match.Success)
            {
                string organization = match.Groups["organization"].Value;
                string project = match.Groups["project"].Value;
                string repository = match.Groups["repository"].Value;

                SourceControlKind kind;
                if (!string.IsNullOrEmpty(project))
                {
                    kind = SourceControlKind.AzureDevOps;
                }
                else
                {
                    kind = SourceControlKind.GitHub;
                    project = repository;
                }

                uri = new SourceControlUri(organization, project, repository, kind);
                return true;
            }
        }

        return false;
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
        if (Repository == null)
        {
            return null;
        }

        switch (Kind)
        {
            case SourceControlKind.AzureDevOps:
            case SourceControlKind.Vsts:
                return $"{Organization}/{Project}/{Repository}";
            case SourceControlKind.GitHub:
                return $"{Organization}/{Repository}";
            default:
                return null;
        }
    }

    public string GetKindPathFragment()
    {
        switch (Kind)
        {
            case SourceControlKind.AzureDevOps:
            case SourceControlKind.Vsts:
                return $"azuredevops";
            case SourceControlKind.GitHub:
                return $"github";
            default:
                throw new NotSupportedException($"Unsupported source control service: {Kind}");
        }
    }

    public string GetContentByCommit(string commit, string repoRelativePath)
    {
        if (string.IsNullOrEmpty(commit)) return null;

        repoRelativePath = repoRelativePath.AsUrlRelativePath();
        switch (Kind)
        {
            case SourceControlKind.AzureDevOps:
            case SourceControlKind.Vsts:
                return $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repository}/items?path={repoRelativePath}&versionDescriptor.version={commit}&versionDescriptor.versionType=commit&api-version=6.0";
            case SourceControlKind.GitHub:
                return $"https://raw.githubusercontent.com/{Organization}/{Repository}/{commit}/{repoRelativePath}";
            default:
                throw new NotSupportedException($"Unsupported source control service: {Kind}");
        }
    }

    public string GetApiUrlByCommit(string commit, string repoRelativePath)
    {
        switch (Kind)
        {
            case SourceControlKind.AzureDevOps:
            case SourceControlKind.Vsts:
                return GetContentByCommit(commit, repoRelativePath);
            case SourceControlKind.GitHub:
                repoRelativePath = repoRelativePath.AsUrlRelativePath();
                return $"https://api.github.com/repos/{Organization}/{Repository}/contents/{repoRelativePath}?ref={commit}";
            default:
                throw new NotSupportedException($"Unsupported source control service: {Kind}");
        }
    }

    public SourceControlUri Normalize()
    {
        var normalizedKind = GetNormalizedKind(Kind);
        if (normalizedKind == Kind) return this;
        return this with { Kind = normalizedKind };
    }

    public static SourceControlKind GetNormalizedKind(SourceControlKind kind)
    {
        if (kind == SourceControlKind.Vsts)
        {
            return SourceControlKind.AzureDevOps;
        }

        return kind;
    }

    public string GetApiUrl(string gitSha)
    {
        if (string.IsNullOrEmpty(gitSha))
        {
            return null;
        }

        switch (Kind)
        {
            case SourceControlKind.AzureDevOps:
            case SourceControlKind.Vsts:
                return $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repository}/blobs/{gitSha}";
            case SourceControlKind.GitHub:
                return $"https://api.github.com/repos/{Organization}/{Repository}/git/blobs/{gitSha}";
            default:
                throw new NotSupportedException($"Unsupported source control service: {Kind}");
        }
    }

    public string GetFileUrlByCommit(string repoRelativePath, string commitId)
    {
        if (string.IsNullOrEmpty(commitId)) return null;

        switch (Kind)
        {
            case SourceControlKind.AzureDevOps:
            case SourceControlKind.Vsts:
                return $"https://{Organization}.visualstudio.com/{Project}/_git/{Repository}?path={repoRelativePath}&version=GC{commitId}";
            case SourceControlKind.GitHub:
                return $"https://github.com/{Organization}/{Repository}/blob/{commitId}/{repoRelativePath.AsUrlRelativePath()}";
            default:
                throw new NotSupportedException($"Unsupported source control service: {Kind}");
        }
    }

    public string GetFileUrlByBranch(string repoRelativePath, string branch)
    {
        repoRelativePath = repoRelativePath.AsUrlRelativePath();
        switch (Kind)
        {
            case SourceControlKind.AzureDevOps:
            case SourceControlKind.Vsts:
                var versionParam = string.IsNullOrEmpty(branch) ? "" : $"&version=GB{branch}";
                return $"https://{Organization}.visualstudio.com/{Project}/_git/{Repository}?path={repoRelativePath}{versionParam}";
            case SourceControlKind.GitHub:
                branch = string.IsNullOrEmpty(branch) ? "main" : branch;
                return $"https://github.com/{Organization}/{Repository}/blob/{branch.AsUrlRelativePath()}/{repoRelativePath}";
            default:
                throw new NotSupportedException($"Unsupported source control service: {Kind}");
        }
    }

    public string GetUrl()
    {
        switch (Kind)
        {
            case SourceControlKind.AzureDevOps:
                return $"https://dev.azure.com/{Organization}/{Project}/_git/{Repository}";
            case SourceControlKind.Vsts:
                return $"https://{Organization}.visualstudio.com/{Project}/_git/{Repository}";
            case SourceControlKind.GitHub:
                return $"https://github.com/{Organization}/{Repository}";
            default:
                throw new NotSupportedException($"Unsupported source control service: {Kind}");
        }
    }

    public static SourceControlUri[]? ParseRepoList(string repoList, string separator = ",")
    {
        try
        {
            return repoList.Split(separator).SelectArray(s1 => ParseName(s1));
        }
        catch
        {
            return null;
        }
    }
}

public static class SourceControlUriExtensions
{
    public static string GetRepoListString(this IEnumerable<SourceControlUri>? uris, string separator = ",")
    {
        if (uris == null) return string.Empty;
        return string.Join(separator, uris.Select(r => r.GetRepoName())); 
    }
}

