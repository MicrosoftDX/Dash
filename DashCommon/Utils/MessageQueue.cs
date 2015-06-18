//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Utils
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

        public QueueMessage(MessageTypes type, MessagePayload payload)
        {
            this.MessageType = type;
            this.payload = payload.ToJson();
        }

        public MessageTypes MessageType { get; set; }

        public string payload { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public abstract class MessagePayload {

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
        //Simple empty class to represent payloads
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
    public class ReplicatePayload : MessagePayload
    {
        public string Source { get; set; }
        public string Destination { get; set; }
        public ReplicatePayload(string source, string destination)
        {
            this.Source = source;
            this.Destination = destination;
        }
    }
    public class ReplicateProgressPayload : MessagePayload
    {
        public string CopyID { get; set; }
        public ReplicateProgressPayload(string copyId)
        {
            this.CopyID = copyId;
        }
    }
}
