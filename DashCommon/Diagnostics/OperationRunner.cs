//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Common.Diagnostics
{
    public class OperationRunner : IDisposable
    {
        Stopwatch _watch;
        string _operation;
        bool _logSuccess;

        public static OperationRunner Start(string operation, bool? logSuccess = null)
        {
            return new OperationRunner(operation, logSuccess);
        }

        public static async Task<T> DoActionAsync<T>(string operation, Func<Task<T>> action)
        {
            return await DoActionAsync(operation, action, (ex) => default(T));
        }

        public static async Task DoActionAsync(string operation, 
            Func<Task> action, 
            Action<StorageException> storageExceptionHandler = null, 
            bool rethrowAllOtherExceptions = true, 
            bool? logSuccess = null)
        {
            await DoActionAsync(operation, async () =>
                {
                    await action();
                    return true;
                },
                (ex) =>
                {
                    if (storageExceptionHandler != null)
                    {
                        storageExceptionHandler(ex);
                    }
                    return true;
                },
                rethrowAllOtherExceptions);
        }

        public static async Task<T> DoActionAsync<T>(string operation, 
            Func<Task<T>> action, 
            Func<StorageException, T> storageExceptionHandler, 
            bool rethrowAllOtherExceptions = true, 
            bool? logSuccess = null)
        {
            using (var runner = OperationRunner.Start(operation))
            {
                try
                {
                    return await action();
                }
                catch (StorageException ex)
                {
                    runner.Success = false;
                    DashTrace.TraceWarning(new TraceMessage
                    {
                        Operation = "Storage Exception",
                        Message = operation,
                        ErrorDetails = DashErrorInformation.Create(ex.RequestInformation.ExtendedErrorInformation),
                    });
                    return storageExceptionHandler(ex);
                }
                catch (Exception ex)
                {
                    runner.Success = false;
                    DashTrace.TraceWarning(new TraceMessage
                    {
                        Operation = "General Exception",
                        Message = operation,
                        ErrorDetails = new DashErrorInformation { ErrorMessage = ex.ToString() },
                    });
                    if (rethrowAllOtherExceptions)
                    {
                        throw;
                    }
                    return default(T);
                }
            }
        }

        protected OperationRunner(string operation, bool? logSuccess = null)
        {
            _operation = operation;
            if (logSuccess.HasValue)
            {
                _logSuccess = logSuccess.Value;
            }
            else
            {
                _logSuccess = DashConfiguration.LogNormalOperations;
            }
            if (_logSuccess)
            {
                DashTrace.TraceInformation(new TraceMessage
                {
                    Operation = "Start",
                    Message = operation,
                });
            }
            this.Success = true;
            _watch = Stopwatch.StartNew();
        }

        public bool Success { get; set; }

        public void Dispose()
        {
            _watch.Stop();
            if (this._logSuccess || !this.Success)
            {
                DashTrace.TraceInformation(new TraceMessage
                {
                    Operation = "Completed",
                    Success = this.Success,
                    Duration = _watch.ElapsedMilliseconds,
                    Message = this._operation,
                });
            }
        }

        public long ElapsedMilliseconds
        {
            get { return this._watch.ElapsedMilliseconds; }
        }
    }
}