//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Common.Platform
{
    public enum MessageTypes
    {
        Unknown = 0,
        BeginReplicate = 1,
        ReplicateProgress = 2
    }

    //Represents a payload of a queue message
    public class QueueMessage
    {
        public QueueMessage()
        {
            this.payload = null;
        }

        public QueueMessage(MessageTypes type, Dictionary<string, string> payload)
        {
            this.MessageType = type;
            this.payload = payload;
        }

        public MessageTypes MessageType { get; set; }

        public Dictionary<string, string> payload { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    //Represents the queue that keeps track of the messages
    public interface MessageQueue
    {
        // Add a message to the queue
        void Enqueue(QueueMessage message);
        // Get a message from the queue and set it as the currently active message
        QueueMessage Dequeue();
        // Delete the currently active message
        void DeleteCurrentMessage();
        // Delete the referenced queue
        void DeleteQueue();
    }
    public class ReplicatePayload
    {
        public const string Source = "source";
        public const string Destination = "destination";
    }
    public class ReplicateProgressPayload
    {
        public const string CopyID = "copyid";
    }

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

        public AzureMessageQueue()
            : this(DashConfiguration.ConfigurationSource.GetSetting<string>("workerqueue", "workerqueue"))
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
