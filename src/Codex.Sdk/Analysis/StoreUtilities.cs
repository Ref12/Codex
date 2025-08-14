using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Codex.ObjectModel;
using System;
using System.Text.RegularExpressions;

namespace Codex.Utilities
{

    public static class StoreUtilities
    {
        public static string GetSafeRepoName(string repoName)
        {
            var safeName = repoName
                .Replace('#', '_')
                .Replace('.', '_')
                .Replace(',', '_')
                .Replace(' ', '_')
                .Replace('\\', '_')
                .Replace('/', '_')
                .Replace('+', '_')
                .Replace('*', '_')
                .Replace('?', '_')
                .Replace('"', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('|', '_')
                .Replace(':', '_')
                .TrimStart('_');

            return safeName;
        }

        public static string GetSafeIndexName(string repoName)
        {
            var safeName = GetSafeRepoName(repoName.ToLowerInvariant());
            return safeName;
        }

        public static string GetTargetIndexName(string repoName)
        {
            return $"{GetSafeIndexName(repoName)}.{DateTime.UtcNow.ToString("yyMMdd.HHmmss")}";
        }
    }
}
