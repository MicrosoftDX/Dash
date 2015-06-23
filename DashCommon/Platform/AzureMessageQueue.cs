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
        private int Timeout;
        private CloudQueue Queue { get; set; }
        private CloudQueueMessage CurrentMessage { get; set; }

        public AzureMessageQueue(string queueName)
        {
            this.Queue = NamespaceHandler.GetQueueByName(DashConfiguration.NamespaceAccount, queueName);
            this.Queue.CreateIfNotExists();
            this.CurrentMessage = null;
            this.Timeout = DashConfiguration.WorkerQueueTimeout;
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
        public QueueMessage Dequeue()
        {
            this.CurrentMessage = Queue.GetMessage(new TimeSpan(0, 0, this.Timeout));
            return JsonConvert.DeserializeObject<QueueMessage>(this.CurrentMessage.AsString);
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
