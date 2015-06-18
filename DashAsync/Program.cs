using Microsoft.Dash.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DashAsync
{
    class Program
    {
        static void Main(string[] args)
        {
            MessageQueue queue = new AzureMessageQueue();
            QueueMessage payload = queue.Dequeue();
            if (payload != null)
            {
                // Right now, success/failure is indicated through a bool
                // Do we want to surround this with a try/catch and use exceptions instead?
                if (ProcessMessage(payload))
                {
                    queue.DeleteCurrentMessage();
                } //Else leave it in the queue
            }
        }

        static bool ProcessMessage(QueueMessage message)
        {
            var success = false;
            //TODO: Implement body of function
            switch (message.MessageType)
            {
                case MessageTypes.BeginReplicate:
                    success = DoReplicateJob(JsonConvert.DeserializeObject<ReplicatePayload>(message.payload));
                    break;
                case MessageTypes.ReplicateProgress:
                    success = DoReplicateProgressJob(JsonConvert.DeserializeObject<ReplicateProgressPayload>(message.payload));
                    break;
                case MessageTypes.Unknown:
                    break;
            }
            return success;
        }

        static bool DoReplicateJob(ReplicatePayload payload)
        {
            //TODO
            return true;
        }

        static bool DoReplicateProgressJob(ReplicateProgressPayload payload)
        {
            //TODO
            return true;
        }
    }
}
