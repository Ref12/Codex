﻿using Codex.ElasticSearch.Formats;
using Codex.ElasticSearch.Store;
using Codex.ElasticSearch.Store.Scripts;
using Codex.ElasticSearch.Utilities;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Storage.ElasticProviders;
using Codex.Utilities;
using Nest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    public class ElasticSearchIdRegistry : IStableIdRegistry
    {
        public const int ReserveCount = 500;
        public const int Version = 1;

        private readonly ElasticSearchStore store;
        private readonly ElasticSearchService service;
        private readonly ElasticSearchEntityStore entityStore;
        private readonly ElasticSearchEntityStore<IStableIdMarker> idStore;
        private readonly ElasticSearchEntityStore<IRegisteredEntity> registryStore;
        private readonly IndexIdRegistry[] indexRegistries;
        private readonly StoredFilterManager storedFilterManager;
        private readonly string committedFilterBucketName;
        private readonly Logger logger;

        public ElasticSearchIdRegistry(ElasticSearchStore store, Guid? ingestId = null)
        {
            this.store = store;
            logger = store.Configuration.Logger;
            this.service = store.Service;
            this.idStore = store.StableIdMarkerStore;
            this.registryStore = store.RegisteredEntityStore;
            this.indexRegistries = SearchTypes.RegisteredSearchTypes.Select(searchType => new IndexIdRegistry(this, searchType)).ToArray();
            this.entityStore = store.StableIdMarkerStore;
            storedFilterManager = new StoredFilterManager(store.StoredFilterStore);
            committedFilterBucketName = (ingestId ?? Guid.NewGuid()).ToString().Substring(0, 4);
        }

        public async Task FinalizeAsync()
        {
            foreach (var indexRegistry in indexRegistries)
            {
                await indexRegistry.FinalizeAsync();
            }
        }

        public async Task CompleteReservations(
            SearchType searchType, 
            IReadOnlyList<string> completedReservations, 
            IReadOnlyList<int> unusedIds = null)
        {
            completedReservations = completedReservations ?? CollectionUtilities.Empty<string>.Array;
            unusedIds = unusedIds ?? CollectionUtilities.Empty<int>.Array;

            var response = await this.service.UseClient(context =>
            {
                var client = context.Client;
                string stableIdMarkerId = searchType.IndexName;
                return client.UpdateAsync<IStableIdMarker, IStableIdMarker>(stableIdMarkerId,
                    ud => ud
                    .Index(entityStore.IndexName)
                    .Upsert(new StableIdMarker())
                    .ScriptedUpsert()
                    .Source()
                    .RetryOnConflict(10)
                    .Script(sd => sd
                        .Source(Scripts.CommitReservation)
                        .Lang(ScriptLang.Painless)
                        .Params(pd => pd
                            .Add("reservationIds", completedReservations)
                            .Add("returnedIds", unusedIds))).CaptureRequest(context))
                    .ThrowOnFailure();
            });

            var stableIdMarkerDocument = response.Result.Get.Source;
        }

        public async Task SetStableIdsAsync(IReadOnlyList<IStableIdItem> items)
        {
            // For each item, reserve ids from the {IndexName}:{StableIdGroup} StableIdMarker document and assign tentative ids for each
            // item
            // Next attempt to get or add a document with Uid = {IndexName}.{Uid}, StableIdGroup, StableId
            // Assign the StableId of the final document after the get or add to the item

            var registeredEntities = new RegisteredEntity[items.Count];
            var reservations = new (ReservationNode node, int stableId)[items.Count];

            var bd = new BulkDescriptor();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var indexRegistry = indexRegistries[item.SearchType.Id];
                var nodeAndStableId = await indexRegistry.GetReservationNodeAndStableId();
                reservations[i] = nodeAndStableId;

                var version = StoredFilterUtilities.ComputeVersion(nodeAndStableId.stableId);

                var registeredEntity = new RegisteredEntity()
                {
                    Uid = GetRegistrationUid(item.SearchType, item.Uid),
                    StableId = nodeAndStableId.stableId,
                    DateAdded = DateTime.UtcNow,
                    EntityVersion = version,
                    IndexName = item.SearchType.IndexName,
                    EntityUid = item.Uid
                };

                registeredEntities[i] = registeredEntity;
                registryStore.AddIndexOperation(bd, registeredEntity);
            }

            var registerResponse = await store.Service.UseClient(context => context.Client.BulkAsync(bd.CaptureRequest(context))
                    .ThrowOnFailure(allowInvalid: true));

            int index = 0;
            foreach (var registerResponseItem in registerResponse.Result.Items)
            {
                var item = items[index];
                var indexRegistry = indexRegistries[item.SearchType.Id];
                var nodeAndStableId = reservations[index];
                var node = nodeAndStableId.node;
                var stableId = nodeAndStableId.stableId;

                bool added = IsAdded(registerResponseItem);
                bool committed = false;
                item.IsAdded = added;
                if (added)
                {
                    item.StableId = stableId;
                    node.CommitId();
                }
                else
                {
                    var knownStableId = StoredFilterUtilities.ExtractStableId(registerResponseItem.Version);
                    committed = indexRegistry.Filter.RoaringFilter.Contains(knownStableId);
                    item.IsCommitted = committed;
                    item.StableId = knownStableId;
                    node.ReturnId(stableId);
                }

                if (item.SearchType == SearchTypes.Definition)
                {
                    logger.LogDiagnosticWithProvenance2($"Uid={item.Uid}, Sid={item.StableId}, Rid={stableId}, Cmt={committed}, Rsp={registerResponseItem.Status} [Err={registerResponseItem.Error != null}]");
                }

                index++;
            }
        }

        private bool IsAdded(IBulkResponseItem item)
        {
            return item.Status == (int)HttpStatusCode.Created;
        }

        public async Task<IStableIdReservation> ReserveIds(SearchType searchType)
        {
            string reservationId = Guid.NewGuid().ToString();
            var response = await this.service.UseClient(context =>
            {
                var client = context.Client;
                string stableIdMarkerId = searchType.IndexName;
                return client.UpdateAsync<IStableIdMarker, IStableIdMarker>(stableIdMarkerId, 
                    ud => ud
                    .Index(entityStore.IndexName)
                    .Upsert(new StableIdMarker())
                    .ScriptedUpsert()
                    .Source()
                    .RetryOnConflict(10)
                    .Script(sd => sd
                        .Source(Scripts.Reserve)
                        .Lang(ScriptLang.Painless)
                        .Params(pd => pd
                            .Add("reservationId", reservationId)
                            .Add("reserveCount", ReserveCount))).CaptureRequest(context))
                    .ThrowOnFailure();
            });

            var stableIdMarkerDocument = response.Result.Get.Source;
            return stableIdMarkerDocument.PendingReservations.Where(r => r.ReservationId == reservationId).Single();
        }

        private static string GetRegistrationUid(SearchType searchType, string entityUid)
        {
            return $"{searchType.IndexName}:{entityUid}";
        }

        public Task CommitStableIdsAsync(IReadOnlyList<IStableIdItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var indexRegistry = indexRegistries[item.SearchType.Id];
                var filter = indexRegistry.Filter;
                Contract.Assert(filter != null, "Reserve must have been called before calling commit");
                filter.Add(item.StableId);
            }

            return Task.CompletedTask;
        }

        private class ReservationNode
        {
            public readonly IStableIdReservation IdReservation;
            private readonly HashSet<int> remainingIds;
            private int committedIdCount;
            public ReservationNode Next;

            public ReservationNode(IStableIdReservation idReservation, ReservationNode next)
            {
                IdReservation = idReservation;
                Next = next;
                remainingIds = new HashSet<int>(idReservation.ReservedIds);
            }

            public bool TryTakeId(out ReservationNode node, out int id)
            {
                lock (remainingIds)
                {
                    foreach (var remainingId in remainingIds)
                    {
                        id = remainingId;
                        node = this;
                        remainingIds.Remove(remainingId);
                        return true;
                    }
                }

                var next = Next;
                if (next == null)
                {
                    id = 0;
                    node = null;
                    return false;
                }
                else
                {
                    return next.TryTakeId(out node, out id);
                }
            }

            public void ReturnId(int id)
            {
                lock (remainingIds)
                {
                    remainingIds.Add(id);
                }
            }

            public void CommitId()
            {
                Interlocked.Increment(ref committedIdCount);
            }

            public ReservationNode CollectCompletedReservationsAndGetNextUncompleted(ref List<string> committedNodes, List<int> finalizationUnusedIds)
            {
                bool isFinalizing = finalizationUnusedIds != null;
                if (committedIdCount == IdReservation.ReservedIds.Count || isFinalizing)
                {
                    // Node is committed since all reserved ids are committed
                    if (committedNodes == null)
                    {
                        committedNodes = new List<string>();
                    }

                    finalizationUnusedIds?.AddRange(remainingIds);
                    committedNodes.Add(this.IdReservation.ReservationId);
                    return Next?.CollectCompletedReservationsAndGetNextUncompleted(ref committedNodes, finalizationUnusedIds);
                }
                else
                {
                    if (Next != null)
                    {
                        Next = Next.CollectCompletedReservationsAndGetNextUncompleted(ref committedNodes, finalizationUnusedIds);
                    }

                    return this;
                }
            }
        }

        private class IndexIdRegistry
        {
            private readonly ElasticSearchIdRegistry idRegistry;
            public readonly SearchType SearchType;
            private ReservationNode indexReservationNode = null;
            private Lazy<Task<ReservationNode>> reservationTask;
            private readonly string committedStableIdsKey;
            public ConcurrentRoaringFilterBuilder Filter { get; private set; }

            public IndexIdRegistry(ElasticSearchIdRegistry idRegistry, SearchType searchType)
            {
                this.idRegistry = idRegistry;
                this.SearchType = searchType;
                committedStableIdsKey = $"{searchType.IndexName}:{Version}.CommittedIds";
            }

            public async ValueTask<(ReservationNode node, int stableId)> GetReservationNodeAndStableId()
            {
                var node = indexReservationNode;
                if (node != null)
                {
                    if (node.TryTakeId(out var reservedNode, out var stableId))
                    {
                        return (reservedNode, stableId);
                    }
                }

                var lazyReservation = ThreadingUtilities.CompareSet(ref reservationTask, new Lazy<Task<ReservationNode>>(() => CreateReservationNode()), null);

                while (true)
                {
                    node = await lazyReservation.Value;
                    if (node.TryTakeId(out var reservedNode, out var stableId))
                    {
                        return (reservedNode, stableId);
                    }
                    else
                    {
                        lazyReservation = ThreadingUtilities.CompareSet(ref reservationTask,
                            new Lazy<Task<ReservationNode>>(() => CreateReservationNode(next: node)),
                            lazyReservation);
                    }
                }

                throw new Exception("Unreachable");
            }

            private async Task<ReservationNode> CreateReservationNode(ReservationNode next = null)
            {
                if (Filter == null)
                {
                    var storedFilter = await idRegistry.storedFilterManager.TryGetStoredFilterById(committedStableIdsKey);
                    if (storedFilter?.StableIds == null)
                    {
                        Filter = new ConcurrentRoaringFilterBuilder();
                    }
                    else
                    {
                        Filter = new ConcurrentRoaringFilterBuilder(RoaringDocIdSet.FromBytes(storedFilter.StableIds));
                    }
                }

                var reservation = await idRegistry.ReserveIds(SearchType);

                next = await CompletePendingReservations(next, finalizationUnusedIds: null);

                var node = new ReservationNode(reservation, next);
                indexReservationNode = node;
                return node;
            }

            private async Task<ReservationNode> CompletePendingReservations(ReservationNode node, List<int> finalizationUnusedIds)
            {
                List<string> completedReservations = null;
                node = node?.CollectCompletedReservationsAndGetNextUncompleted(ref completedReservations, finalizationUnusedIds: finalizationUnusedIds);
                if (completedReservations != null)
                {
                    await idRegistry.CompleteReservations(SearchType, completedReservations, unusedIds: finalizationUnusedIds);
                }

                return node;
            }

            public async Task FinalizeAsync()
            {
                if (Filter != null)
                {
                    Filter.Complete();
                    await idRegistry.storedFilterManager.AddStoredFilterAsync(committedStableIdsKey, idRegistry.committedFilterBucketName,
                        new StoredFilter()
                        {
                            DateUpdated = DateTime.UtcNow
                        }
                        .ApplyStableIds(Filter.RoaringFilter)
                        .PopulateContentIdAndSize());
                }

                var reservationNode = indexReservationNode;
                if (reservationNode != null)
                {
                    await CompletePendingReservations(reservationNode, finalizationUnusedIds: new List<int>());
                }
            }
        }
    }
}
