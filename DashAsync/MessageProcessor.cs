//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Processors;

namespace Microsoft.Dash.Async
{
    public class MessageProcessor
    {
        public static void ProcessMessageLoop(ref int msgProcessed, ref int msgErrors, int? invisibilityTimeout = null)
        {
            IMessageQueue queue = new AzureMessageQueue();
            while (true)
            {
                QueueMessage payload = queue.Dequeue(invisibilityTimeout);
                if (payload == null)
                {
                    break;
                }
                // Right now, success/failure is indicated through a bool
                // Do we want to surround this with a try/catch and use exceptions instead?
                if (ProcessMessage(payload, invisibilityTimeout))
                {
                    queue.DeleteCurrentMessage();
                    msgProcessed++;
                }
                else
                {
                    // Leave it in the queue for retry after invisibility period expires
                    msgErrors++;
                }
            }
        }

        static bool ProcessMessage(QueueMessage message, int? invisibilityTimeout = null)
        {
            var messageProcessed = false;
            switch (message.MessageType)
            {
                case MessageTypes.BeginReplicate:
                    messageProcessed = DoReplicateJob(message, invisibilityTimeout);
                    break;

                case MessageTypes.ReplicateProgress:
                    messageProcessed = DoReplicateProgressJob(message, invisibilityTimeout);
                    break;

                case MessageTypes.DeleteReplica:
                    messageProcessed = DoDeleteReplicaJob(message, invisibilityTimeout);
                    break;

                case MessageTypes.Unknown:
                default:
                    DashTrace.TraceWarning("Unable to process unknown message type from async queue [{0}]. Payload: {1}",
                        message.MessageType,
                        message.ToString());
                    // Let this message bounce around for a bit - there may be a different version running
                    // on another instance that knows about this message. It will be automatically discarded
                    // after exceeding the deque limit.
                    messageProcessed = false;
                    break;
            }
            return messageProcessed;
        }

        static bool DoReplicateJob(QueueMessage message, int? invisibilityTimeout = null)
        {
            return BlobReplicator.BeginBlobReplication(
                message.Payload[ReplicatePayload.Source],
                message.Payload[ReplicatePayload.Destination],
                message.Payload[ReplicatePayload.Container],
                message.Payload[ReplicatePayload.BlobName], 
                invisibilityTimeout);
        }

        static bool DoReplicateProgressJob(QueueMessage message, int? invisibilityTimeout = null)
        {
            return BlobReplicator.ProgressBlobReplication(
                message.Payload[ReplicateProgressPayload.Source],
                message.Payload[ReplicateProgressPayload.Destination],
                message.Payload[ReplicateProgressPayload.Container],
                message.Payload[ReplicateProgressPayload.BlobName],
                message.Payload[ReplicateProgressPayload.CopyID],
                invisibilityTimeout);
        }

        static bool DoDeleteReplicaJob(QueueMessage message, int? invisibilityTimeout = null)
        {
            return BlobReplicator.DeleteReplica(
                message.Payload[ReplicateProgressPayload.Source],
                message.Payload[ReplicateProgressPayload.Container],
                message.Payload[ReplicateProgressPayload.BlobName]);
        }
    }
}
