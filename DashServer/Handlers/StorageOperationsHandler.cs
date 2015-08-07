//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Handlers
{
    public class StorageOperationsHandler
    {
        public static async Task<HandlerResult> HandlePrePipelineOperationAsync(IHttpRequestWrapper requestWrapper)
        {
            string containerName = requestWrapper.UriParts.Container;
            string blobName = requestWrapper.UriParts.BlobName;
            StorageOperationTypes requestOperation = StorageOperations.GetBlobOperation(requestWrapper);
            DashClientCapabilities client = DashClientDetector.DetectClient(requestWrapper);
            HandlerResult result = null;
            bool servePrimaryOnly = false;
            switch (requestOperation)
            {
                case StorageOperationTypes.GetBlobProperties:
                case StorageOperationTypes.SetBlobProperties:
                case StorageOperationTypes.GetBlobMetadata:
                case StorageOperationTypes.SetBlobMetadata:
                case StorageOperationTypes.LeaseBlob:
                case StorageOperationTypes.GetBlockList:
                    servePrimaryOnly = true;
                    // Fall through
                    goto case StorageOperationTypes.GetBlob;
                case StorageOperationTypes.GetBlob:
                case StorageOperationTypes.SnapshotBlob:
                case StorageOperationTypes.GetPageRanges:
                    if (client.HasFlag(DashClientCapabilities.FollowRedirects))
                    {
                        // If the client has specified a concurrent operation, then we always serve the primary account
                        // as that is where the concurrency controls exist
                        if (!servePrimaryOnly && requestWrapper.Headers.IsConcurrentRequest())
                        {
                            servePrimaryOnly = true;
                        }
                        result = await BlobHandler.BasicBlobAsync(requestWrapper, 
                            containerName, 
                            blobName, 
                            servePrimaryOnly,
                            BlobReplicationOperations.DoesOperationTriggerReplication(requestOperation));
                    }
                    break;

                case StorageOperationTypes.PutPage:
                    if (client.HasFlag(DashClientCapabilities.NoPayloadToDash))
                    {
                        result = await BlobHandler.BasicBlobAsync(requestWrapper, 
                            containerName, 
                            blobName,
                            true,
                            BlobReplicationOperations.DoesOperationTriggerReplication(requestOperation));
                    }
                    break;

                case StorageOperationTypes.PutBlob:
                case StorageOperationTypes.PutBlock:
                case StorageOperationTypes.PutBlockList:
                    if (client.HasFlag(DashClientCapabilities.NoPayloadToDash))
                    {
                        result = await BlobHandler.PutBlobAsync(requestWrapper, 
                            containerName, 
                            blobName,
                            BlobReplicationOperations.DoesOperationTriggerReplication(requestOperation));
                    }
                    break;

                default:
                    // All other operations flow through to the controller action
                    break;
            }
            DashTrace.TraceInformation("Operation: {0}, client capability: {1}, action: {2}",
                requestOperation,
                client,
                result == null ? "Forward" : "Redirect");
            return result;
        }
    }
}