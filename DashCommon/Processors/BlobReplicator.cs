//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Processors
{
    public class BlobReplicator
    {
        public static bool BeginBlobReplication(string sourceAccount, string destAccount, string container, string blobName, int waitDelay = 10)
        {
            bool retval = false;
            try
            {
                // Process is:
                //  - start the copy
                //  - wait around for a little while to see if it finishes - if so, we're done - update the namespace
                //  - if the copy is still in progress, enqueue a ReplicateProgress message to revisit the progress & update the namespace
                var sourceClient = DashConfiguration.GetDataAccountByAccountName(sourceAccount).CreateCloudBlobClient();
                var sourceBlob = sourceClient.GetContainerReference(container).GetBlobReferenceFromServer(blobName);
                var destContainer = DashConfiguration.GetDataAccountByAccountName(destAccount).CreateCloudBlobClient().GetContainerReference(container);
                ICloudBlob destBlob = null;
                if (sourceBlob.BlobType == BlobType.PageBlob)
                {
                    destBlob = destContainer.GetPageBlobReference(blobName);
                }
                else
                {
                    destBlob = destContainer.GetBlockBlobReference(blobName);
                }
                DashTrace.TraceInformation("Replicating blob [{0}] to account [{1}]", sourceBlob.Uri, destAccount);
                var sasUri = new UriBuilder(sourceBlob.Uri);
                sasUri.Query = sourceBlob.GetSharedAccessSignature(new SharedAccessBlobPolicy
                    {
                        SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),
                        SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(60),
                        Permissions = SharedAccessBlobPermissions.Read,
                    }).TrimStart('?');
                string copyId = destBlob.StartCopyFromBlob(sasUri.Uri);
                DateTime waitForCompleteGiveUp = DateTime.UtcNow.AddSeconds(waitDelay);
                while (DateTime.UtcNow < waitForCompleteGiveUp)
                {
                    DashTrace.TraceInformation("Fetching attributes for [{0}]", destBlob.Uri);
                    destBlob.FetchAttributes();
                    if (destBlob.CopyState.CopyId != copyId || destBlob.CopyState.Status != CopyStatus.Pending)
                    {
                        break;
                    }
                    Thread.Sleep(1000);
                }
                retval = ProcessBlobCopyStatus(destBlob, sourceAccount, copyId, waitDelay);
            }
            catch (StorageException ex)
            {
                DashTrace.TraceWarning("Storage error initiating replication for blob [{0}][{1}] to account [{2}]. Details: {3}",
                    sourceAccount,
                    PathUtils.CombineContainerAndBlob(container, blobName),
                    destAccount,
                    ex);
                // TODO: Classify the errors into retryable & non-retryable
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error initiating replication for blob [{0}][{1}] to account [{2}]. Details: {3}",
                    sourceAccount,
                    PathUtils.CombineContainerAndBlob(container, blobName),
                    destAccount,
                    ex);
            }
            return retval;
        }

        public static bool ProgressBlobReplication(string sourceAccount, string destAccount, string container, string blobName, string copyId)
        {
            bool retval = false;
            try
            {
                var destContainer = DashConfiguration.GetDataAccountByAccountName(destAccount).CreateCloudBlobClient().GetContainerReference(container);
                var destBlob = destContainer.GetBlobReferenceFromServer(blobName);
                retval = ProcessBlobCopyStatus(destBlob, sourceAccount, copyId);
            }
            catch (StorageException ex)
            {
                DashTrace.TraceWarning("Storage error checking replication progress for blob [{0}][{1}] to account [{2}]. Details: {3}",
                    sourceAccount,
                    PathUtils.CombineContainerAndBlob(container, blobName),
                    destAccount,
                    ex);
                // TODO: Classify the errors into retryable & non-retryable
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error checking replication progress for blob [{0}][{1}] to account [{2}]. Details: {3}",
                    sourceAccount,
                    PathUtils.CombineContainerAndBlob(container, blobName),
                    destAccount,
                    ex);
            }
            return retval;
        }

        public static bool DeleteReplica(string accountName, string container, string blobName)
        {
            bool retval = false;
            try
            {
                var blobContainer = DashConfiguration.GetDataAccountByAccountName(accountName).CreateCloudBlobClient().GetContainerReference(container);
                var replicaBlob = blobContainer.GetBlobReferenceFromServer(blobName);
                if (FinalizeBlobReplication(accountName, container, blobName, true))
                {
                    replicaBlob.Delete(DeleteSnapshotsOption.IncludeSnapshots, AccessCondition.GenerateIfMatchCondition(replicaBlob.Properties.ETag));
                    retval = true;
                }
            }
            catch (StorageException ex)
            {
                DashTrace.TraceWarning("Storage error deleting replica blob [{0}] from account [{1}]. Details: {2}",
                    PathUtils.CombineContainerAndBlob(container, blobName),
                    accountName,
                    ex);
                // TODO: Classify the errors into retryable & non-retryable
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error deleting replica blob [{0}] from account [{1}]. Details: {2}",
                    PathUtils.CombineContainerAndBlob(container, blobName),
                    accountName,
                    ex);
            }
            return retval;
        }

        static bool ProcessBlobCopyStatus(ICloudBlob destBlob, string sourceAccount, string copyId, int waitDelay = 10)
        {
            bool retval = false;
            string destAccount = destBlob.ServiceClient.Credentials.AccountName;
            Uri sourceUri = destBlob.CopyState.Source;
            // If the blob has moved on to another copy, just assume that it overrode our copy
            if (destBlob.CopyState.CopyId == copyId)
            {
                switch (destBlob.CopyState.Status)
                {
                    case CopyStatus.Aborted:
                        // Copy has been abandoned - we don't automatically come back from here
                        DashTrace.TraceInformation("Replicating blob [{0}] to account [{1}]. Copy Id [{3}] has been aborted.",
                            sourceUri, destAccount, copyId);
                        retval = true;
                        break;

                    case CopyStatus.Failed:
                    case CopyStatus.Invalid:
                        // Possibly temporaral issues - allow the message to retry after a period
                        DashTrace.TraceInformation("Replicating blob [{0}] to account [{1}]. Copy Id [{3}] has been failed or is invalid.",
                            sourceUri, destAccount, copyId);
                        retval = false;
                        break;

                    case CopyStatus.Pending:
                        // Enqueue a new message to check on the copy status
                        DashTrace.TraceInformation("Replicating blob [{0}] to account [{1}]. Copy Id [{2}] is pending. Enqueing progress message.",
                            sourceUri, destBlob.ServiceClient.Credentials.AccountName, copyId);
                        new AzureMessageQueue().Enqueue(new QueueMessage(MessageTypes.ReplicateProgress,
                            new Dictionary<string, string> 
                                {
                                    { ReplicateProgressPayload.Source, sourceAccount },
                                    { ReplicateProgressPayload.Destination, destAccount },
                                    { ReplicateProgressPayload.Container, destBlob.Container.Name },
                                    { ReplicateProgressPayload.BlobName, destBlob.Name },
                                    { ReplicateProgressPayload.CopyID, copyId },
                                }), waitDelay);
                        retval = true;
                        break;

                    case CopyStatus.Success:
                        retval = FinalizeBlobReplication(destAccount, destBlob.Container.Name, destBlob.Name, false);
                        break;
                }
            }
            else
            {
                DashTrace.TraceInformation("Replication of blob [{0}] to account [{1}] has been aborted as the destination blob has a different copy id. Expected [{2}], actual [{3}]",
                    sourceUri, destAccount, copyId, destBlob.CopyState.CopyId);
                // Return true to indicate that this operation shouldn't be retried
                retval = true;
            }
            return retval;
        }

        static bool FinalizeBlobReplication(string dataAccount, string container, string blobName, bool deleteReplica)
        {
            return NamespaceHandler.PerformNamespaceOperation(container, blobName, async (namespaceBlob) =>
            {
                bool exists = await namespaceBlob.ExistsAsync();
                if (!exists || namespaceBlob.IsMarkedForDeletion)
                {
                    // It's ok for a deleted replica not to have a corresponding namespace blob
                    if (!deleteReplica)
                    {
                        DashTrace.TraceWarning("Replication of blob [{0}] to account [{1}] cannot be completed because the namespace blob either does not exist or is marked for deletion.",
                            PathUtils.CombineContainerAndBlob(container, blobName),
                            dataAccount);
                    }
                    // Do not attempt retry in this state
                    return true;
                }
                string message = "replicated to";
                bool nsDirty = false;
                if (deleteReplica)
                {
                    nsDirty = namespaceBlob.RemoveDataAccount(dataAccount);
                    message = "dereplicated from";
                }
                else
                {
                    nsDirty = namespaceBlob.AddDataAccount(dataAccount);
                }
                if (nsDirty)
                {
                    await namespaceBlob.SaveAsync();
                    DashTrace.TraceInformation("Blob [{0}] has been successfully {1} account [{2}].",
                        PathUtils.CombineContainerAndBlob(container, blobName),
                        message,
                        dataAccount);
                }
                return true;
            }).Result;
        }
    }
}
