using System.Collections.Concurrent;

namespace Codex.Utilities
{
    public class RootFileSystem : SystemFileSystem
    {
        public readonly FileSystemFilter Filter;
        public readonly string RootDirectory;
        private readonly string SearchPattern;
        public List<string> Files;

        public bool DisableEnumeration { get; set; }

        public RootFileSystem(string rootDirectory, FileSystemFilter filter, string searchPattern = "*")
        {
            RootDirectory = rootDirectory;
            Filter = filter;
            SearchPattern = searchPattern;
        }

        public override IEnumerable<string> GetFiles()
        {
            if (DisableEnumeration)
            {
                return new string[0];
            }

            return GetFilesHelper(RootDirectory);
        }

        private IEnumerable<string> GetFilesHelper(string rootDirectory)
        {
            CompletionTracker tracker = new CompletionTracker();

            BlockingCollection<string> directories = new BlockingCollection<string>();
            BlockingCollection<string> files = new BlockingCollection<string>();
            QueueDirectory(rootDirectory, tracker, directories);

            tracker.PendingCompletion.ContinueWith(t => directories.CompleteAdding());

            ParallelConsume(directories, directory =>
            {
                try
                {
                    foreach (var childDirectory in Directory.GetDirectories(directory))
                    {
                        if (Filter.IncludeDirectory(this, childDirectory))
                        {
                            QueueDirectory(childDirectory, tracker, directories);
                        }
                    }

                    foreach (var file in Directory.GetFiles(directory, SearchPattern))
                    {
                        if (Filter.IncludeFile(this, file))
                        {
                            files.Add(file);
                        }
                    }
                }
                finally
                {
                    CompleteDirectory(directory, tracker);
                }
            }, () =>
            {
                files.CompleteAdding();
            });

            return files.GetConsumingEnumerable();
        }

        private void QueueDirectory(string directory, CompletionTracker tracker, BlockingCollection<string> directories)
        {
            tracker.OnStart();
            directories.Add(directory);
        }

        private void CompleteDirectory(string directory, CompletionTracker tracker)
        {
            tracker.OnComplete();
        }

        private async void ParallelConsume<T>(BlockingCollection<T> collection, Action<T> action, Action completion)
        {
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    T item;
                    while (collection.TryTake(out item, Timeout.Infinite))
                    {
                        action(item);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            completion();
        }
    }
}