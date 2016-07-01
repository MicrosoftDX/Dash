//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Authorization
{
    public static class Anonymous
    {
        public static async Task<bool> IsAuthorizedAsync(IHttpRequestWrapper request)
        {
            bool retval = false;
            var requestUriParts = request.UriParts;
            var requestOperation = StorageOperations.GetBlobOperation(request.HttpMethod, requestUriParts, request.QueryParameters, request.Headers);
            switch (requestOperation)
            {
                case StorageOperationTypes.GetContainerProperties:
                case StorageOperationTypes.GetContainerMetadata:
                case StorageOperationTypes.ListBlobs:
                    retval = await GetContainerPublicAccessAsync(requestUriParts.Container) == BlobContainerPublicAccessType.Container;
                    break;

                case StorageOperationTypes.GetBlob:
                case StorageOperationTypes.GetBlobProperties:
                case StorageOperationTypes.GetBlobMetadata:
                case StorageOperationTypes.GetBlockList:
                    retval = await GetContainerPublicAccessAsync(requestUriParts.Container) != BlobContainerPublicAccessType.Off;
                    break;
            }
            return retval;
        }

        static async Task<BlobContainerPublicAccessType> GetContainerPublicAccessAsync(string container)
        {
            // TODO: Plug this potential DoS vector - spurious anonymous requests could drown us here...
            var containerObject = NamespaceHandler.GetContainerByName(DashConfiguration.NamespaceAccount, container);
            var permissions = await containerObject.GetPermissionsAsync();
            return permissions.PublicAccess;
        }
    }
}