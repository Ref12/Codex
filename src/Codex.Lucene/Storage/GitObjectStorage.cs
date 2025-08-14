using LibGit2Sharp;
using Branch = LibGit2Sharp.Branch;
using Commit = LibGit2Sharp.Commit;
using Repository = LibGit2Sharp.Repository;

namespace Codex.Storage;

public record GitObjectStorage(string Directory,
    string BranchName = GitObjectStorage.FiltersBranchName,
    string CommitId = null,
    bool AllowWrites = true,
    bool Reset = false) : IObjectStorage
{
    public const string FiltersBranchName = "filters";
    public Repository Repo { get; private set; }
    private TreeDefinition treeDefinition;
    private Branch branch;
    private Commit sourceCommit;

    public GitObjectStorage WithBranch(string branchName)
    {
        return this with
        {
            BranchName = branchName,
            branch = null,
            sourceCommit = null,
            treeDefinition = null,
        };
    }

    public void Initialize()
    {
        LibGit.Init();

        if (Repo == null)
        {
            if (!Repository.IsValid(Directory))
            {
                Repository.Init(Directory);
            }

            Repo = new Repository(Directory);
        }

        if (branch == null)
        {
            if (Reset)
            {
                Repo.Branches.Remove(BranchName);
            }

            branch = Repo.Branches[BranchName];
            if (CommitId != null)
            {
                sourceCommit = Repo.Lookup<Commit>(CommitId);
                treeDefinition = TreeDefinition.From(sourceCommit);
                if (branch?.Tip == null)
                {
                    branch = Repo.CreateBranch(BranchName, CreateCommit(new TreeDefinition(), "Initial", fresh: true));
                }
            }
            else if (branch?.Tip is { } commit)
            {
                treeDefinition = TreeDefinition.From(commit);
                sourceCommit = commit;
            }
            else
            {
                treeDefinition = new TreeDefinition();
                branch = Repo.CreateBranch(BranchName, CreateCommit(new TreeDefinition(), "Initial", fresh: true));
            }
        }
    }

    public void Finalize(string message)
    {
        if (AllowWrites)
        {
            Commit filterCommit = CreateCommit(treeDefinition, message);

            branch ??= Repo.CreateBranch(BranchName, filterCommit);
            Repo.Branches.Add(BranchName, filterCommit, allowOverwrite: true);

            if (Repo.Head?.Tip == null)
            {
                Repo.Reset(ResetMode.Soft, filterCommit);
            }
        }
    }

    private Commit CreateCommit(TreeDefinition treeDefinition, string message, bool fresh = false)
    {
        var tree = Repo.ObjectDatabase.CreateTree(treeDefinition);
        var committer = new Signature("CodexIngester", "ingest@ref12.io", DateTimeOffset.Now);
        var filterCommit = Repo.ObjectDatabase.CreateCommit(
            committer,
            committer,
            message,
            tree,
            parents: new[] { branch?.Tip }.Where(c => c != null && !fresh),
            prettifyMessage: false);
        return filterCommit;
    }

    public Stream Load(string relativePath)
    {
        var blob = sourceCommit?[relativePath]?.Target as Blob;
        if (blob == null) return null;

        return blob.GetContentStream();
    }

    public string Write(string relativePath, MemoryStream stream)
    {
        if (!AllowWrites) return null;

        stream.Position = 0;
        var blob = Repo.ObjectDatabase.CreateBlob(stream);

        lock (treeDefinition)
        {
            treeDefinition.Add(relativePath, blob, Mode.NonExecutableFile);
        }

        return blob.Sha;
    }

    public void Dispose()
    {
        Repo.Dispose();
    }
}