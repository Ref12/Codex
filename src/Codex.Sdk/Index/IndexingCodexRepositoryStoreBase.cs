using System.Collections.Concurrent;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.ObjectModel.Attributes;
using Codex.ObjectModel.Implementation;
using Codex.Sdk.Utilities;
using Codex.Utilities;
using Codex.Utilities.Serialization;

namespace Codex.Storage
{
    public abstract record IndexingCodexRepositoryStoreBase(
        ICodexStoreWriter StoreWriter,
        Logger Logger,
        RepositoryStoreInfo StoreInfo) : ICodexRepositoryStore
    {
        public virtual bool AddStoreInfo => true;

        public virtual Task InitializeAsync()
        {
            return StoreWriter.InitializeAsync();
        }

        public virtual async Task FinalizeAsync()
        {
            if (AddStoreInfo)
            {
                await AddAsync(SearchTypes.Repository, new RepositorySearchModel()
                {
                    Repository = StoreInfo.Repository,
                });

                if (StoreInfo.Commit != null)
                {
                    await AddAsync(SearchTypes.Commit, new CommitSearchModel()
                    {
                        Commit = StoreInfo.Commit,
                    });
                }

                if (StoreInfo.Branch != null)
                {
                    Placeholder.Todo("Add branch store and add branch to branch store");
                }

                Placeholder.Todo("Add commit bound source document (with links to changed files in commit, commit stats [lines added/removed], link to commit portal, link to diff view).");
            }

            await StoreWriter.FinalizeAsync();
        }

        #region Abstract Members

        public virtual ValueTask AddAsync<T>(SearchType<T> searchType, T entity, IndexAddOptions options = default)
            where T : class, ISearchEntity<T>
        {
            entity.PopulateContentIdAndSize();

            return StoreWriter.AddAsync(searchType, entity, options);
        }

        #endregion Abstract Members

        public virtual async Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            foreach (var file in files)
            {
                using var _ = StoreWriter.EnterRootEntityScope(file);

                if (!SdkFeatures.AmbientFileIndexFilter.Value.Invoke(file.SourceFile.Info))
                {
                    continue;
                }

                await AddBoundSourceFileAsync(file);
            }
        }

        public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
        {
            return Placeholder.NotImplementedAsync("Add language support");
        }

        public virtual async Task AddProjectsAsync(IReadOnlyList<AnalyzedProjectInfo> projects)
        {
            foreach (var project in projects)
            {
                using var _ = StoreWriter.EnterRootEntityScope(project);

                await AddProjectAsync(project);
            }
        }

        protected abstract Task AddProjectAsync(AnalyzedProjectInfo project);

        protected abstract Task AddBoundSourceFileAsync(BoundSourceFile boundSourceFile);
    }
}