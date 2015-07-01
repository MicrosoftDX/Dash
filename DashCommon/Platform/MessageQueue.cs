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
    //Represents the queue that keeps track of the messages
    public interface MessageQueue
    {
        // Add a message to the queue
        void Enqueue(QueueMessage message);
        Task EnqueueAsync(QueueMessage message);
        // Get a message from the queue and set it as the currently active message
        QueueMessage Dequeue();
        // Delete the currently active message
        void DeleteCurrentMessage();
        // Delete the referenced queue
        void DeleteQueue();
    }
}
