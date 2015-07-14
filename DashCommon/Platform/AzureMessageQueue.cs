//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Microsoft.Dash.Common.Platform
{
    public class AzureMessageQueue : IMessageQueue
    {
        private static readonly int _timeout    = DashConfiguration.WorkerQueueTimeout;
        private static readonly int _dequeLimit = DashConfiguration.WorkerQueueDequeueLimit;

        private CloudQueue Queue { get; set; }
        private CloudQueueMessage CurrentMessage { get; set; }

        public AzureMessageQueue(string queueName)
        {
            this.Queue = NamespaceHandler.GetQueueByName(DashConfiguration.NamespaceAccount, queueName);
            this.Queue.CreateIfNotExists();
            this.CurrentMessage = null;
        }

        public AzureMessageQueue()
            : this(DashConfiguration.WorkerQueueName)
        {
        }

        public void Enqueue(QueueMessage payload, int? initialInvisibilityDelay = null)
        {
            EnqueueAsync(payload, initialInvisibilityDelay).Wait();
        }

        public async Task EnqueueAsync(QueueMessage payload, int? initialInvisibilityDelay = null)
        {
            CloudQueueMessage message = new CloudQueueMessage(payload.ToJson());
            TimeSpan? invisibilityDelay = null;
            if (initialInvisibilityDelay.HasValue)
            {
                invisibilityDelay = TimeSpan.FromSeconds(initialInvisibilityDelay.Value);
            }
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
                this.CurrentMessage = Queue.GetMessage(new TimeSpan(0, 0, invisibilityTimeout.Value));
                if (this.CurrentMessage != null)
                {
                    if (this.CurrentMessage.DequeueCount >= _dequeLimit)
                    {
                        DashTrace.TraceWarning("Discarding message after exceeding deque limit of {0}. Message details: {1}",
                            this.CurrentMessage.DequeueCount,
                            this.CurrentMessage.AsString);
                        DeleteCurrentMessage();
                    }
                    else
                    {
                        return JsonConvert.DeserializeObject<QueueMessage>(this.CurrentMessage.AsString);
                    }
                }
                else
                {
                    break;
                }
            }
            return null;
        }

        public void DeleteCurrentMessage()
        {
            if (this.CurrentMessage != null)
            {
                Queue.DeleteMessage(this.CurrentMessage);
                this.CurrentMessage = null;
            }
        }

        public void DeleteQueue()
        {
            this.Queue.Delete();
        }
    }
}
