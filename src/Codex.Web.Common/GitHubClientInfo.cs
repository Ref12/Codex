using System.Text.RegularExpressions;

namespace Codex.Web
{
    public record GitHubClientInfo(string Id, string Secret)
    {
        public static readonly GitHubClientInfo DefaultKeys = new GitHubClientInfo("Github-Client-Id", "Github-Client-Secret");

        public static readonly Regex ClientKeyRegex = new Regex(@"^(.*\.)?(?<domain>\w+\.\w+)$");

        public static string GetDomain(string url)
        {
            var host = url;
            if (url.Contains(":"))
            {
                var uri = new Uri(url);
                host = uri.Host;
            }
            var match = ClientKeyRegex.Match(host);
            var domain = match.Groups["domain"].Value;
            return domain;
        }

        public static GitHubClientInfo GetClientKeys(string url, bool legacy = false)
        {
            var domain = GetDomain(url);
            var part = domain.Replace('.', '-').ToLower();

            if (legacy)
            {
                return new ($"{part}-Github-Client-Id", $"{part}-Github-Client-Secret");
            }
            else
            {
                return new($"Github-Client-{part}-Id", $"Github-Client-{part}-Secret");
            }
        }
    }
}
