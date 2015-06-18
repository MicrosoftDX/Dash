//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Common.Handlers;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Utils
{   
    public class AzureMessageQueue : MessageQueue
    {
        private CloudQueue Queue { get; set; }
        private CloudQueueMessage CurrentMessage { get; set; }

        public AzureMessageQueue(string queueName)
        {
            this.Queue = NamespaceHandler.GetQueueByName(DashConfiguration.NamespaceAccount, queueName);
            this.Queue.CreateIfNotExists();
            this.CurrentMessage = null;
        }

        public AzureMessageQueue(): this(DashConfiguration.ConfigurationSource.GetSetting<string>("workerqueue", "workerqueue"))
        {
        }

        public void Enqueue(QueueMessage payload)
        {
            CloudQueueMessage message = new CloudQueueMessage(payload.ToJson());
            this.Queue.AddMessage(message);
        }
        public QueueMessage Dequeue()
        {
            this.CurrentMessage = Queue.GetMessage();
            return JsonConvert.DeserializeObject<QueueMessage>(this.CurrentMessage.AsString);
        }

        public void DeleteCurrentMessage()
        {
            Queue.DeleteMessage(this.CurrentMessage);
        }

        public void DeleteQueue()
        {
            this.Queue.Delete();
        }
    }
}
