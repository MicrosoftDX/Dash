//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Platform
{
    public class AzureMessageQueue : MessageQueue
    {
        private static readonly int Timeout = DashConfiguration.WorkerQueueTimeout;
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

        public void Enqueue(QueueMessage payload)
        {
            CloudQueueMessage message = new CloudQueueMessage(payload.ToJson());
            this.Queue.AddMessage(message);
        }
        public async Task EnqueueAsync(QueueMessage payload)
        {
            CloudQueueMessage message = new CloudQueueMessage(payload.ToJson());
            await this.Queue.AddMessageAsync(message);
        }
        public QueueMessage Dequeue(int? invisibilityTimeout = null)
        {
            this.CurrentMessage = Queue.GetMessage(new TimeSpan(0, 0, invisibilityTimeout ?? Timeout));
            if (this.CurrentMessage != null)
            {
                return JsonConvert.DeserializeObject<QueueMessage>(this.CurrentMessage.AsString);
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
