using System.Collections.Concurrent;
using System.Diagnostics.ContractsLight;
using System.Runtime.CompilerServices;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.ObjectModel.Implementation;
using Codex.Sdk.Utilities;
using Codex.Storage.BlockLevel;
using Codex.Utilities;
using Codex.Utilities.Tasks;

namespace Codex.Storage.Store
{
    public partial class DirectoryCodexStore : ICodexStore, ICodexRepositoryStore
    {
        private readonly ConcurrentQueue<ValueTask<None>> backgroundTasks = new ConcurrentQueue<ValueTask<None>>();
        private readonly ConcurrentDictionary<string, bool> addedFiles = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public readonly string DirectoryPath;
        private const string EntityFileExtension = ".cdx.json";

        private const long FileSizeByteMax = 20 << 20;

        // These two files should contain the same content after finalization
        public const string RepositoryInfoFileName = "repo" + EntityFileExtension;
        private const string RepositoryInitializationFileName = "initrepo" + EntityFileExtension;

        public DirectoryRepositoryStoreInfo StoreInfo { get; set; }

        public Logger Logger => logger;
        private Logger logger;
        private bool flattenDirectory;

        /// <summary>
        /// Disables optimized serialization for use when testing
        /// </summary>
        public bool DisableOptimization { get; set; }

        public DirectoryStoreFormat Format { get; set; } = DirectoryStoreFormat.Json;
        public ICodexStoreWriterProvider WriterProvider { get; set; } = new NullCodexStoreWriter();

        public bool ReadProjectsOnly { get; set; }

        public int MaxParallelism { get; set; } = 32;
        public bool Clean { get; set; }
        public bool WriteStoreInfo => string.IsNullOrEmpty(QualifierSuffix);
        public string QualifierSuffix { get; set; } = string.Empty;
        public string ZipPasswordPrivateKey { get; set; } = SdkFeatures.DefaultZipStorePasswordPrivateKey;
        public string ZipPassword { get; set; } = SdkFeatures.DefaultZipStorePassword;

        public Func<string, bool> CanReadFilter { get; set; }

        public DirectoryCodexStore(string directory, Logger logger = null, bool flattenDirectory = false)
        {
            DirectoryPath = directory;
            this.logger = logger ?? Logger.Null;
            this.flattenDirectory = flattenDirectory;
        }

        public static IEnumerable<string> GetEntityFiles(string directory)
        {
            if (Directory.Exists(directory))
            {
                return Directory.GetFiles(directory, "*" + EntityFileExtension, SearchOption.AllDirectories);
            }

            return Array.Empty<string>();
        }

        public Task ReadAsync(ICodexStore store, bool finalize = true, string repositoryName = null)
        {
            repositoryName = string.IsNullOrEmpty(repositoryName) ? null : repositoryName;
            return ReadCoreAsync(async fileSystem =>
            {
                if (StoreInfo == null)
                {
                    StoreInfo = Read<DirectoryRepositoryStoreInfo>(fileSystem, RepositoryInitializationFileName);
                    StoreInfo.Repository.Name = repositoryName ?? StoreInfo.Repository.Name ?? StoreInfo.Commit.RepositoryName;
                    StoreInfo.Commit.RepositoryName = StoreInfo.Repository.Name;
                }

                logger.LogMessage("Reading repository information");
                var repositoryStore = await store.CreateRepositoryStore(StoreInfo);

                logger.LogMessage($"Read repository information (repo name: {StoreInfo.Repository.Name})");

                if (repositoryStore is IndexingCodexRepositoryStoreBase indexingStore
                    && indexingStore.StoreWriter is IPrefilterCodexStoreWriter prefilterWriter)
                {
                    await prefilterWriter.LoadFilterAsync(new FileSystemReadOnlyObjectStorage(fileSystem, ""));
                }

                return repositoryStore;
            },
            finalize: finalize);
        }

        public Task ReadAsync(ICodexRepositoryStore repositoryStore)
        {
            return ReadCoreAsync(fileSystem => Task.FromResult(repositoryStore), finalize: false);
        }

        private async Task ReadCoreAsync(Func<FileSystem, Task<ICodexRepositoryStore>> createRepositoryStoreAsync, bool finalize)
        {
            FileSystem fileSystem = GetFileSystem();

            using (fileSystem)
            {
                var repositoryStore = await createRepositoryStoreAsync(fileSystem);
                await ReadCoreAsync(repositoryStore, fileSystem, finalize);
            }
        }

        private FileSystem GetFileSystem()
        {
            FileSystem fileSystem;
            if (Directory.Exists(DirectoryPath))
            {
                if (flattenDirectory)
                {
                    fileSystem = new FlattenDirectoryFileSystem(DirectoryPath, "*" + EntityFileExtension);
                }
                else
                {
                    fileSystem = new DirectoryFileSystem(DirectoryPath, "*" + EntityFileExtension);
                }
            }
            else
            {
                fileSystem = new ZipFileSystem(DirectoryPath, ZipPassword, ZipPasswordPrivateKey);
            }

            return fileSystem;
        }

        private async Task ReadCoreAsync(ICodexRepositoryStore repositoryStore, FileSystem fileSystem, bool finalize)
        {
            int nextIndex = 0;
            var paralellism = SdkFeatures.IngestParallelism.Value ?? Math.Min(Environment.ProcessorCount, MaxParallelism);
            foreach (var kind in ReadProjectsOnly
                ? new[] { StoredEntityKind.Projects }
                : StoredEntityKind.KindsProjectsFirst)
            {
                // Each kind is handled separately. Namely, we need all projects to be processed before files
                // to allow lookup of referenced definitions stored with projects.
                // NOTE: It is not sufficient to only process a file's project since MSBuild files are a part of
                // the c# project but contain references to definitions in the MSBuild files project.
                logger.LogMessage($"Reading {kind} infos");

                var kindDirectoryPath = Path.Combine(DirectoryPath, kind.Name);
                logger.LogMessage($"Reading {kind} infos from {kindDirectoryPath}");
                var files = fileSystem.GetFiles(kind.Name).ToList();
                int count = files.Count;

                await TaskUtilities.ForEachAsync(parallel: paralellism, files, async (file, token) =>
                {
                    var i = Interlocked.Increment(ref nextIndex);
                    logger.LogMessage($"{i}/{files.Count}: Queuing {kind} info at {file}");
                    if (CanReadFilter?.Invoke(file) == false || SdkFeatures.CanReadFilter.Value?.Invoke(file) == false)
                    {
                        logger.LogMessage($"{i}/{count}: Ignoring {kind} info at {file} due to defined filter function.");
                        return;
                    }

                    if (kind == StoredEntityKind.BoundFiles)
                    {
                        var fileSize = fileSystem.GetFileSize(file);
                        if (fileSize > FileSizeByteMax)
                        {
                            logger.LogMessage($"{i}/{count}: Ignoring {kind} info at {file}. File size {fileSize} bytes > {FileSizeByteMax} bytes.");

                            // Ignore files larger than 10 MB
                            return;
                        }
                    }

                    logger.LogMessage($"{i}/{count}: Reading {kind} info at {file}");
                    try
                    {
                        await kind.Add(this, fileSystem, file, repositoryStore);
                    }
                    catch (Exception ex)
                    {
                        logger.LogExceptionError("AddFile", ex);
                        throw;
                    }

                    logger.LogMessage($"{i}/{count}: Added {file} to store.");
                });
            }

            if (finalize)
            {
                logger.LogMessage($"Finalizing.");
                await repositoryStore.FinalizeAsync();
                logger.LogMessage($"Finalized.");
            }
        }

        public async Task<ICodexRepositoryStore> CreateRepositoryStore(RepositoryStoreInfo storeInfo)
        {
            if (Clean)
            {
                var allFiles = GetEntityFiles(DirectoryPath).ToList();
                foreach (var file in allFiles)
                {
                    File.Delete(file);
                }
            }

            lock (this)
            {
                Contract.Assert(StoreInfo == null);
                StoreInfo = new(storeInfo)
                {
                    Format = Format
                };
            }

            if (WriteStoreInfo)
            {
                Write(RepositoryInitializationFileName, StoreInfo);
            }

            return await CreateRepositoryStoreCore(storeInfo);
        }

        protected virtual async Task<ICodexRepositoryStore> CreateRepositoryStoreCore(RepositoryStoreInfo storeInfo)
        {
            //if (Format.ShouldWriteBlocks())
            //{
            //    var writer = await WriterProvider.CreateStoreWriterAsync(storeInfo);
            //    var store = new BlockEntityCodexRepositoryStore(
            //        writer,
            //        logger,
            //        StoreInfo,
            //        new BlockEntityWriterConfiguration(
            //            new FileGrowableBufferProvider(DirectoryPath))
            //        {
            //            AddStoreInfo = false
            //        });

            //    await store.InitializeAsync();

            //    return store;
            //}

            return this;
        }

        private Task AddAsync<T, TProcessor>(IReadOnlyList<T> entities, StoredEntityKind<T, TProcessor> kind, Func<T, string> pathGenerator)
        where T : EntityBase
        where TProcessor : IPostReadProcessor<T>
        {
            foreach (var entity in entities)
            {
                var stableId = kind.GetEntityStableId(entity);
                var pathPart = pathGenerator(entity);

                Write(Path.Combine(kind.Name, $"{pathPart}{stableId}{EntityFileExtension}"), entity, kind);
            }

            return Task.CompletedTask;
        }

        private void Write<T>(string relativePath, T entity, StoredEntityKind kind = null)
        {
            if (addedFiles.TryAdd(relativePath, true))
            {
                var fullPath = Path.Combine(DirectoryPath, relativePath);
                long fileSize = 0;

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    using (var streamWriter = new StreamWriter(fullPath))
                    {
                        var stage = DisableOptimization ? ObjectStage.All : ObjectStage.OptimizedStore;
                        entity.SerializeEntityTo(streamWriter.BaseStream, stage: stage, flags: DisableOptimization ? JsonFlags.Indented : default);
                        if (kind == StoredEntityKind.BoundFiles)
                        {
                            fileSize = streamWriter.BaseStream.Length;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogExceptionError($"Writing '{fullPath}' failed:", ex);
                    File.Delete(fullPath);
                    return;
                }

                if (fileSize > FileSizeByteMax)
                {
                    logger.LogWarning($"Excluding '{fullPath}' because file size {fileSize} bytes > {FileSizeByteMax} bytes.");
                    File.Delete(fullPath);
                }
            }
        }

        private T Read<T>(FileSystem fileSystem, string relativePath)
        {
            using (var stream = fileSystem.OpenFile(relativePath))
            {
                return stream.DeserializeEntity<T>();
            }
        }

        #region ICodexRepositoryStore Members

        public Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            return AddAsync(files.SelectList(CreateStoredBoundFile), StoredEntityKind.BoundFiles, e => Path.Combine(GetProjectFolder(e.BoundSourceFile.ProjectId), Path.GetFileName(e.BoundSourceFile.RepoRelativePath)));
        }

        private string GetProjectFolder(string projectId)
        {
            if (!string.IsNullOrEmpty(QualifierSuffix))
            {
                projectId += "&q=" + Uri.EscapeDataString(QualifierSuffix);
            }

            return Uri.EscapeDataString(projectId);
        }

        public void PreprocessBoundSourceFile(BoundSourceFile boundSourceFile)
        {
            var storedFile = CreateStoredBoundFile(boundSourceFile);
            var finalFile = FromStoredBoundFile(storedFile);
        }

        private StoredBoundSourceFile CreateStoredBoundFile(BoundSourceFile boundSourceFile)
        {
            if (StoreInfo != null)
            {
                boundSourceFile.RepositoryName = StoreInfo.Repository.Name;
            }

            boundSourceFile.ApplySourceFileInfo();

            var result = new StoredBoundSourceFile()
            {
                BoundSourceFile = boundSourceFile,
            };

            result.BeforeSerialize(optimize: !DisableOptimization, optimizeLineInfo: true, logOptimizationIssue: message => logger.LogWarning(message));
            return result;
        }

        public BoundSourceFile FromStoredBoundFile(StoredBoundSourceFile storedBoundFile)
        {
            PostReadProcessor.PostProcess(this, storedBoundFile);

            storedBoundFile.BoundSourceFile.ApplySourceFileInfo();
            storedBoundFile.AfterDeserialization();

            var boundSourceFile = storedBoundFile.BoundSourceFile;
            return boundSourceFile;
        }

        public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
        {
            return Placeholder.NotImplementedAsync();
        }

        public async Task AddProjectsAsync(IReadOnlyList<AnalyzedProjectInfo> projects)
        {
            await AddAsync(projects, StoredEntityKind.Projects, e => GetProjectFolder(e.ProjectId));
        }

        async Task ICodexRepositoryStore.FinalizeAsync()
        {
            // Flush any background operations
            while (backgroundTasks.TryDequeue(out var backgroundTask))
            {
                await backgroundTask;
            }
        }

        private static string ToStableId(params string[] values)
        {
            var rawSanitizedId = Paths.SanitizeFileName(IndexingUtilities.ComputeUrlSafeHashString(
                string.Join("|", values.Where(v => v != null).Select(v => v.ToLowerInvariant())), maxLength: 6)
                .ToUpperInvariant());

            return Uri.EscapeDataString("&s=" + Uri.EscapeDataString(rawSanitizedId));
        }

        public async Task InitializeAsync()
        {
            if (Format.ShouldWriteBlocks())
            {
                await WriterProvider.InitializeAsync();
            }
        }

        public async Task FinalizeAsync()
        {
            if (Format.ShouldWriteBlocks())
            {
                await WriterProvider.FinalizeAsync();
            }

            if (WriteStoreInfo)
            {
                Write(RepositoryInfoFileName, StoreInfo);
            }
        }

        #endregion ICodexRepositoryStore Members

        private abstract class StoredEntityKind
        {
            // NOTE: ANY KINDS ADDED MUST ALSO BE ADDED TO THE Kinds property
            public static readonly StoredEntityKind<StoredBoundSourceFile, PostReadProcessor> BoundFiles = Create<StoredBoundSourceFile, PostReadProcessor>(
                (entity) => ToStableId(entity.BoundSourceFile.ProjectId, entity.BoundSourceFile.ProjectRelativePath),
                (entity, repositoryStore, directoryStore) => repositoryStore.AddBoundFilesAsync(new[] { directoryStore.FromStoredBoundFile(entity) }));
            public static readonly StoredEntityKind<AnalyzedProjectInfo, PostReadProcessor> Projects = Create<AnalyzedProjectInfo, PostReadProcessor>(
                (entity) => ToStableId(entity.ProjectId),
                (entity, repositoryStore, directoryStore) => repositoryStore.AddProjectsAsync(new[] { entity }));

            public static IReadOnlyList<StoredEntityKind> KindsProjectsFirst => new StoredEntityKind[] { Projects, BoundFiles };

            public static StoredEntityKind<T, TProcessor> Create<T, TProcessor>(Func<T, string> getEntityStableId, Func<T, ICodexRepositoryStore, DirectoryCodexStore, Task> add, [CallerMemberName] string name = null)
                where TProcessor : IPostReadProcessor<T>
            {
                var kind = new StoredEntityKind<T, TProcessor>(getEntityStableId, add, name);
                return kind;
            }

            public abstract string Name { get; }

            public abstract Task Add(DirectoryCodexStore store, FileSystem fileSystem, string fullPath, ICodexRepositoryStore repositoryStore);

            public override string ToString()
            {
                return Name;
            }
        }

        private class StoredEntityKind<T, TProccesor> : StoredEntityKind
            where TProccesor : IPostReadProcessor<T>
        {
            public override string Name { get; }

            public TProccesor Processor { get; }

            private Func<T, ICodexRepositoryStore, DirectoryCodexStore, Task> add;

            public readonly Func<T, string> GetEntityStableId;

            public StoredEntityKind(Func<T, string> getEntityStableId, Func<T, ICodexRepositoryStore, DirectoryCodexStore, Task> add, string name)
            {
                Name = name;
                this.add = add;
                GetEntityStableId = getEntityStableId;
            }

            public override Task Add(DirectoryCodexStore store, FileSystem fileSystem, string fullPath, ICodexRepositoryStore repositoryStore)
            {
                T entity = Read(store, fileSystem, fullPath);
                return add(entity, repositoryStore, store);
            }

            public T Read(DirectoryCodexStore store, FileSystem fileSystem, string fullPath)
            {
                return store.Read<T>(fileSystem, fullPath);
            }
        }

        private class PostReadProcessor :
            IPostReadProcessor<StoredBoundSourceFile>,
            IPostReadProcessor<AnalyzedProjectInfo>
        {
            public static void PostProcess(DirectoryCodexStore store, AnalyzedProjectInfo entity)
            {
                if (store.StoreInfo?.Repository is { } repo && !string.IsNullOrEmpty(repo.Name))
                {
                    entity.RepositoryName = repo.Name;
                }
            }

            public static void PostProcess(DirectoryCodexStore store, StoredBoundSourceFile storedBoundFile)
            {
                if (store.StoreInfo?.Repository is { } repo && !string.IsNullOrEmpty(repo.Name))
                {
                    storedBoundFile.BoundSourceFile.RepositoryName = repo.Name;
                }
            }
        }

        private interface IPostReadProcessor<T>
        {
            static abstract void PostProcess(DirectoryCodexStore store, T entity);
        }
    }
}
