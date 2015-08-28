//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Microsoft.Dash.Common.Platform
{
    public class AzureMessageQueue : IMessageQueue
    {
        private static readonly int _timeout    = DashConfiguration.AsyncWorkerTimeout;
        private static readonly int _dequeLimit = DashConfiguration.WorkerQueueDequeueLimit;

        static ISet<Tuple<string, string>> _queuesCheckedForExistance = new HashSet<Tuple<string, string>>();

        internal CloudQueue Queue { get; set; }

        public AzureMessageQueue(CloudStorageAccount namespaceAccount = null, string queueName = null)
        {
            if (String.IsNullOrWhiteSpace(queueName))
            {
                queueName = DashConfiguration.WorkerQueueName;
            }
            this.Queue = NamespaceHandler.GetQueueByName(namespaceAccount ?? DashConfiguration.NamespaceAccount, queueName);
            // Performance optimization - We only check once if the queue has been created
            var queueKey = Tuple.Create((namespaceAccount ?? DashConfiguration.NamespaceAccount).Credentials.AccountName.ToLowerInvariant(), queueName.ToLowerInvariant());
            if (!_queuesCheckedForExistance.Contains(queueKey))
            {
                lock (this)
                {
                    if (!_queuesCheckedForExistance.Contains(queueKey))
                    {
                        _queuesCheckedForExistance.Add(queueKey);
                        this.Queue.CreateIfNotExists();
                    }
                }
            }
        }

        public AzureMessageQueue(QueueMessage sourceMessage)
        {
            this.Queue = ((AzureMessageItem)sourceMessage.MessageItem).ContainingQueue.Queue;
        }

        public void Enqueue(QueueMessage payload, int? initialInvisibilityDelay = null)
        {
            EnqueueAsync(payload, initialInvisibilityDelay).Wait();
        }

        public async Task EnqueueAsync(QueueMessage payload, int? initialInvisibilityDelay = null)
        {
            CloudQueueMessage message = new CloudQueueMessage(payload.ToJson());
            TimeSpan? invisibilityDelay = TimeSpan.FromSeconds(initialInvisibilityDelay ?? DashConfiguration.WorkerQueueInitialDelay);
            await this.Queue.AddMessageAsync(message, null, invisibilityDelay, null, null);
        }

        public QueueMessage Dequeue(int? invisibilityTimeout = null)
        {
            while (true)
            {
                invisibilityTimeout = invisibilityTimeout ?? _timeout;
                if (invisibilityTimeout.Value < 1)
                {
                    invisibilityTimeout = 1;
                }
                var message = Queue.GetMessage(new TimeSpan(0, 0, invisibilityTimeout.Value));
                if (message != null)
                {
                    var payload = JsonConvert.DeserializeObject<QueueMessage>(message.AsString);
                    payload.MessageItem = new AzureMessageItem
                    {
                        ContainingQueue = this,
                        Message = message,
                    };
                    if (message.DequeueCount >= _dequeLimit)
                    {
                        DashTrace.TraceWarning("Discarding message after exceeding deque limit of {0}. Message details: {1}",
                            message.DequeueCount,
                            message.AsString);
                        // We actually let this message go around after flagging that the operation is to be abandoned. This allows
                        // operation processors to properly fail their operation
                        payload.AbandonOperation = true;
                    }
                    return payload;
                }
                else
                {
                    break;
                }
            }
            return null;
        }

        public void DeleteQueue()
        {
            this.Queue.Delete();
        }
    }

    public class AzureMessageItem : IMessageItem
    {
        public AzureMessageQueue ContainingQueue { get; set; }
        public CloudQueueMessage Message { get; set; }

        public void Delete()
        {
            this.ContainingQueue.Queue.DeleteMessage(this.Message);
        }

        public void UpdateInvisibility(TimeSpan invisibilityTimeout)
        {
            this.ContainingQueue.Queue.UpdateMessage(this.Message, invisibilityTimeout, MessageUpdateFields.Visibility);
        }
    }
}
