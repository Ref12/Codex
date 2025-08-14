using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Codex.Utilities;

namespace Codex.Web.Utilities
{
    public static class WebHelper
    {
        /// <summary>
        /// Returns the string encoded with <see cref="HttpUtility.HtmlEncode(string)"/>
        /// </summary>
        public static string AsHtmlEncoded(this string s)
        {
            return HttpUtility.HtmlEncode(s);
        }

        public static Uri ToQueryString<TValue>(IDictionary<string, TValue> queryParams, Func<TValue, string> toString = null)
        {
            toString ??= o => o.ToString();
            if (queryParams.Count != 0)
            {
                return new Uri($"?{string.Join("&", queryParams.Select(e => $"{e.Key}={HttpUtility.UrlEncode(toString(e.Value))}"))}", UriKind.Relative);
            }
            else
            {
                return new Uri("", UriKind.Relative);
            }
        }

        public static Uri AsQueryUri<T>(T obj)
        {
            var json = JsonSerializer.SerializeToNode<T>(obj, JsonSerializationUtilities.GetOptions());

            return ToQueryString(json.AsObject());
        }

        public static string AsQueryString<T>(T obj)
        {
            return AsQueryUri(obj).ToString();
        }

        public static T FromQueryString<T>(string queryString)
        {
            var queryParams = HttpUtility.ParseQueryString(queryString);
            var jsonObj = new JsonObject(
                queryParams.OfType<string>()
                .Select(s => KeyValuePair.Create(s, (JsonNode)queryParams[s])));

            return jsonObj.Deserialize<T>(JsonSerializationUtilities.GetOptions());
        }
    }
}