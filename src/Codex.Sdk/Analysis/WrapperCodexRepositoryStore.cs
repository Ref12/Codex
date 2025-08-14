namespace Codex
{
    public record WrapperCodexStore(ICodexStore Inner, Func<ICodexRepositoryStore, RepositoryStoreInfo, ICodexRepositoryStore> WrapStore) : ICodexStore
    {
        public virtual async Task<ICodexRepositoryStore> CreateRepositoryStore(RepositoryStoreInfo info)
        {
            var store = await Inner.CreateRepositoryStore(info);
            return WrapStore(store, info);
        }

        public virtual Task FinalizeAsync()
        {
            return Inner.FinalizeAsync();
        }

        public virtual Task InitializeAsync()
        {
            return Inner.InitializeAsync();
        }
    }

    public record WrapperCodexRepositoryStore(ICodexRepositoryStore InnerStore) : ICodexRepositoryStore
    {
        public virtual Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            return InnerStore.AddBoundFilesAsync(files);
        }

        public virtual Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
        {
            return InnerStore.AddLanguagesAsync(languages);
        }

        public virtual Task AddProjectsAsync(IReadOnlyList<AnalyzedProjectInfo> projects)
        {
            return InnerStore.AddProjectsAsync(projects);
        }

        public virtual Task FinalizeAsync()
        {
            return InnerStore.FinalizeAsync();
        }
    }
}
