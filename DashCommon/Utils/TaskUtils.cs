//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Utils
{
    public static class TaskUtils
    {
        public class Result<T>
        {
            public int CompletedIndex { get; internal set; }
            public T Value { get; internal set; }
        }

        public static Result<T> WaitAnyWithPredicate<T>(Func<T, bool> predicate, params Func<T>[] actions)
        {
            var tcs = new TaskCompletionSource<Tuple<int, T>>();
            var allComplete = Task.WhenAll(actions.Select((action, index) => StartAction(tcs, predicate, action, index)));
            Task.WaitAny(tcs.Task, allComplete);
            if (tcs.Task.Status == TaskStatus.RanToCompletion)
            {
                return new Result<T>
                {
                    CompletedIndex = tcs.Task.Result.Item1,
                    Value = tcs.Task.Result.Item2,
                };
            }
            return new Result<T>
            {
                CompletedIndex = -1,
            };
        }

        private static Task StartAction<T>(TaskCompletionSource<Tuple<int, T>> tcs, Func<T, bool> predicate, Func<T> action, int index)
        {
            return Task.Factory.StartNew(() => action())
                .ContinueWith(completedTask =>
                    {
                        if (completedTask.Status == TaskStatus.RanToCompletion && predicate(completedTask.Result))
                        {
                            tcs.TrySetResult(Tuple.Create(index, completedTask.Result));
                        }
                    });
        }
    }
}
