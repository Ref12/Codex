﻿using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Utilities;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatcher
    {
        /// <summary>
        /// Defines stored filters for each entity type
        /// </summary>
        public readonly ElasticSearchStoredFilterBuilder[] CommitSearchTypeStoredFilters = new ElasticSearchStoredFilterBuilder[SearchTypes.RegisteredSearchTypes.Count];

        /// <summary>
        /// Defines stored filter for declared definitions in the current commit
        /// </summary>
        /// <remarks>
        /// This is an array because Add takes an array not a single item
        /// </remarks>
        public readonly ElasticSearchStoredFilterBuilder[] DeclaredDefinitionStoredFilter;

        /// <summary>
        /// Empty set of stored filters for passing as additional stored filters
        /// </summary>
        internal readonly ElasticSearchStoredFilterBuilder[] EmptyStoredFilters = Array.Empty<ElasticSearchStoredFilterBuilder>();

        private readonly ConcurrentQueue<ValueTask<None>> backgroundTasks = new ConcurrentQueue<ValueTask<None>>();

        private readonly ElasticSearchService service;
        private readonly ElasticSearchStore store;
        private readonly SemaphoreSlim batchSemaphore;

        private ElasticSearchBatch currentBatch;
        private AtomicBool backgroundDequeueReservation = new AtomicBool();

        public ElasticSearchBatcher(ElasticSearchStore store, string commitFilterName, string repositoryFilterName, string cumulativeCommitFilterName)
        {
            Debug.Assert(store.Initialized, "Store must be initialized");

            this.store = store;
            batchSemaphore = new SemaphoreSlim(store.Configuration.MaxBatchConcurrency);
            service = store.Service;
            currentBatch = new ElasticSearchBatch(this);

            CommitSearchTypeStoredFilters = store.EntityStores.Select(entityStore =>
            {
                return new ElasticSearchStoredFilterBuilder(
                    entityStore, 
                    filterName: commitFilterName, 
                    unionFilterNames: new[] { repositoryFilterName, cumulativeCommitFilterName });
            }).ToArray();

            DeclaredDefinitionStoredFilter = new[] { new ElasticSearchStoredFilterBuilder(
                    store.DefinitionStore,
                    filterName: commitFilterName) };
        }

        public async ValueTask<None> AddAsync<T>(ElasticSearchEntityStore<T> store, T entity, Action onAdded = null, params ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            // This part must be synchronous since Add calls this method and
            // assumes that the content id and size will be set
            PopulateContentIdAndSize(entity, store);

            while (true)
            {
                var batch = currentBatch;
                if (!batch.TryAdd(store, entity, onAdded, additionalStoredFilters))
                {
                    await ExecuteBatchAsync(batch);
                }
                else
                {
                    // Added to batch so batch is not full. Don't need to execute batch yet so return.
                    return None.Value;
                }
            }
        }

        private async ValueTask<None> ExecuteBatchAsync(ElasticSearchBatch batch) 
        {
            if (batch.TryReserveExecute())
            {
                using (await batchSemaphore.AcquireAsync())
                {
                    currentBatch = new ElasticSearchBatch(this);
                    await service.UseClient(batch.ExecuteAsync);
                }

                if (backgroundDequeueReservation.TrySet(true))
                {
                    while (backgroundTasks.TryPeek(out var dequeuedTask))
                    {
                        if (dequeuedTask.IsCompleted)
                        {
                            backgroundTasks.TryDequeue(out dequeuedTask);
                        }
                        else
                        {
                            break;
                        }
                    }

                    backgroundDequeueReservation.TrySet(false);
                }
            }

            return None.Value;
        }

        public void Add<T>(ElasticSearchEntityStore<T> store, T entity, params ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            var backgroundTask = AddAsync(store, entity, additionalStoredFilters: additionalStoredFilters);
            if (!backgroundTask.IsCompleted)
            {
                backgroundTasks.Enqueue(backgroundTask);
            }
        }

        public void PopulateContentIdAndSize<T>(T entity, ElasticSearchEntityStore<T> store)
            where T : class, ISearchEntity
        {
            entity.PopulateContentIdAndSize();
        }

        public async Task FinalizeAsync()
        {
            while (true)
            {
                // Flush any background operations
                while (backgroundTasks.TryDequeue(out var backgroundTask))
                {
                    await backgroundTask;
                }

                if (currentBatch.EntityItems.Count == 0)
                {
                    break;
                }

                await ExecuteBatchAsync(currentBatch);
            }

            // Finalize the stored filters
            foreach (var filter in CommitSearchTypeStoredFilters.Concat(DeclaredDefinitionStoredFilter))
            {
                await filter.FinalizeAsync();
            }
        }
    }
}