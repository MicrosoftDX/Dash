//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Platform.Payloads;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Processors
{
    public class BlobReplicator
    {
        public static bool BeginBlobReplication(string sourceAccount, string destAccount, string container, string blobName, int? waitDelay = null)
        {
            bool retval = false;
            ICloudBlob destBlob = null;
            try
            {
                // Process is:
                //  - start the copy
                //  - wait around for a little while to see if it finishes - if so, we're done - update the namespace
                //  - if the copy is still in progress, enqueue a ReplicateProgress message to revisit the progress & update the namespace

                // Attempt to acquire a reference to the specified destination first, because if the source no longer exists we must cleanup this orphaned replica
                var destContainer = DashConfiguration.GetDataAccountByAccountName(destAccount).CreateCloudBlobClient().GetContainerReference(container);
                try
                {
                    destBlob = destContainer.GetBlobReferenceFromServer(blobName);
                }
                catch
                {
                    destBlob = null;
                }
                var sourceClient = DashConfiguration.GetDataAccountByAccountName(sourceAccount).CreateCloudBlobClient();
                var sourceBlob = sourceClient.GetContainerReference(container).GetBlobReferenceFromServer(blobName);
                if (destBlob == null)
                {
                    if (sourceBlob.BlobType == BlobType.PageBlob)
                    {
                        destBlob = destContainer.GetPageBlobReference(blobName);
                    }
                    else
                    {
                        destBlob = destContainer.GetBlockBlobReference(blobName);
                    }
                }
                DashTrace.TraceInformation("Replicating blob [{0}] to account [{1}]", sourceBlob.Uri, destAccount);
                // If the source is still being copied to (we kicked off the replication as the result of a copy operation), then just recycle
                // this message (don't return false as that may ultimately cause us to give up replicating).
                if (sourceBlob.CopyState != null && sourceBlob.CopyState.Status == CopyStatus.Pending)
                {
                    DashTrace.TraceInformation("Waiting for replication source [{0}] to complete copy.", sourceBlob.Uri);
                    new AzureMessageQueue().Enqueue(
                        BlobReplicationHandler.ConstructReplicationMessage(
                            false,
                            sourceAccount,
                            destAccount,
                            container,
                            blobName,
                            String.Empty));
                    return true;
                }
                var sasUri = new UriBuilder(sourceBlob.Uri);
                sasUri.Query = sourceBlob.GetSharedAccessSignature(new SharedAccessBlobPolicy
                    {
                        SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),
                        SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(60),
                        Permissions = SharedAccessBlobPermissions.Read,
                    }).TrimStart('?');
                string copyId = destBlob.StartCopyFromBlob(sasUri.Uri);
                DateTime waitForCompleteGiveUp = DateTime.UtcNow.AddSeconds(waitDelay ?? (DashConfiguration.AsyncWorkerTimeout / 2));
                while (DateTime.UtcNow < waitForCompleteGiveUp)
                {
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
                // Classify the errors into retryable & non-retryable
                retval = !RecoverReplicationError(ex, destBlob,
                    String.Format("Storage error initiating replication for blob [{0}][{1}] to account [{2}]. Details: {3}",
                        sourceAccount,
                        PathUtils.CombineContainerAndBlob(container, blobName),
                        destAccount,
                        ex));
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

        public static bool ProgressBlobReplication(string sourceAccount, string destAccount, string container, string blobName, string copyId, int? waitDelay = null)
        {
            bool retval = false;
            ICloudBlob destBlob = null;
            try
            {
                var destContainer = DashConfiguration.GetDataAccountByAccountName(destAccount).CreateCloudBlobClient().GetContainerReference(container);
                destBlob = destContainer.GetBlobReferenceFromServer(blobName);
                retval = ProcessBlobCopyStatus(destBlob, sourceAccount, copyId, waitDelay);
            }
            catch (StorageException ex)
            {
                // Classify the errors into retryable & non-retryable
                retval = !RecoverReplicationError(ex, destBlob,
                    String.Format("Storage error checking replication progress for blob [{0}][{1}] to account [{2}]. Details: {3}",
                        sourceAccount,
                        PathUtils.CombineContainerAndBlob(container, blobName),
                        destAccount,
                        ex));

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

        public static bool DeleteReplica(string accountName, string container, string blobName, string eTag)
        {
            bool retval = false;
            ICloudBlob replicaBlob = null;
            try
            {
                DashTrace.TraceInformation("Deleting replica blob [{0}] from account [{1}]", PathUtils.CombineContainerAndBlob(container, blobName), accountName);
                var blobContainer = DashConfiguration.GetDataAccountByAccountName(accountName).CreateCloudBlobClient().GetContainerReference(container);
                replicaBlob = blobContainer.GetBlobReferenceFromServer(blobName);
                AccessCondition accessCondition = null;
                if (!String.IsNullOrWhiteSpace(eTag))
                {
                    accessCondition = AccessCondition.GenerateIfMatchCondition(eTag);
                }
                replicaBlob.Delete(DeleteSnapshotsOption.IncludeSnapshots, accessCondition);
                FinalizeBlobReplication(accountName, container, blobName, true);
                retval = true;
            }
            catch (StorageException ex)
            {
                // Classify the errors into retryable & non-retryable
                retval = !RecoverReplicationError(ex, replicaBlob,
                    String.Format("Storage error deleting replica blob [{0}] from account [{1}]. Details: {2}",
                        PathUtils.CombineContainerAndBlob(container, blobName),
                        accountName,
                        ex));
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

        static bool ProcessBlobCopyStatus(ICloudBlob destBlob, string sourceAccount, string copyId, int? waitDelay = null)
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
                        DashTrace.TraceWarning("Replicating blob [{0}] to account [{1}]. Copy Id [{3}] has been aborted.",
                            sourceUri, destAccount, copyId);
                        // Make sure we don't orphan the replica
                        CleanupAbortedBlobReplication(destBlob);
                        retval = true;
                        break;

                    case CopyStatus.Failed:
                    case CopyStatus.Invalid:
                        // Possibly temporaral issues - allow the message to retry after a period
                        DashTrace.TraceWarning("Replicating blob [{0}] to account [{1}]. Copy Id [{3}] has been failed or is invalid.",
                            sourceUri, destAccount, copyId);
                        // Make sure we don't orphan the replica
                        CleanupAbortedBlobReplication(destBlob);
                        retval = false;
                        break;

                    case CopyStatus.Pending:
                        // Enqueue a new message to check on the copy status
                        DashTrace.TraceInformation("Replicating blob [{0}] to account [{1}]. Copy Id [{2}] is pending. Copied [{3}]/[{4}] bytes. Enqueing progress message.",
                            sourceUri, destBlob.ServiceClient.Credentials.AccountName, copyId, destBlob.CopyState.BytesCopied, destBlob.CopyState.TotalBytes);
                        new AzureMessageQueue().Enqueue(new QueueMessage(MessageTypes.ReplicateProgress,
                                new Dictionary<string, string> 
                                    {
                                        { ReplicateProgressPayload.Source, sourceAccount },
                                        { ReplicateProgressPayload.Destination, destAccount },
                                        { ReplicateProgressPayload.Container, destBlob.Container.Name },
                                        { ReplicateProgressPayload.BlobName, destBlob.Name },
                                        { ReplicateProgressPayload.CopyID, copyId },
                                    },
                                DashTrace.CorrelationId),
                            waitDelay ?? (DashConfiguration.WorkerQueueInitialDelay + 10));
                        retval = true;
                        break;

                    case CopyStatus.Success:
                        retval = FinalizeBlobReplication(destAccount, destBlob.Container.Name, destBlob.Name, false, destBlob);
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

        static bool FinalizeBlobReplication(string dataAccount, string container, string blobName, bool deleteReplica, ICloudBlob destBlob = null)
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
                        // Attempt to not leave the replica orphaned
                        if (CleanupAbortedBlobReplication(namespaceBlob, destBlob))
                        {
                            await namespaceBlob.SaveAsync();
                        }
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

        static bool RecoverReplicationError(StorageException ex, ICloudBlob destBlob, string exceptionMessage)
        {
            bool retval = true;
            switch ((HttpStatusCode)ex.RequestInformation.HttpStatusCode)
            {
                case HttpStatusCode.Conflict:
                case HttpStatusCode.PreconditionFailed:
                    // Destination has been modified - no retry
                    if (destBlob != null)
                    {
                        DashTrace.TraceInformation("A pre-condition was not met attempting to replicate or cleanup blob [{0}] in account [{1}]. This operation will be aborted",
                            destBlob.Name,
                            destBlob.ServiceClient.Credentials.AccountName);
                    }
                    else
                    {
                        DashTrace.TraceWarning("A pre-condition was not met attempting to replicate or cleanup an unknown blob. This operation will be aborted.");
                    }
                    retval = false;
                    break;

                case HttpStatusCode.NotFound:
                    // The source blob could not be found - delete the target to prevent orphaning
                    if (destBlob != null)
                    {
                        DashTrace.TraceInformation("Replication of blob [{0}] to account [{1}] cannot be completed because the source blob does not exist.",
                            destBlob.Name,
                            destBlob.ServiceClient.Credentials.AccountName);
                        CleanupAbortedBlobReplication(destBlob);
                    }
                    else
                    {
                        DashTrace.TraceWarning("Replication of unknown blob cannot be completed because the source blob does not exist.");
                    }
                    retval = false;
                    break;

                default:
                    // Unexpected exceptions are warnings
                    DashTrace.TraceWarning(exceptionMessage);
                    break;
            }
            return retval;
        }

        static void CleanupAbortedBlobReplication(ICloudBlob destBlob)
        {
            if (destBlob == null)
            {
                return;
            }
            NamespaceHandler.PerformNamespaceOperation(destBlob.Container.Name, destBlob.Name, async (namespaceBlob) =>
            {
                if (CleanupAbortedBlobReplication(namespaceBlob, destBlob))
                {
                    await namespaceBlob.SaveAsync();
                    return true;
                }
                return false;
            }).Wait();
        }

        static bool CleanupAbortedBlobReplication(NamespaceBlob namespaceBlob, ICloudBlob destBlob)
        {
            try
            {
                destBlob.DeleteIfExists();
            }
            catch (Exception ex1)
            {
                DashTrace.TraceWarning("Error deleting aborted replication target [{0}][{1}]. Details: {2}",
                    destBlob.ServiceClient.Credentials.AccountName,
                    destBlob.Name,
                    ex1);
            }
            return namespaceBlob.RemoveDataAccount(destBlob.ServiceClient.Credentials.AccountName);
        }
    }
}
