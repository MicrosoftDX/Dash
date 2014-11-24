//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Server.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Dash.Server.Handlers
{
    public class StorageOperationsHandler
    {
        public static async Task<HandlerResult> HandlePrePileOperationAsync(IHttpRequestWrapper requestWrapper)
        {
            string containerName = requestWrapper.UriParts.Container;
            string blobName = requestWrapper.UriParts.BlobName;
            StorageOperationTypes requestOperation = StorageOperations.GetBlobOperation(requestWrapper);
            HandlerResult result = null;
            switch (requestOperation)
            {
                case StorageOperationTypes.GetBlob:
                case StorageOperationTypes.GetBlobProperties:
                case StorageOperationTypes.SetBlobProperties:
                case StorageOperationTypes.GetBlobMetadata:
                case StorageOperationTypes.SetBlobMetadata:
                case StorageOperationTypes.LeaseBlob:
                case StorageOperationTypes.SnapshotBlob:
                case StorageOperationTypes.GetBlockList:
                    result = await BlobHandler.BasicBlobAsync(containerName, blobName);
                    break;

                case StorageOperationTypes.PutBlob:
                case StorageOperationTypes.PutBlock:
                case StorageOperationTypes.PutBlockList:
                    result = await BlobHandler.PutBlobAsync(containerName, blobName);
                    break;

                default:
                    // All other operations flow through to the controller action
                    break;
            }
            return result;
        }
    }
}