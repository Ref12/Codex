﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using System.Collections.Concurrent;
using Codex.Sdk.Utilities;
using System.IO;
using Codex.Utilities;
using Codex.Serialization;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Codex.Storage.DataModel;
using Codex.ElasticSearch.Utilities;
using Codex.Logging;
using System.Threading;

namespace Codex.ElasticSearch.Store
{
    public class DirectoryCodexStore : ICodexStore, ICodexRepositoryStore
    {
        private readonly ConcurrentQueue<ValueTask<None>> backgroundTasks = new ConcurrentQueue<ValueTask<None>>();
        private readonly ConcurrentQueue<CommitFileLink> commitFiles = new ConcurrentQueue<CommitFileLink>();

        public readonly string DirectoryPath;
        private const string EntityFileExtension = ".cdx.json";

        // These two files should contain the same content after finalization
        private const string RepositoryInfoFileName = "repo" + EntityFileExtension;
        private const string RepositoryInitializationFileName = "initrepo" + EntityFileExtension;

        private RepositoryStoreInfo m_storeInfo;

        private Logger logger;

        /// <summary>
        /// Disables optimized serialization for use when testing
        /// </summary>
        public bool DisableOptimization { get; set; }
        public bool Clean { get; set; }

        public DirectoryCodexStore(string directory, Logger logger = null)
        {
            DirectoryPath = directory;
            this.logger = logger ?? Logger.Null;
        }

        public static IEnumerable<string> GetEntityFiles(string directory)
        {
            if (Directory.Exists(directory))
            {
                return Directory.GetFiles(directory, "*" + EntityFileExtension, SearchOption.AllDirectories);
            }

            return CollectionUtilities.Empty<string>.Array;
        }

        public async Task ReadAsync(ICodexStore store, bool finalize = true)
        {
            FileSystem fileSystem;
            if (Directory.Exists(DirectoryPath))
            {
                fileSystem = new DirectoryFileSystem(DirectoryPath, "*" + EntityFileExtension);
            }
            else
            {
                fileSystem = new ZipFileSystem(DirectoryPath);
            }

            using (fileSystem)
            {
                m_storeInfo = Read<RepositoryStoreInfo>(fileSystem, RepositoryInitializationFileName);

                ConcurrentQueue<Func<Task>> actionQueue = new ConcurrentQueue<Func<Task>>();

                logger.LogMessage("Reading repository information");
                var repositoryStore = await store.CreateRepositoryStore(m_storeInfo.Repository, m_storeInfo.Commit, m_storeInfo.Branch);

                logger.LogMessage($"Read repository information (repo name: {m_storeInfo.Repository.Name})");

                int nextIndex = 0;
                int count = 0;
                List<Task> tasks = new List<Task>();
                foreach (var kind in StoredEntityKind.Kinds)
                {
                    // TODO: Do we even need concurrency here?
                    tasks.Add(Task.Run(() =>
                    {
                        var kindDirectoryPath = Path.Combine(DirectoryPath, kind.Name);
                        logger.LogMessage($"Reading {kind} infos from {kindDirectoryPath}");
                        foreach (var file in fileSystem.GetFiles(kind.Name))
                        {
                            actionQueue.Enqueue(async () =>
                            {
                                var i = Interlocked.Increment(ref nextIndex);
                                logger.LogMessage($"{i}/{count}: Reading {kind} info at {file}");
                                await kind.Add(this, fileSystem, file, repositoryStore);
                                logger.LogMessage($"{i}/{count}: Added {file} to store.");
                            });
                        }
                    }
                    ));
                }

                await Task.WhenAll(tasks);

                count = actionQueue.Count;
                tasks.Clear();
                for (int i = 0; i < Math.Min(Environment.ProcessorCount, 32); i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        while (actionQueue.TryDequeue(out var taskFactory))
                        {
                            await taskFactory();
                        }
                    }
                    ));
                }

                await Task.WhenAll(tasks);

                if (finalize)
                {
                    await repositoryStore.FinalizeAsync();
                }
            }
        }

        public Task<ICodexRepositoryStore> CreateRepositoryStore(Repository repository, Commit commit, Branch branch)
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
                Contract.Assert(m_storeInfo == null);
                m_storeInfo = new RepositoryStoreInfo()
                {
                    Repository = repository,
                    Commit = commit,
                    Branch = branch
                };
            }
            Write(RepositoryInitializationFileName, m_storeInfo);

            return Task.FromResult<ICodexRepositoryStore>(this);
        }

        private Task AddAsync<T>(IReadOnlyList<T> entities, StoredEntityKind<T> kind, Func<T, string> pathGenerator)
            where T : EntityBase
        {
            foreach (var entity in entities)
            {
                var stableId = kind.GetEntityStableId(entity);
                var pathPart = pathGenerator(entity);

                Write(Path.Combine(kind.Name, $"{pathPart}.{stableId}{EntityFileExtension}"), entity);
            }

            return Task.CompletedTask;
        }

        private void Write<T>(string relativePath, T entity)
        {
            var fullPath = Path.Combine(DirectoryPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var streamWriter = new StreamWriter(fullPath))
            {
                entity.SerializeEntityTo(new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented });
            }
        }

        private T Read<T>(FileSystem fileSystem, string relativePath)
        {
            using (var streamReader = new StreamReader(fileSystem.OpenFile(relativePath)))
            {
                return streamReader.DeserializeEntity<T>();
            }
        }

        #region ICodexRepositoryStore Members

        public Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            return AddAsync(files.SelectList(CreateStoredBoundFile), StoredEntityKind.BoundFiles, e => Path.Combine(e.BoundSourceFile.ProjectId, Path.GetFileName(e.BoundSourceFile.SourceFile.Info.RepoRelativePath)));
        }

        private StoredBoundSourceFile CreateStoredBoundFile(BoundSourceFile boundSourceFile)
        {
            boundSourceFile.RepositoryName = m_storeInfo.Repository.Name;
            boundSourceFile.ApplySourceFileInfo();

            var result = new StoredBoundSourceFile()
            {
                BoundSourceFile = boundSourceFile,
            };

            result.BeforeSerialize(optimize: !DisableOptimization, optimizeLineInfo: true);

            return result;
        }

        private BoundSourceFile FromStoredBoundFile(StoredBoundSourceFile storedBoundFile)
        {
            storedBoundFile.BoundSourceFile.RepositoryName = m_storeInfo.Repository.Name;
            storedBoundFile.BoundSourceFile.ApplySourceFileInfo();
            storedBoundFile.AfterDeserialization();

            var boundSourceFile = storedBoundFile.BoundSourceFile;
            return boundSourceFile;
        }

        public Task AddCommitFilesAsync(IReadOnlyList<CommitFileLink> links)
        {
            foreach (var link in links)
            {
                commitFiles.Enqueue(link);
            }

            return Task.CompletedTask;
        }

        public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
        {
            return Placeholder.NotImplementedAsync();
        }

        public async Task AddProjectsAsync(IReadOnlyList<AnalyzedProject> projects)
        {
            foreach (var project in projects)
            {
                await AddBoundFilesAsync(project.AdditionalSourceFiles);
            }

            await AddAsync(projects, StoredEntityKind.Projects, e => Path.GetFileName(e.ProjectId));
        }

        public Task AddTextFilesAsync(IReadOnlyList<SourceFile> files)
        {
            return AddAsync(files, StoredEntityKind.TextFiles, e => Path.GetFileName(e.Info.RepoRelativePath));
        }

        public async Task FinalizeAsync()
        {
            await AddAsync(commitFiles.GroupBy(f => Path.GetDirectoryName(f.RepoRelativePath), StringComparer.OrdinalIgnoreCase)
                .Select(g => new CommitFilesDirectory()
                {
                    RepoRelativePath = g.Key,
                    Files = g.OrderBy(c => c.RepoRelativePath, StringComparer.OrdinalIgnoreCase).ToList()
                }).ToList(), StoredEntityKind.CommitDirectories, e => Path.GetFileName(e.RepoRelativePath) ?? string.Empty);

            // Flush any background operations
            while (backgroundTasks.TryDequeue(out var backgroundTask))
            {
                await backgroundTask;
            }

            Write(RepositoryInfoFileName, m_storeInfo);
        }

        private static string ToStableId(params string[] values)
        {
            var rawSanitizedId = Paths.SanitizeFileName(IndexingUtilities.ComputeHashString(
                string.Join("|", values.Where(v => v != null).Select(v => v.ToLowerInvariant())))
                .Substring(0, 6)
                .Replace("\\", "_")
                .ToUpperInvariant());

            return $"[{rawSanitizedId}]";
        }

        #endregion ICodexRepositoryStore Members

        private abstract class StoredEntityKind
        {
            public static readonly StoredEntityKind<StoredBoundSourceFile> BoundFiles = Create<StoredBoundSourceFile>(
                (entity) => ToStableId(entity.BoundSourceFile.ProjectId, entity.BoundSourceFile.ProjectRelativePath),
                (entity, repositoryStore, directoryStore) => repositoryStore.AddBoundFilesAsync(new[] { directoryStore.FromStoredBoundFile(entity) }));
            public static readonly StoredEntityKind<AnalyzedProject> Projects = Create<AnalyzedProject>(
                (entity) => ToStableId(entity.ProjectId),
                (entity, repositoryStore, directoryStore) => repositoryStore.AddProjectsAsync(new[] { entity }));
            public static readonly StoredEntityKind<SourceFile> TextFiles = Create<SourceFile>(
                (entity) => ToStableId(entity.Info.RepoRelativePath, entity.Info.ProjectRelativePath),
                (entity, repositoryStore, directoryStore) => repositoryStore.AddTextFilesAsync(new[] { entity }));
            public static readonly StoredEntityKind<CommitFilesDirectory> CommitDirectories = Create<CommitFilesDirectory>(
                (entity) => ToStableId(entity.RepoRelativePath),
                (entity, repositoryStore, directoryStore) => repositoryStore.AddCommitFilesAsync(entity.Files));

            public static IReadOnlyList<StoredEntityKind> Kinds => Inner.Kinds;

            public static StoredEntityKind<T> Create<T>(Func<T, string> getEntityStableId, Func<T, ICodexRepositoryStore, DirectoryCodexStore, Task> add, [CallerMemberName] string name = null)
            {
                var kind = new StoredEntityKind<T>(getEntityStableId, add, name);
                Inner.Kinds.Add(kind);
                return kind;
            }

            public abstract string Name { get; }

            public abstract Task Add(DirectoryCodexStore store, FileSystem fileSystem, string fullPath, ICodexRepositoryStore repositoryStore);

            private static class Inner
            {
                public static readonly List<StoredEntityKind> Kinds = new List<StoredEntityKind>();
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private class StoredEntityKind<T> : StoredEntityKind
        {
            public override string Name { get; }

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
                var entity = store.Read<T>(fileSystem, fullPath);
                return add(entity, repositoryStore, store);
            }
        }
    }
}