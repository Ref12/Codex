using System.IO.Compression;
using Codex.Automation.Workflow;
using System.Text.RegularExpressions;
using Codex.Sdk;
using Codex.Utilities;
using CommandLine;
using Codex.Application.Verbs;
using LibGit2Sharp;
using Codex.Application;

namespace Codex.Automation.Workflow.Cli;

[Verb("git-reparent", HelpText = "Creates a new commit based off the current commit, but with a new parent commit.")]
public record GitApply : OperationBase
{
    [Option('c', "commit", Required = false, Default = "HEAD", HelpText = "The committish to reparent.")]
    public string commitId { get; set; } = "HEAD";

    [Option('p', "parent", Required = true, HelpText = "The parent committish to reparent to.")]
    public string parentId { get; set; }

    [Option('b', "branch", Required = false, HelpText = "The local branch to update. Defaults to current branch")]
    public string? targetBranchName { get; set; }

    [Option('r', "root", Required = false, HelpText = "The root of the repository. Defaults to the current working directory.")]
    public string? root { get; set; }

    protected override async ValueTask InitializeAsync()
    {
        GitHelpers.Init();
        await base.InitializeAsync();
    }

    protected override async ValueTask<int> ExecuteAsync()
    {
        using var repo = new Repository(root ?? Environment.CurrentDirectory);
        var tip = repo.Head.Tip;

        var startCommit = repo.Lookup<Commit>(commitId);
        var newParent = repo.Lookup<Commit>(parentId);

        var newCommit = repo.ObjectDatabase.CreateCommit(
            author: startCommit.Author,
            committer: startCommit.Committer,
            message: startCommit.Message,
            tree: startCommit.Tree,
            parents: [newParent],
            prettifyMessage: false);


        Logger.LogMessage($"Created commit: {newCommit.Sha} with parent (id={parentId}, resolved value={newParent.Sha}) from (id={commitId}, resolved value={startCommit.Sha})");

        if (targetBranchName.IsNonEmpty())
        {
            repo.Branches.Add(targetBranchName, newCommit, allowOverwrite: true);
        }
        else
        {
            Logger.LogMessage($"Updating current working dir to {newCommit.Sha}  from {tip.Sha}");
            repo.Reset(ResetMode.Mixed, newCommit);
        }

        return 0;
    }
}

