using System.Runtime.CompilerServices;
using BuildXL.Utilities.Collections;
using Codex.Utilities.Tasks;

namespace Codex.Utilities
{
    public static class Atomic
    {
        public static Task<T> RunOnceAsync<T, TData>(ref Task<T> taskSlot, TData data, Func<TData, Task<T>> runAsync)
        {
            if (TryReserveCompletion(ref taskSlot, out var completion, new(out var task)))
            {
                completion.LinkToTask(Out.Var(out task, Out.Invoke(async() =>
                {
                    return await runAsync(data);
                })));
            }

            return task;
        }

        public static async Task<TResult> RunOnceAsync<TKey, TResult, TData>(
            this ConcurrentBigMap<TKey, Task<TResult>> taskCompletionMap,
            TKey key,
            TData data, 
            Func<TKey, TData, Task<TResult>> runAsync,
            bool removeOnCompletion = false)
        {
            if (TryReserveCompletion(taskCompletionMap, key, out var task, out var completion))
            {
                completion.LinkToTask(Out.Var(out task, Out.Invoke(async () =>
                {
                    return await runAsync(key, data);
                })));
            }

            var result = await task;

            if (removeOnCompletion)
            {
                taskCompletionMap.RemoveKey(key);
            }

            return result;
        }

        public static bool TryReserveCompletion<TResult>(
                ref Task<TResult> taskSlot,
                out TaskSourceSlim<TResult> addedTaskCompletionSource,
                Out<Task<TResult>> task = default)
        {
            task.SetOrCreate(taskSlot);
            if (task.Value != null)
            {
                addedTaskCompletionSource = default;
                return false;
            }

            addedTaskCompletionSource = TaskSourceSlim.Create<TResult>();
            if (!Atomic.TryCompareExchange(ref taskSlot, addedTaskCompletionSource.Task, null, task))
            {
                addedTaskCompletionSource = default;
                return false;
            }

            task.Set(addedTaskCompletionSource.Task);
            return true;
        }

        public static bool TryReserveCompletion<TKey, TResult>(
            this ConcurrentBigMap<TKey, Task<TResult>> taskCompletionMap,
            TKey key,
            out Task<TResult> retrievedTask,
            out TaskSourceSlim<TResult> addedTaskCompletionSource)
        {
            Task<TResult> taskResult;
            if (taskCompletionMap.TryGetValue(key, out taskResult))
            {
                retrievedTask = taskResult;
                addedTaskCompletionSource = default;
                return false;
            }

            addedTaskCompletionSource = TaskSourceSlim.Create<TResult>();
            retrievedTask = taskCompletionMap.GetOrAdd(key, addedTaskCompletionSource.Task).Item.Value;

            if (retrievedTask != addedTaskCompletionSource.Task)
            {
                addedTaskCompletionSource = default;
                return false;
            }

            return true;
        }

        public static T CompareExchangeValue<T>(ref T location, T comparand, T value, Out<bool> exchanged = default)
            where T : unmanaged
        {
            if (Unsafe.SizeOf<T>() == sizeof(int))
            {
                var int_comparand = Unsafe.As<T, int>(ref comparand);
                var int_result = Interlocked.CompareExchange(
                    ref Unsafe.As<T, int>(ref location),
                    int_comparand,
                    Unsafe.As<T, int>(ref value));
                exchanged.Set(int_comparand == int_result);
                return Unsafe.As<int, T>(ref int_result);
            }

            if (Unsafe.SizeOf<T>() == sizeof(long))
            {
                var int_comparand = Unsafe.As<T, long>(ref comparand);
                var int_result = Interlocked.CompareExchange(
                    ref Unsafe.As<T, long>(ref location),
                    int_comparand,
                    Unsafe.As<T, long>(ref value));
                exchanged.Set(int_comparand == int_result);
                return Unsafe.As<long, T>(ref int_result);
            }

            throw new InvalidCastException();
        }

        public static bool TryCompareExchangeValue<T>(ref T location, T comparand, T value, Out<T> currentValue = default)
            where T : unmanaged
        {
            currentValue.Set(CompareExchangeValue(ref location, comparand, value, new(out var exchanged)));
            return exchanged;
        }

        public static bool TryCompareExchange<T>(ref T location, T value, T comparand, Out<T> currentValue = default)
            where T : class
        {
            var capturedValue = Interlocked.CompareExchange(ref location, value, comparand);
            currentValue.Set(capturedValue);
            return capturedValue == comparand;
        }

        public static bool TryCompareExchange(ref int location, int value, int comparand)
        {
            return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
        }

        public static bool TryCompareExchange(ref long location, long value, long comparand)
        {
            return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
        }

        public static T Create<T>(ref T location, T value)
            where T : class
        {
            return Interlocked.CompareExchange(ref location, value, null) ?? value;
        }

        public static void Max(ref int location, int value)
        {
            while (Out.TrueVar(out var current, location)
                && value > current
                && !TryCompareExchange(ref location, value, current))
            {
            }
        }

        public static int InterlockedIncrement(this ref int location) => Interlocked.Increment(ref location);
        public static int InterlockedDecrement(this ref int location) => Interlocked.Decrement(ref location);
        public static int InterlockedAdd(this ref int location, int value) => Interlocked.Add(ref location, value);
        public static long InterlockedAdd(this ref long location, long value) => Interlocked.Add(ref location, value);
        public static long InterlockedAdd(this ref TimeSpan location, TimeSpan value) => 
            Interlocked.Add(ref Unsafe.As<TimeSpan, long>(ref location), Unsafe.As<TimeSpan, long>(ref value));

        public static long InterlockedIncrement(this ref long location) => Interlocked.Increment(ref location);
        public static long InterlockedDecrement(this ref long location) => Interlocked.Decrement(ref location);
    }
}
