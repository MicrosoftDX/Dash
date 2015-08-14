//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Platform
{
    //Represents the queue that keeps track of the messages
    public interface IMessageQueue
    {
        // Add a message to the queue
        void Enqueue(QueueMessage message, int? initialInvisibilityDelay = null);
        Task EnqueueAsync(QueueMessage message, int? initialInvisibilityDelay = null);
        // Get a message from the queue and set it as the currently active message
        QueueMessage Dequeue(int? invisibilityTimeout = null);
        // Delete the currently active message
        void DeleteCurrentMessage();
        // Update 
        // Delete the referenced queue
        void DeleteQueue();
    }
}
