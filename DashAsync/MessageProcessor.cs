//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Platform.Payloads;
using Microsoft.Dash.Common.Processors;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Async
{
    public class MessageProcessor
    {
        public static void ProcessMessageLoop(ref int msgProcessed, ref int msgErrors, int? invisibilityTimeout = null, string namespaceAccount = null, string queueName = null)
        {
            CloudStorageAccount newNamespace = null;
            if (!String.IsNullOrWhiteSpace(namespaceAccount))
            {
                if (CloudStorageAccount.TryParse(namespaceAccount, out newNamespace))
                {
                    DashConfiguration.NamespaceAccount = newNamespace;
                }
            }
            IMessageQueue queue = new AzureMessageQueue(newNamespace, queueName);
            while (true)
            {
                try
                {
                    QueueMessage payload = queue.Dequeue(invisibilityTimeout);
                    if (payload == null)
                    {
                        break;
                    }
                    // Right now, success/failure is indicated through a bool
                    // Do we want to surround this with a try/catch and use exceptions instead?
                    if (ProcessMessage(payload, invisibilityTimeout) || payload.AbandonOperation)
                    {
                        payload.Delete();
                        msgProcessed++;
                    }
                    else
                    {
                        // Leave it in the queue for retry after invisibility period expires
                        msgErrors++;
                    }
                }
                catch (Exception ex)
                {
                    DashTrace.TraceWarning("Unhandled exception processing async message. Message will be left in queue. Details: {0}", ex);
                    msgErrors++;
                }
            }
        }

        static bool ProcessMessage(QueueMessage message, int? invisibilityTimeout = null)
        {
            var messageProcessed = false;
            Guid contextId = message.CorrelationId.HasValue ? message.CorrelationId.Value : Guid.Empty;
            using (var ctx = new CorrelationContext(contextId))
            {
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

                    case MessageTypes.UpdateService:
                        messageProcessed = DoUpdateServiceJob(message, invisibilityTimeout);
                        break;

                    case MessageTypes.ServiceOperationUpdate:
                        messageProcessed = DoServiceOperationUpdate(message, invisibilityTimeout);
                        break;

                    case MessageTypes.Unknown:
                    default:
                        DashTrace.TraceWarning("Unable to process unknown message type from async queue [{0}]. Payload: {1}",
                            message.MessageType,
                            message.ToString());
                        // Let this message bounce around for a bit - there may be a different version running
                        // on another instance that knows about this message. It will be automatically discarded
                        // after exceeding the dequeue limit.
                        messageProcessed = false;
                        break;
                }
            }
            return messageProcessed;
        }

        static bool DoReplicateJob(QueueMessage message, int? invisibilityTimeout = null)
        {
            if (message.AbandonOperation)
            {
                // We don't have any specific failure functionality for this operation
                return true;
            }
            return BlobReplicator.BeginBlobReplication(
                message.Payload[ReplicatePayload.Source],
                message.Payload[ReplicatePayload.Destination],
                message.Payload[ReplicatePayload.Container],
                message.Payload[ReplicatePayload.BlobName], 
                invisibilityTimeout);
        }

        static bool DoReplicateProgressJob(QueueMessage message, int? invisibilityTimeout = null)
        {
            if (message.AbandonOperation)
            {
                // We don't have any specific failure functionality for this operation
                return true;
            }
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
            if (message.AbandonOperation)
            {
                // We don't have any specific failure functionality for this operation
                return true;
            }
            return BlobReplicator.DeleteReplica(
                message.Payload[DeleteReplicaPayload.Source],
                message.Payload[DeleteReplicaPayload.Container],
                message.Payload[DeleteReplicaPayload.BlobName],
                message.Payload[DeleteReplicaPayload.ETag]);
        }

        static bool DoUpdateServiceJob(QueueMessage message, int? invisibilityTimeout = null)
        {
            // Extend the timeout on this message (5 mins for account creation, 1 min for account import & 30 secs to upgrade service)
            message.UpdateInvisibility(invisibilityTimeout ?? 390);
            var payload = new UpdateServicePayload(message);
            string operationId = ServiceUpdater.ImportAccountsAndUpdateService(
                message.Payload[ServiceOperationPayload.SubscriptionId],
                message.Payload[ServiceOperationPayload.ServiceName],
                message.Payload[ServiceOperationPayload.OperationId],
                message.Payload[ServiceOperationPayload.RefreshToken],
                payload.CreateAccountRequestIds,
                payload.ImportAccounts,
                payload.Settings,
                message.AbandonOperation).Result;
            if (!String.IsNullOrWhiteSpace(operationId) && !message.AbandonOperation)
            {
                // Enqueue a follow-up message to check the progress of this operation
                EnqueueServiceOperationUpdate(message, invisibilityTimeout);
                return true;
            }
            return false;
        }

        static bool DoServiceOperationUpdate(QueueMessage message, int? invisibilityTimeout = null)
        {
            if (ServiceUpdater.UpdateOperationStatus(
                message.Payload[ServiceOperationPayload.SubscriptionId],
                message.Payload[ServiceOperationPayload.ServiceName],
                message.Payload[ServiceOperationPayload.OperationId],
                message.Payload[ServiceOperationPayload.RefreshToken],
                message.AbandonOperation).Result && !message.AbandonOperation)
            {
                // Re-enqueue another message to continue to check on the operation
                EnqueueServiceOperationUpdate(message, invisibilityTimeout);
            }
            return true;
        }

        static void EnqueueServiceOperationUpdate(QueueMessage message, int? invisibilityTimeout = null)
        {
            if (message.AbandonOperation)
            {
                return;
            }
            // Ensure that this message isn't visible immediately as we'll sit in a tight loop
            new AzureMessageQueue(message).Enqueue(new QueueMessage(MessageTypes.ServiceOperationUpdate,
                new Dictionary<string, string>
                {
                    { UpdateServicePayload.OperationId, message.Payload[UpdateServicePayload.OperationId] },
                    { UpdateServicePayload.SubscriptionId, message.Payload[ServiceOperationPayload.SubscriptionId] },
                    { UpdateServicePayload.ServiceName, message.Payload[ServiceOperationPayload.ServiceName] },
                    { UpdateServicePayload.RefreshToken, message.Payload[ServiceOperationPayload.RefreshToken] },
                },
                message.CorrelationId), invisibilityTimeout ?? 10);
        }
    }
}
