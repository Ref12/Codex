using System;
using System.Collections.Immutable;
using Codex.Logging;
using Codex.Lucene.Formats;
using Codex.Storage;
using Codex.Utilities;
using Lucene.Net.Search;

namespace Codex.Lucene.Search
{
    using static LuceneConstants;

    public partial record StoredFilterUpdater(IObjectStorage DiskStorage, Logger Logger, string SettingsRoot = null)
    {
        public StoredFile<StableIdStorageHeader> HeaderFile { get; } = new StoredFile<StableIdStorageHeader>(DiskStorage, StableIdStorageHeaderPath);
        public StoredFile<StableIdStorageHeader> HeaderLegacyFile { get; } = new StoredFile<StableIdStorageHeader>(DiskStorage, StableIdStorageHeaderPath);
        public StableIdStorageHeader Header { get; private set; }

        public IStoredRepositorySettings DefaultRepoSettings { get; set; } = new StoredRepositorySettings();

        private string RepoSettingsPath = PathUtilities.UriCombine(SettingsRoot, RepoSettingsRelativePath, normalize: true); 

        public async ValueTask InitializeAsync()
        {
            Header = await LoadHeaderAsync();
        }

        public async ValueTask FinalizeAsync()
        {
            await HeaderFile.WriteAsync(Header);
        }

        public async ValueTask<StableIdStorageHeader> LoadHeaderAsync()
        {
            return await DiskStorage.LoadValueAsync<StableIdStorageHeader>(LuceneConstants.StableIdStorageHeaderPath)
                ?? await DiskStorage.CreateOrLoadValueAsync<StableIdStorageHeader>(LuceneConstants.StableIdStorageHeaderLegacyPath);
        }

        public async ValueTask UpdateRepoAsync(string repoName, PersistedStoredFilter? newFilter, bool reuseOldFilter = false)
        {
            StoredFilterFiles storedRepoFilter = GetRepoFilter(repoName);

            var addGroupFilters = (newFilter == null && !reuseOldFilter) ? new StoredFilterFiles[0] : await GetAddGroupFiltersAsync(storedRepoFilter.Name);

            (var oldFilter, var oldGroups) = await storedRepoFilter.GetAndUpdateRepoFilterAsync(newFilter, addGroupFilters.SelectArray(g => g.Name));

            if (reuseOldFilter)
            {
                newFilter = oldFilter;
            }

            var removeGroupFilters = GetGroupFilters(oldGroups.Except(addGroupFilters.Select(g => g.Name)));

            PersistedStoredFilter addFilter = newFilter;
            PersistedStoredFilter removeFilter = oldFilter;
            
            if (reuseOldFilter)
            {
                (addFilter, removeFilter) = (new(), new());
            }
            else if (newFilter != null && !oldFilter.IsEmpty)
            {
                (addFilter, removeFilter) = Diffable.Diff(newFilter, oldFilter);
            }

            await Parallel.ForEachAsync(addGroupFilters, async (groupFilter, token) =>
            {
                Logger.LogMessage($"Adding repository '{repoName}' to group '{groupFilter.Name}'.");
                await groupFilter.UpdateGroupFilterAsync(storedRepoFilter, newFilter, addFilter: addFilter, removeFilter: removeFilter);
            });

            await Parallel.ForEachAsync(removeGroupFilters, async (groupFilter, token) =>
            {
                Logger.LogMessage($"Removing repository '{repoName}' from group '{groupFilter.Name}'.");

                // Specify new filter as null, to indicate we are removing the repo.
                await groupFilter.UpdateGroupFilterAsync(storedRepoFilter, newFilter: null, addFilter: null, removeFilter: oldFilter);
            });
        }

        public StoredFilterFiles GetRepoFilter(string repoName)
        {
            return new StoredFilterFiles(DiskStorage, repoName, StoredFilterKinds.repo);
        }

        private IReadOnlyList<StoredFilterFiles> GetGroupFilters(IEnumerable<string> groupNames)
        {
            return groupNames.Select(name => new StoredFilterFiles(DiskStorage, name, StoredFilterKinds.group)).ToArray();
        }

        private async Task<IReadOnlyList<StoredFilterFiles>> GetAddGroupFiltersAsync(string repoName)
        {
            var settingsFile = new StoredFile<GlobalStoredRepositorySettings>(DiskStorage, RepoSettingsPath);
            var globalSettings = await settingsFile.LoadAsync();
            var repoSettings = globalSettings.Repositories.GetValueOrDefault(repoName, DefaultRepoSettings);

            IEnumerable<RepoName> getGroupNames()
            {
                foreach (var access in Enum.GetValues<RepoAccess>())
                {
                    if (access == RepoAccess.InternalOnly && repoSettings.Access == RepoAccess.Public)
                    {
                        // Public repos should not be included in InternalOnly
                        continue;
                    }

                    if (access <= repoSettings.Access)
                    {
                        yield return access.GetGroupName();
                    }
                }

                if (!repoSettings.ExplicitGroupsOnly)
                {
                    yield return AllGroupName;
                }

                foreach (var group in repoSettings.Groups)
                {
                    if (IsReservedGroupName(group))
                    {
                        Logger.LogWarning($"{group} is a reserved name and cannot be specified in explicit groups. Skipping.");
                        continue;
                    }

                    yield return group;
                }
            }

            var groupNames = getGroupNames().ToImmutableHashSet();

            var groupSettingsByBase = globalSettings.Groups.ToLookup(g => g.Value.Base);

            foreach (var groupName in groupNames)
            {
                foreach (var derivedGroup in groupSettingsByBase[groupName])
                {
                    if (!derivedGroup.Value.Excludes.Contains(repoName) && !derivedGroup.Value.Excludes.Any(e => groupNames.Contains(e)))
                    {
                        groupNames = groupNames.Add(derivedGroup.Key);
                    }
                }
            }

            return GetGroupFilters(groupNames.Select(s => s.Value));
        }
    }
}