using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Utils
{
    public class AzureWorkerMessage : WorkerMessage
    {
        public CloudQueueMessage Message { get; set; }
        private CloudQueue Queue { get; set; }
    }
    public class AzureMessageQueue : MessageQueue
    {
        private CloudQueue Queue { get; set; }

        public AzureMessageQueue()
        {
            //TODO
        }
        public void Enqueue(MessagePayload payload)
        {
            //TODO
        }
        public WorkerMessage Dequeue()
        {
            //TODO
            return new AzureWorkerMessage();
        }

        public void DeleteMessage(WorkerMessage message)
        {
            AzureWorkerMessage wMessage = (AzureWorkerMessage)message;
            Queue.DeleteMessage(wMessage.Message);
        }
    }
}
