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
    public abstract class MessagePayload
    {
        public MessageTypes MessageType { get; set; }
    }
    //Represents a queue message. Defined separately from the payload in case there is a queue object from the implementation
    //that needs to be accessed or manipulated
    public abstract class WorkerMessage
    {
        // Concrete definitions of this class should have a reference to the object so that it can be deleted.
        public MessagePayload Payload { get; set; }
    }
    //Represents the queue that keeps track of the messages
    public interface MessageQueue
    {
        void Enqueue(MessagePayload message);
        WorkerMessage Dequeue();
        void DeleteMessage(WorkerMessage message);
    }
    public class ReplicatePayload : MessagePayload
    {
        public string Source { get; set; }
        public string Destination { get; set; }
        public ReplicatePayload()
        {
            this.MessageType = MessageTypes.BeginReplicate;
        }
        public ReplicatePayload(string source, string destination)
        {
            this.MessageType = MessageTypes.BeginReplicate;
            this.Source = source;
            this.Destination = destination;
        }
    }
    public class ReplicateProgressPayload : MessagePayload
    {
        public string CopyID { get; set; }
        public ReplicateProgressPayload()
        {
            this.MessageType = MessageTypes.ReplicateProgress;
        }
        public ReplicateProgressPayload(string copyId)
        {
            this.MessageType = MessageTypes.ReplicateProgress;
            this.CopyID = copyId;
        }
    }
}
