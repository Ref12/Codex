using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.Search;
using Codex.Utilities;
using Microsoft.Build.Logging.StructuredLogger;
using Git = LibGit2Sharp;

namespace Codex.Application
{
    public class GitHelpers
    {
        static GitHelpers()
        {
            Init();
        }

        public static bool Init()
        {
            // This is here only to trigger static constructor
            return Git.LibGit.Init();
        }

        public static GitShaProvider DetectGit(RepositoryStoreInfo storeInfo, string root, Logger logger)
        {
            GitShaProvider provider = null;

            try
            {
                (var repository, var commit, var branch) = storeInfo;
                root = Path.GetFullPath(root);
                logger.LogMessage($"DetectGit: Using LibGit2Sharp to load repo info for {root}");
                var repo = new Git.Repository(root);

                provider = new GitShaProvider(repo);
                {
                    var tip = repo.Head.Tip;
                    var firstRemote = repo.Network.Remotes.FirstOrDefault()?.Url;
                    if (firstRemote != null && Uri.TryCreate(firstRemote, UriKind.Absolute, out var firstRemoteUri))
                    {
                        // String username and password from remote uri.
                        firstRemote = new UriBuilder(firstRemoteUri) { UserName = null, Password = null }.Uri.ToString();
                    }

                    commit.CommitId = Set(logger, "commit.CommitId", () => tip.Id.Sha);
                    commit.DateCommitted = Set(logger, "commit.DateCommited", () => tip.Committer.When.DateTime.ToUniversalTime());
                    commit.Description = Set(logger, "commit.Description", () => tip.Message?.Trim(), print: _ => "...");
                    commit.ParentCommitIds.AddRange(Set(logger, "commit.ParentCommitIds", () => tip.Parents?.Select(c => c.Sha).ToArray() ?? Array.Empty<string>(), v => string.Join(", ", v)));
                    branch.Name = Set(logger, "branch.Name", () => GetBranchName(repo));
                    branch.HeadCommitId = Set(logger, "branch.HeadCommitId", () => commit.CommitId);
                    repository.SourceControlWebAddress = Set(logger, "repository.SourceControlWebAddress", () => firstRemote?.TrimEndIgnoreCase(".git"), defaultValue: repository.SourceControlWebAddress);
                    
                    // TODO: Add changed files?
                }
            }
            catch (Exception ex)
            {
                logger.LogExceptionError("DetectGit", ex);
                provider?.Dispose();
            }

            return provider;
        }

        public class GitShaProvider : ISourceControlInfoProvider
        {
            public GitShaProvider(Git.Repository repository)
            {
                Repository = repository;
                HeadTip = repository.Head.Tip;
            }

            public Git.Repository Repository { get; }
            public Git.Commit HeadTip { get; private set; }

            public void Dispose()
            {
                Repository.Dispose();
            }

            public IEnumerable<string> GetSubmodulePaths()
            {
                return Repository.Submodules.Select(s => s.Path).ToArray();
            }

            public bool TryGetContentId(string repoRelativePath, out KeyValuePair<PropertyKey, string> sourceControlContentId)
            {
                repoRelativePath = PathUtilities.AsUrlRelativePath(repoRelativePath, encode: false);
                var treeEntry = HeadTip[repoRelativePath];
                var sha = treeEntry?.Target.Sha.ToLowerInvariant();
                if (sha != null)
                {
                    sourceControlContentId = new(PropertyKey.GitSha, sha);
                    return true;
                }
                else
                {
                    sourceControlContentId = default;
                    return false;
                }
            }
        }

        private static string GetBranchName(Git.Repository repo)
        {
            var head = repo.Head;
            var name = head.TrackedBranch?.FriendlyName;
            if (name != null)
            {
                if (head.RemoteName != null)
                {
                    return name.TrimStartIgnoreCase(head.RemoteName).TrimStart('/');
                }

                return name;
            }

            // Try to find master or main branch
            foreach (var branch in repo.Branches)
            {
                if (branch.IsRemote)
                {
                    var canonicalName = branch.UpstreamBranchCanonicalName;
                    if (canonicalName == "refs/heads/main" || canonicalName == "refs/heads/master")
                    {
                        return canonicalName.Substring("refs/heads/".Length);
                    }
                }
            }

            // Try find current branch remote
            foreach (var branch in repo.Branches)
            {
                if (branch.IsRemote && branch.Tip.Id == head.Tip.Id)
                {
                    var canonicalName = branch.UpstreamBranchCanonicalName;
                    return canonicalName.Substring("refs/heads/".Length);
                }
            }

            if (name != null)
            {
                if (head.RemoteName != null)
                {
                    return name.TrimStartIgnoreCase(head.RemoteName).TrimStart('/');
                }

                return name;
            }

            return head.FriendlyName;
        }

        private static T Set<T>(Logger logger, string valueName, Func<T> get, Func<T, string> print = null, T defaultValue = default)
        {
            print = print ?? (v => v?.ToString());
            var value = get();

            if (!(value is object obj))
            {
                value = defaultValue;
            }

            logger.LogMessage($"DetectGit: Updating {valueName} to [{print(value)}]");
            return value;
        }
    }
}