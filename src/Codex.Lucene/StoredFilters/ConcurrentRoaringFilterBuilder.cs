using System.Collections.Concurrent;
using System.Diagnostics;
using Codex.Lucene.Formats;
using Codex.Utilities;

namespace Codex.Lucene
{
    public class ConcurrentRoaringFilterBuilder
    {
        private const int BatchSize = 10000;

        private readonly BatchQueue<int> Queue = new BatchQueue<int>(BatchSize);
        private object mutex = new object();
        public RoaringDocIdSet RoaringFilter { get; internal set; } = RoaringDocIdSet.Empty;

        public ConcurrentRoaringFilterBuilder(RoaringDocIdSet startFilter = null)
        {
            RoaringFilter = startFilter ?? RoaringDocIdSet.Empty;
        }

        public void Add(int id)
        {
            if (id < 0)
            {
                Debug.Fail($"{id}");
            }

            if (Queue.AddAndTryGetBatch(id, out var batch))
            {
                AddIds(batch);
            }
        }

        public void Complete()
        {
            while (Queue.TryGetBatch(out var batch))
            {
                AddIds(batch);
            }
        }

        public RoaringDocIdSet Build()
        {
            Complete();
            return RoaringFilter;
        }

        private void AddIds(List<int> batch)
        {
            lock (mutex)
            {
                batch.Sort();
                var filterBuilder = new RoaringDocIdSet.Builder();

                IEnumerable<int> ids = batch.SortedUnique(Comparer<int>.Default);

                if (RoaringFilter.Count != 0)
                {
                    ids = RoaringFilter.Enumerate().ExclusiveInterleave(ids, Comparer<int>.Default);
                }

                foreach (var id in ids)
                {
                    filterBuilder.Add(id);
                }

                RoaringFilter = filterBuilder.Build();
            }
        }
    }
}
