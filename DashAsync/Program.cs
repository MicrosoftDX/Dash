using Microsoft.Dash.Common.Utils;
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
            WorkerMessage message = queue.Dequeue();
            if (message != null)
            {
                MessagePayload payload = message.Payload;
                if (ProcessMessage(payload))
                {
                    queue.DeleteMessage(message);
                } //Else leave it in the queue
            }
        }

        static bool ProcessMessage(MessagePayload payload)
        {
            //TODO: Implement body of function
            switch (payload.MessageType)
            {
                case MessageTypes.BeginReplicate:
                    break;
                case MessageTypes.ReplicateProgress:
                    break;
                case MessageTypes.Unknown:
                    break;
            }
            return true;
        }
    }
}
