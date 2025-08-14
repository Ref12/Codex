// --------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// --------------------------------------------------------------------

namespace Codex.Utilities
{
    public struct Taskish(ValueTask valueTask)
    {
        public static implicit operator Taskish(ValueTask task) => new(valueTask: task);
        public static implicit operator Taskish(Task task) => new(valueTask: new(task));

        public Task AsTask() => valueTask.AsTask();

        public bool IsCompletedSuccessfully => valueTask.IsCompletedSuccessfully;

        public ValueTask ContinueWith(Action<ValueTask> continuation)
        {
            if (IsCompletedSuccessfully)
            {
                try
                {
                    continuation(valueTask);
                    return ValueTask.CompletedTask;
                }
                catch (Exception ex)
                {
                    return ValueTask.FromException(ex);
                }
            }

            return valueTask.AsTask().ContinueWith(static (task, continuation) => ((Action<ValueTask>)continuation).Invoke(new(task)), continuation).ToValueTask();
        }
    }
}