using System;
using System.Threading.Tasks;
using Codex.Utilities;
using Codex.Utilities.Tasks;
using static Codex.Utilities.TaskUtilities;

namespace Codex.Import
{
    public class TaskDispatcher
    {
        private readonly PriorityActionQueue actionQueue;
        private CompletionTracker tracker;
        private bool[] allowedTypes;

        public TaskDispatcher(int? maxParallelism = null)
        {
            allowedTypes = new bool[4];
            SetAllowedTaskTypes(t => true);
            tracker = new CompletionTracker();
            actionQueue = new PriorityActionQueue(
                    maxDegreeOfParallelism: maxParallelism ?? Environment.ProcessorCount + 2,
                    tracker: tracker,
                    priorityCount: Enum.GetValues(typeof(TaskType)).Length/*,
                    boundedCapacity: 128*/);
        }

        public CompletionTracker.CompletionHandle TrackScope()
        {
            return tracker.TrackScope();
        }

        public void SetAllowedTaskTypes(Func<TaskType, bool> isAllowed)
        {
            foreach (TaskType taskType in Enum.GetValues(typeof(TaskType)))
            {
                SetAllowedTaskType(taskType, isAllowed(taskType));
            }
        }

        public void SetAllowedTaskType(TaskType type, bool allowed)
        {
            allowedTypes[(int)type] = allowed;
        }

        public void CheckAllowed(TaskType type)
        {
            if(!allowedTypes[(int)type])
            {
                throw new Exception($"Task type '{type}' is not allowed");
            }
        }

        public Task OnCompletion()
        {
            return actionQueue.PendingCompletion;
        }

        public void QueueInvoke(Action action, TaskType type = TaskType.Analysis)
        {
            Invoke(action, type).IgnoreAsync();
        }

        public void QueueInvoke(Func<Task> asyncAction, TaskType type = TaskType.Analysis)
        {
            Invoke(asyncAction, type).IgnoreAsync();
        }

        public void QueueInvoke<T>(Func<Task<T>> asyncAction, TaskType type = TaskType.Analysis)
        {
            Invoke(asyncAction, type).IgnoreAsync();
        }

        public Task Invoke(Action action, TaskType type = TaskType.Analysis)
        {
            CheckAllowed(type);
            return actionQueue.Execute(() =>
                {
                    action();
                    return Task.FromResult(true);
                }, (int)type);
        }

        public Task Invoke(Func<Task> asyncAction, TaskType type = TaskType.Analysis)
        {
            CheckAllowed(type);
            return actionQueue.Execute(asyncAction, (int)type);
        }

        public Task<T> Invoke<T>(Func<Task<T>> asyncAction, TaskType type = TaskType.Analysis)
        {
            CheckAllowed(type);
            return actionQueue.Execute(asyncAction, (int)type);
        }
    }
}
