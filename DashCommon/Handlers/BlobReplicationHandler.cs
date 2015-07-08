//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Utils;
using System.Text.RegularExpressions;

namespace Microsoft.Dash.Common.Handlers
{
    public class BlobReplicationHandler
    {
        static Regex _replicationPathExpression = null;

        static BlobReplicationHandler()
        {
            string pathPattern = DashConfiguration.ReplicationPathPattern;
            if (!String.IsNullOrWhiteSpace(pathPattern))
            {
                // Note that this evaluation is performance critical, so we compile the regex
                _replicationPathExpression = new Regex(pathPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
        }

        public static bool ShouldReplicateBlob(ILookup<string, string> headers, string container, string blob)
        {
            bool retval = false;
            if (DashConfiguration.IsBlobReplicationEnabled)
            {
                bool evaluated = false;
                string replicaMetadata = DashConfiguration.ReplicationMetadataName;
                if (headers != null && !String.IsNullOrWhiteSpace(replicaMetadata))
                {
                    if (headers.Contains(replicaMetadata))
                    {
                        retval = String.Equals(DashConfiguration.ReplicationMetadataValue, headers[replicaMetadata].First(), StringComparison.OrdinalIgnoreCase);
                        evaluated = true;
                    }
                }
                if (!evaluated)
                {
                    if (_replicationPathExpression != null)
                    {
                        retval = _replicationPathExpression.IsMatch(container + "/" + blob);
                    }
                }

            }
            return retval;
        }

        public static bool ShouldReplicateBlob(ILookup<string, string> headers, NamespaceBlob namespaceBlob)
        {
            return ShouldReplicateBlob(headers, namespaceBlob.Container, namespaceBlob.BlobName);
        }

        public static async Task EnqueueBlobReplicationAsync(string container, string blob, bool deleteReplica)
        {
            var namespaceBlob = await NamespaceHandler.FetchNamespaceBlobAsync(container, blob);
            await EnqueueBlobReplicationAsync(namespaceBlob, deleteReplica);
        }

        public static async Task EnqueueBlobReplicationAsync(NamespaceBlob namespaceBlob, bool deleteReplica, bool saveNamespaceEntry = true)
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
            var task = Task.Factory.StartNew(() =>
            {
                var queue = new AzureMessageQueue();
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
            return new QueueMessage(deleteReplica ? MessageTypes.DeleteReplica : MessageTypes.BeginReplicate, 
                new Dictionary<string, string> 
                {
                    { ReplicatePayload.Source, sourceAccount },
                    { ReplicatePayload.Destination, destinationAccount },
                    { ReplicatePayload.Container, container },
                    { ReplicatePayload.BlobName, blob },
                });
        }
    }
}
