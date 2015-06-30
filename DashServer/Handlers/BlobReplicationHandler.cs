//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Handlers
{
    public class BlobReplicationHandler
    {
        static readonly HashSet<StorageOperationTypes> _replicationTriggerOperations = new HashSet<StorageOperationTypes>()
        {
            StorageOperationTypes.PutBlob,
            StorageOperationTypes.PutBlockList,
            StorageOperationTypes.PutPage,
            StorageOperationTypes.SetBlobMetadata,
            StorageOperationTypes.SetBlobProperties,
            StorageOperationTypes.DeleteBlob,
        };

        public static bool DoesOperationTriggerReplication(StorageOperationTypes operation)
        {
            return _replicationTriggerOperations.Contains(operation);
        }

        public static bool ShouldReplicateBlob(RequestHeaders headers, string container, string blob)
        {
            bool retval = false;
            if (DashConfiguration.IsBlobReplicationEnabled)
            {
                bool evaluated = false;
                string replicaMetadata = DashConfiguration.ReplicationMetadataName;
                if (!String.IsNullOrWhiteSpace(replicaMetadata))
                {
                    if (headers.Contains(replicaMetadata))
                    {
                        retval = String.Equals(DashConfiguration.ReplicationMetadataValue, headers.Value(replicaMetadata, ""), StringComparison.OrdinalIgnoreCase);
                        evaluated = true;
                    }
                }
                if (!evaluated)
                {
                    string pathPattern = DashConfiguration.ReplicationPathPattern;
                    if (!String.IsNullOrWhiteSpace(pathPattern))
                    {
                        // TODO: Determine pattern matching mechanism for path
                    }
                }

            }
            return retval;
        }

        public static bool ShouldReplicateBlob(RequestHeaders headers, NamespaceBlob namespaceBlob)
        {
            return ShouldReplicateBlob(headers, namespaceBlob.Container, namespaceBlob.BlobName);
        }

        public static async Task EnqueueBlobReplication(string container, string blob, bool deleteReplica)
        {
            var namespaceBlob = await NamespaceHandler.FetchNamespaceBlobAsync(container, blob);
            await EnqueueBlobReplication(namespaceBlob, deleteReplica);
        }

        public static async Task EnqueueBlobReplication(NamespaceBlob namespaceBlob, bool deleteReplica, bool saveNamespaceEntry = true)
        {
            if (!await namespaceBlob.ExistsAsync())
            {
                return;
            }
            // Trim down the namespace replication list to the first 'master' item. This is sufficient to ensure that the
            // orphaned blobs are not effectively in the account. The master blob will be replicated over the top of the
            // orphaned blobs.
            string primaryAccount = namespaceBlob.PrimaryAccountName;
            if (namespaceBlob.IsReplicated)
            {
                namespaceBlob.PrimaryAccountName = primaryAccount;
                if (saveNamespaceEntry)
                {
                    await namespaceBlob.SaveAsync();
                }
            }
            // This rest of this method does not block. Enqueueing the replication is a completely async process
            var queue = new AzureMessageQueue();
            var task = Task.Factory.StartNew(() =>
            {
                DashConfiguration.DataAccounts
                    .Where(dataAccount => !dataAccount.Credentials.AccountName.Equals(primaryAccount, StringComparison.OrdinalIgnoreCase))
                    .ToList()
                    .ForEach(async dataAccount => await queue.EnqueueAsync(ConstructReplicationMessage(deleteReplica, 
                                primaryAccount, 
                                dataAccount.Credentials.AccountName, 
                                namespaceBlob.Container,
                                namespaceBlob.BlobName)));

            });
        }

        static QueueMessage ConstructReplicationMessage(bool deleteReplica, string sourceAccount, string destinationAccount, string container, string blob)
        {
            if (deleteReplica)
            {
                return new QueueMessage(MessageTypes.DeleteReplica, new Dictionary<string, string>
                    {
                        { ReplicatePayload.Source, destinationAccount },
                        { ReplicatePayload.Container, container },
                        { ReplicatePayload.Blob, blob },
                    });
            }
            else
            {
                return new QueueMessage(MessageTypes.BeginReplicate, new Dictionary<string, string>
                    {
                        { ReplicatePayload.Source, sourceAccount },
                        { ReplicatePayload.Destination, destinationAccount },
                        { ReplicatePayload.Container, container },
                        { ReplicatePayload.Blob, blob },
                    });
            }
        }
    }
}