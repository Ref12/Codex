﻿using System;
using System.IO;
using System.Linq;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.Utilities;
using Git = LibGit2Sharp;

namespace Codex.Application
{
    public class GitHelpers
    {
        public static void DetectGit((Repository repository, Commit commit, Branch branch) repoData, string root, Logger logger)
        {
            try
            {
                (var repository, var commit, var branch) = repoData;
                root = Path.GetFullPath(root);
                logger.LogMessage($"DetectGit: Using LibGit2Sharp to load repo info for {root}");
                using (var repo = new Git.Repository(root))
                {
                    var tip = repo.Head.Tip;
                    var firstRemote = repo.Network.Remotes.FirstOrDefault();

                    commit.CommitId = Set(logger, "commit.CommitId", () => tip.Id.Sha);
                    commit.DateCommitted = Set(logger, "commit.DateCommited", () => tip.Committer.When.DateTime.ToUniversalTime());
                    commit.Description = Set(logger, "commit.Description", () => tip.Message?.Trim());
                    commit.ParentCommitIds.AddRange(Set(logger, "commit.ParentCommitIds", () => tip.Parents?.Select(c => c.Sha).ToArray() ?? CollectionUtilities.Empty<string>.Array, v => string.Join(", ", v)));
                    branch.Name = Set(logger, "branch.Name", () => GetBranchName(repo));
                    branch.HeadCommitId = Set(logger, "branch.HeadCommitId", () => commit.CommitId);
                    repository.SourceControlWebAddress = Set(logger, "repository.SourceControlWebAddress", () => firstRemote?.Url?.TrimEndIgnoreCase(".git"), defaultValue: repository.SourceControlWebAddress);
                    
                    // TODO: Add changed files?
                }
            }
            catch (Exception ex)
            {
                logger.LogExceptionError("DetectGit", ex);
            }
        }

        private static string GetBranchName(Git.Repository repo)
        {
            var head = repo.Head;
            var name = head.TrackedBranch?.FriendlyName;
            if (name != null && (name.Contains("master") || name.Contains("main")))
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