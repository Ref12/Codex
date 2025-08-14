using System.Web;

namespace Codex.Utilities;

public record BuildUri(Uri OrganizationUri, string Project)
{
    public int? BuildId { get; set; }
    public int? DefinitionId { get; set; }
    public string? SourceBranch { get; set; }
    public string? PersonalAccessToken { get; set; }

    public string GetBuildWebUrl()
    {
        return $"{OrganizationUri}{Project}/_build/results?buildId={BuildId}";
    }

    public string GetDefinitionWebUrl()
    {
        return $"{OrganizationUri}{Project}/_build?definitionId={DefinitionId}";
    }

    public string GetGitUrl(string repoName)
    {
        return $"{OrganizationUri}{Project}/_git/{repoName}";
    }

    public BuildUri WithBuildId(int? buildId)
    {
        return this with { BuildId = buildId };
    }

    public static BuildUri ParseBuildUri(string buildUrl)
    {
        var uri = new UriBuilder(buildUrl);
        var parameters = HttpUtility.ParseQueryString(uri.Query);
        var buildId = parameters["buildId"];
        var definitionId = parameters["definitionId"];
        var pat = uri.UserName.AsNotEmptyOrNull();
        if (string.IsNullOrEmpty("definitionId"))
        {
            definitionId = parameters["pipelineId"] ?? definitionId;
        }

        string project = null;
        if (uri.Host.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var projectStart = uri.Path.IndexOf('/', 1) + 1;
            project = uri.Path[projectStart..uri.Path.IndexOf('/', projectStart)];
            uri.Path = uri.Path.Substring(0, projectStart);
        }
        else
        {
            project = uri.Path[1..uri.Path.IndexOf('/', 1)];
            uri.Path = null;
        }

        uri.UserName = null;
        uri.Password = null;

        uri.Query = null;
        return new BuildUri(uri.Uri, project)
        {
            BuildId = ParseId(buildId),
            DefinitionId = ParseId(definitionId),
            PersonalAccessToken = pat
        };
    }

    public static int? ParseId(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        return int.Parse(id);
    }
}
