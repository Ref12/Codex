using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Utilities
{
    public class ObjectPool<T>
    {
        private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();
        private readonly Func<T> create;
        private readonly Action<T> clean;
        private readonly Action<T> prepare;

        public ObjectPool(Func<T> create, Action<T> clean = null, Action<T> prepare = null)
        {
            this.create = create;
            this.clean = clean;
            this.prepare = prepare;
        }

        public T Get()
        {
            var result = getCore();
            prepare?.Invoke(result);
            return result;

            T getCore()
            {
                if (queue.TryDequeue(out var item))
                {
                    return item;
                }
                else
                {
                    return create();
                }
            }
        }

        public Lease Acquire()
        {
            return new Lease(this, Get());
        }

        public Lease Acquire(out T instance)
        {
            var lease = Acquire();
            instance = lease.Instance;
            return lease;
        }

        public void Return(T item)
        {
            clean?.Invoke(item);
            queue.Enqueue(item);
        }

        public struct Lease : IDisposable
        {
            private readonly ObjectPool<T> pool;
            public readonly T Instance;

            internal Lease(ObjectPool<T> pool, T item)
            {
                this.pool = pool;
                Instance = item;
            }

            public void Dispose()
            {
                pool.Return(Instance);
            }
        }
    }
}
