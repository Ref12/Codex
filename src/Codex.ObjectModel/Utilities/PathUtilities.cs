using System.Diagnostics;
using System.Net;
using Codex.Sdk;

namespace Codex.Utilities
{
    /// <summary>
    /// Utility methods for paths and Uris (including relativizing and derelativizing)
    /// </summary>
    public static partial class PathUtilities
    {
        private static readonly string NormalizingPathRoot = Path.GetFullPath("//NORMALIZING_PATH_ROOT/");

        public const string AppendSuffix = "###append";

        public static readonly ReadOnlyMemory<char> InvalidFileNameChars = Path.GetInvalidFileNameChars();

        public static bool ToUriOrPath(string pathOrUri, out Uri uri, out string path, string parentPath = "")
        {
            var candidateUri = new Uri(pathOrUri, UriKind.RelativeOrAbsolute);
            return ToUriOrPath(candidateUri, out uri, out path, parentPath);
        }

        public static bool ToUriOrPath(Uri candidateUri, out Uri uri, out string path, string parentPath = "")
        {
            if (!candidateUri.IsAbsoluteUri || candidateUri.Scheme == "file")
            {
                var basePath = candidateUri.IsAbsoluteUri
                    ? candidateUri.GetComponents(UriComponents.Path, UriFormat.Unescaped)
                    : candidateUri.OriginalString;

                path = Path.Combine(parentPath, basePath);
                uri = default;
                return false;
            }
            else
            {
                uri = candidateUri;
                path = default;
                return true;
            }
        }

        public static Url Combine(this Url uri, string relativeUri)
        {
            var builder = uri.UriBuilder;
            builder.Path = UriCombine(builder.Path, relativeUri);
            return builder.Uri;
        }

        public static Uri Combine(this Uri uri, string relativeUri)
        {
            var builder = new UriBuilder(uri);
            builder.Path = UriCombine(builder.Path, relativeUri);
            return builder.Uri;
        }

        public static Uri RemoveQuery(Url uri)
        {
            return new Uri(uri.Uri.GetLeftPart(UriPartial.Path));
        }

        public static Uri WithoutQuery(this Uri uri)
        {
            return RemoveQuery(uri);
        }

        public static string NormalizePath(string path, char? directorySeparator = null)
        {
            path = Path.GetFullPath(Path.Combine(NormalizingPathRoot, path));
            path = path.TrimStartIgnoreCase(NormalizingPathRoot);
            if (directorySeparator != null)
            {
                path = path.Replace(Path.DirectorySeparatorChar, directorySeparator.Value);
            }

            return path;
        }

        public static bool IsAbsoluteUri(string uri)
        {
            return Uri.IsWellFormedUriString(uri, UriKind.Absolute);
        }

        public static bool IsValidFileName(string name)
        {
            return name.AsSpan().IndexOfAny(InvalidFileNameChars.Span) < 0;
        }

        public static void ForceDeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;

            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                info.Attributes = FileAttributes.Normal;
            }

            directory.Delete(true);
        }

        public static string[] GetDirectoryFilesSafe(string directory, string searchPattern, SearchOption option)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(directory, searchPattern, option);
        }

        public static string[] GetAllFilesRecursive(string directory)
        {
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        }

        public static string[] GetAllRelativeFilesRecursive(string directory, string rootDirectory = null, bool recursive = true, string searchPattern = "*")
        {
            rootDirectory ??= directory;
            directory = Path.GetFullPath(directory);
            rootDirectory = Path.GetFullPath(rootDirectory);
            return Directory.GetFiles(directory, searchPattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .SelectArray(f => GetRelativePath(rootDirectory, f));
        }

        public static void CreateParentDirectory(string filePath) => Directory.CreateDirectory(Path.GetDirectoryName(filePath));

        public static string UriCombine(string baseUri, string relativeUri, bool normalize = false)
        {
            string path;
            if (string.IsNullOrEmpty(relativeUri))
            {
                path = baseUri;
            }
            else if (string.IsNullOrEmpty(baseUri))
            {
                path = relativeUri.TrimStart('/');
            }
            else if (relativeUri.Contains(':'))
            {
                // This is actually a full uri. Just return it.
                path = relativeUri;
            }
            else
            {
                path = $"{baseUri.TrimEnd('/')}/{relativeUri.TrimStart('/')}";
            }

            if (normalize)
            {
                path = NormalizePath(path, '/');
            }

            return path;
        }

        public static string AppendQuery(string uri, string query)
        {
            var trimmedQuery = query.AsSpan().TrimStart("?&");
            if (trimmedQuery.Length == 0) return uri;

            var separator = uri.Contains("?") ? "&" : "?";
            return $"{uri}{separator}{trimmedQuery}";
        }

        public static string AsUrlRelativePath(this string relativePath, bool encode = true)
        {
            relativePath = relativePath.Trim().TrimStart(PathSeparatorChars).Replace('\\', '/');
            return encode ? WebUtility.UrlEncode(relativePath) : relativePath;
        }

        /// <summary>
        /// Indicates that a relative path is same as the base path
        /// </summary>
        public const string CurrentDirectoryRelativePath = @"./";

        private static readonly char[] PathSeparatorChars = new char[] { '\\', '/' };

        public static bool EqualsIgnoreCase(this string s1, string s2)
        {
            return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetFileName(string path)
        {
            return path.Substring(path.LastIndexOfAny(PathSeparatorChars) + 1);
        }

        public static string GetRelativePath(string directory, string path)
        {
            string result = null;
            if (!string.IsNullOrEmpty(directory) && path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            {
                result = path.Substring(directory.Length).TrimStart('/', '\\');
            }

            return result;
        }

        public static string GetExtension(string path)
        {
            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i] == '\\')
                {
                    return string.Empty;
                }

                if (path[i] == '.')
                {
                    return path.Substring(i);
                }
            }

            return string.Empty;
        }

        public static string GetDirectoryName(this string path)
        {
            var index = path.AsSpan().LastIndexOfAny('\\', '/');
            return path.Substring(0, index > 0 ? index : 0);
        }

        /// <summary>
        /// Ensures that the path has a trailing slash at the end
        /// </summary>
        /// <param name="path">the path</param>
        /// <returns>the path ending with a trailing slash</returns>
        public static string EnsureTrailingSlash(string path)
        {
            return PathUtilities.EnsureTrailingSlash(path, Path.DirectorySeparatorChar);
        }

        public static Uri EnsureTrailingSlash(this Uri uri)
        {
            var ub = new UriBuilder(uri);
            ub.Path = ub.Path.AsNotEmptyOrNull()?.FluidSelect(s => EnsureTrailingSlash(s, '/')) ?? "/";
            return ub.Uri;
        }

        /// <summary>
        /// Ensures that the path has a trailing slash at the end
        /// </summary>
        /// <param name="path">the path</param>
        /// <param name="separatorChar">the trailing separator char</param>
        /// <returns>the path ending with a trailing slash</returns>
        public static string EnsureTrailingSlash(string path, char separatorChar)
        {
            if (path[path.Length - 1] != separatorChar)
            {
                return path + separatorChar;
            }

            return path;
        }

        public static string RemoveLeadingSlash(this string path)
        {
            return path.TrimStart(PathSeparatorChars);
        }

        public static string TrimTrailingSlash(this string path)
        {
            return path.TrimEnd(PathSeparatorChars);
        }
    }
}