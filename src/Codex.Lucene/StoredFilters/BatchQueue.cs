using System.Collections.Concurrent;

namespace Codex.Lucene
{
    public class BatchQueue<T>
    {
        private int totalCount;
        private int batchSize;
        private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

        public BatchQueue(int batchSize)
        {
            this.batchSize = batchSize;
        }

        public int TotalCount => totalCount;

        public bool TryGetBatch(out List<T> batch)
        {
            List<T> batchList = new List<T>(batchSize);
            for (int i = 0; i < batchSize; i++)
            {
                T dequeuedItem;
                if (queue.TryDequeue(out dequeuedItem))
                {
                    batchList.Add(dequeuedItem);
                }
                else
                {
                    break;
                }
            }

            if (batchList.Count != 0)
            {
                batch = batchList;
                return true;
            }

            batch = null;
            return false;
        }

        public bool AddAndTryGetBatch(T item, out List<T> batch)
        {
            queue.Enqueue(item);
            var updatedTotalCount = Interlocked.Increment(ref totalCount);
            if (updatedTotalCount % batchSize == 0)
            {
                return TryGetBatch(out batch);
            }

            batch = null;
            return false;
        }
    }
}
