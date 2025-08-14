using System.IO;
using System.Text.Json;
using Codex.Application;
using Codex.Lucene;
using Codex.Lucene.Formats;
using Codex.Lucene.Search;
using Codex.Storage;
using Codex.Utilities;
using Codex.View;
using Codex.Web.Common;
using LibGit2Sharp;
using Xunit.Abstractions;

using static Codex.ObjectModel.SearchTypeId;

namespace Codex.Integration.Tests;

public record StoredFilterTests(ITestOutputHelper Output) : CodexTestBase(Output)
{
    [Fact]
    public void Simple()
    {
        var roaring1 = RoaringDocIdSet.From(new[] { 1, 2, 3 });
        var roaring2 = RoaringDocIdSet.From(new[] { 2, 3 });
        var roaring3 = RoaringDocIdSet.From(new[] { 3 });

        CountingFilter counting = new CountingFilter();

        counting.Add(roaring1);
        counting.Add(roaring2);
        counting.Add(roaring3);

        var agg = counting.GetAggregate();
    }

    private PersistedStoredFilter LoadFilter(Repository repo, string commitId, string path)
    {
        path = path.Replace('\\', '/').TrimStart('/');
        var filterBlob = (Blob)repo.Lookup<Commit>(commitId)[path].Target;

        using var stream = filterBlob.GetContentStream();
        return JsonSerializationUtilities.DeserializeEntity<PersistedStoredFilter>(stream);
    }

    private PersistedStoredFilter FindFilter(GitObjectStorage gitStorage, string sourceCommitId, string updateCommitId)
    {
        var sourceCommit = gitStorage.Repo.Lookup<Commit>(sourceCommitId);
        var updateCommit = gitStorage.Repo.Lookup<Commit>(updateCommitId);
        var treeChanges = gitStorage.Repo.Diff.Compare<TreeChanges>(sourceCommit.Tree, updateCommit.Tree);

        var item = treeChanges.Added.Where(i => i.Path.ContainsIgnoreCase("repos") && i.Path.ContainsIgnoreCase(".json")
            && !i.Path.ContainsIgnoreCase(".cumulative.json")).First();

        var filterBlob = gitStorage.Repo.Lookup<Blob>(item.Oid);

        using var stream = filterBlob.GetContentStream();
        return JsonSerializationUtilities.DeserializeEntity<PersistedStoredFilter>(stream);
    }
}