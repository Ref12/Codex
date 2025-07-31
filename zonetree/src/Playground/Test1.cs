﻿using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Collections.BTree;
using Tenray.ZoneTree.Collections.BTree.Lock;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Playground;

public sealed class Test1
{
    public static void SeveralParallelTransactions()
    {
        var dataPath = "../../data/SeveralParallelTransactions";
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        int n = 1000000;
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogOptions(x =>
            {
                x.WriteAheadLogMode = WriteAheadLogMode.Sync;
                x.EnableIncrementalBackup = true;
            })
            .OpenOrCreateTransactional();
        using var basicMaintainer = new ZoneTreeMaintainer<int, int>(zoneTree);
        
        Console.WriteLine("Loaded: " + stopWatch.ElapsedMilliseconds);

        stopWatch.Restart();
        var uncommitted = zoneTree.Maintenance.TransactionLog.UncommittedTransactionIds;
        Console.WriteLine($"found {uncommitted.Count} uncommitted transactions.");
        foreach (var u in uncommitted)
        {
            zoneTree.Rollback(u);
        }
        Console.WriteLine("uncommitted transactions are rollbacked. Elapsed:"
            + stopWatch.ElapsedMilliseconds);
        stopWatch.Restart();

        var transactional = true;
        if (transactional)
        {
            Parallel.For(0, n, (x) =>
            {
                var tx = zoneTree.BeginTransaction();
                zoneTree.Upsert(tx, x, x + x);
                zoneTree.Upsert(tx, -x, -x - x);
                zoneTree.Prepare(tx);
                zoneTree.Commit(tx);
            });
        }
        else
        {
            var data = zoneTree.Maintenance.ZoneTree;
            Parallel.For(0, n, (x) =>
            {
                data.Upsert(x, x + x);
                data.Upsert(-x, -x - x);
            });
        }
        Console.WriteLine("Elapsed: " + stopWatch.ElapsedMilliseconds);
        basicMaintainer.CompleteRunningTasks();
        zoneTree.Maintenance.SaveMetaData();
    }

    public static void TestReverseIterator(
        int count = 1_000_000,
        bool recreate = false)
    {
        var dataPath = "../../data/TestReverseIterator";
        if (recreate && Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var stopwatchAll = Stopwatch.StartNew();

        var upload = Stopwatch.StartNew();
        IZoneTree<int, string> GetZoneTree() => new ZoneTreeFactory<int, string>()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogOptions(x =>
            {
                x.CompressionBlockSize = 1024 * 1024 * 20;
                x.WriteAheadLogMode = WriteAheadLogMode.AsyncCompressed;
            })
            .ConfigureDiskSegmentOptions(x =>
            {
                x.EnableCompression = true;
                x.DiskSegmentMode = DiskSegmentMode.MultiPartDiskSegment;
                x.CompressionBlockSize = 1024 * 1024 * 20;
                x.MinimumRecordCount = 10_000;
                x.MaximumRecordCount = 100_000;
            })
            .OpenOrCreate();

        using (var zoneTree1 = GetZoneTree())
        {
            using var maintainer = zoneTree1.CreateMaintainer();
            maintainer.ThresholdForMergeOperationStart = 300000;
            for (int i = 0; i < 1_000_000; i++)
            {
                zoneTree1.Upsert(i, string.Join('+',
                    Enumerable.Repeat(Guid.NewGuid().ToString(), 4)));
                if (i % 100 == 0)
                    zoneTree1.TryAtomicAddOrUpdate(i, "a", bool (ref string x) =>
                    {
                        x += " ooops!";
                        return true;
                    });
            }
            maintainer.CompleteRunningTasks();
        }

        upload.Stop();
        Console.WriteLine($"Upload {upload.Elapsed}");

        var download = Stopwatch.StartNew();
        int iterateCount = 0;
        using (var zoneTree2 = GetZoneTree())
        {
            using var iterator = zoneTree2.CreateIterator();
            while (iterator.Next())
            {
                var key = iterator.CurrentKey;
                var value = iterator.CurrentValue;
                if (value.Length > 0)
                    iterateCount++;
            }
        }
        download.Stop();
        Console.WriteLine($"Download {download.Elapsed}");

        var reverse = Stopwatch.StartNew();

        using (var zoneTree3 = GetZoneTree())
        {
            using var reverseIterator = zoneTree3.CreateReverseIterator();
            while (reverseIterator.Next())
            {
                var key = reverseIterator.CurrentKey;
                var value = reverseIterator.CurrentValue;
                if (value.Length > 0)
                    iterateCount++;
            }
        }
        reverse.Stop();
        Console.WriteLine($"Reverse {reverse.Elapsed}");

        if (2 * count != iterateCount)
        {
            throw new Exception($"iterateCount != {2*count} " + iterateCount);
        }
        stopwatchAll.Stop();
        Console.WriteLine($"All Time:{stopwatchAll.Elapsed}");
    }

    public static void MassiveInsertsAndReads(long[] arr, BTreeLockMode lockMode = BTreeLockMode.NodeLevelMonitor)
    {
        var readCount = 0;
        var tree = new BTree<long, long>(new Int64ComparerAscending(),
            lockMode);
        var task1 = Parallel.ForEachAsync(arr, (i, t) =>
        {
            tree.TryInsert(i, i, out _);
            return ValueTask.CompletedTask;
        });
        Thread.Sleep(1);
        var task2 = Parallel.ForEachAsync(arr, (i, t) =>
        {
            if (tree.TryGetValue(i, out var j))
            {
                if (i != j)
                    throw new Exception($"{i} != {j}");
                Interlocked.Increment(ref readCount);
            }
            return ValueTask.CompletedTask;
        });
        task1.Wait();
        task2.Wait();
        Console.WriteLine("Read Count: " + readCount);
    }

    public static void TestIteratorBehavior(
        int count = 10_000_000,
        bool recreate = true)
    {
        var dataPath = "../../data/TestIteratorBehavior";
        if (recreate && Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        IZoneTree<int, int> GetZoneTree() => new ZoneTreeFactory<int, int>()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogOptions(x =>
            {
                x.CompressionBlockSize = 1024 * 1024 * 20;
                x.WriteAheadLogMode = WriteAheadLogMode.None;
            })
            .ConfigureDiskSegmentOptions(x =>
            {
                x.EnableCompression = true;
                x.DiskSegmentMode = DiskSegmentMode.SingleDiskSegment;
            })
            .OpenOrCreate();

        using var zoneTree1 = GetZoneTree();
        using var maintainer = zoneTree1.CreateMaintainer();
        var t1 = Task.Run(() =>
        {
            for(var i = 0; i < count; ++i)
            {
                zoneTree1.Upsert(i, i);
            }
        });
        bool reverse = false;
        Thread.Sleep(500);
        int iteratorCount = 100;
        Parallel.For(0, iteratorCount, (x) =>
        {
            var c = zoneTree1.Count();
            var s = 0;
            Console.WriteLine("count:" + c);
            using var it = reverse ? 
                zoneTree1.CreateReverseIterator() :
                zoneTree1.CreateIterator();
            it.Next();
            ++s;
            var p = it.CurrentKey;
            Console.WriteLine(p);
            while (it.Next())
            {
                ++s;
                var k = it.CurrentKey;
                if (Math.Abs(k - p) > 1)
                {
                    Console.WriteLine($"{k} - {k-p}");
                }
                p = k;
            }
            Console.WriteLine(p);
            if (s < c)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{s} < {c}");
            }
        });
        t1.Wait();
        zoneTree1.Maintenance.TryCancelMergeOperation();
        maintainer.CompleteRunningTasks();
    }


    public static void TestTreeIteratorBehavior()
    {
        int count = 10_000_000;
        var tree = new BTree<int, int>(
            new Int32ComparerAscending(), BTreeLockMode.NodeLevelMonitor);
        var t1 = Task.Run(() =>
        {
            for (var i = 0; i < count; ++i)
            {
                tree.Upsert(i, i, out _);
            }
        });
        int iteratorCount = 100;
        Parallel.For(0, iteratorCount, (x) =>
        {
            var c = tree.Length;
            var s = 0;
            Console.WriteLine("count:" + c);
            var it = new BTreeSeekableIterator<int, int>(tree);

            it.Next();
            ++s;
            var p = it.CurrentKey;
            while (it.Next())
            {
                ++s;
                var k = it.CurrentKey;
                if (Math.Abs(k - p) > 1)
                {
                    throw new Exception($"iterator jump detected: {k} - {k - p}");
                }
                p = k;
            }
            if (s < c)
            {
                throw new Exception($"count validation failed: {s} < {c}");
            }
        });
        t1.Wait();
    }

    public static void TestTreeReverseIteratorBehavior()
    {
        int count = 10_000_000;
        var tree = new BTree<int, int>(
            new Int32ComparerAscending(), BTreeLockMode.NodeLevelMonitor);
        var t1 = Task.Run(() =>
        {
            for (var i = 0; i < count; ++i)
            {
                tree.Upsert(i, i, out _);
            }
        });
        int iteratorCount = 100;
        Parallel.For(0, iteratorCount, (x) =>
        {
            var c = tree.Length;
            var s = 0;
            Console.WriteLine("count:" + c);
            var it = new BTreeSeekableIterator<int, int>(tree);
            it.SeekEnd();
            ++s;
            var p = it.CurrentKey;
            while (it.Prev())
            {
                ++s;
                var k = it.CurrentKey;
                if (Math.Abs(k - p) > 1)
                {
                    throw new Exception($"iterator jump detected: {k} - {k - p}");
                }
                p = k;
            }
            if (s < c)
            {
                throw new Exception($"count validation failed: {s} < {c}");
            }
        });
        t1.Wait();
    }
}