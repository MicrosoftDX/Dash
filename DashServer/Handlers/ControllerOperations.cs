//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Web;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Authorization;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Handlers
{
    public static class ControllerOperations
    {
        public static Uri ForwardUriToNamespace(HttpRequestBase request)
        {
            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = request.Url.Scheme,
                Host = DashConfiguration.NamespaceAccount.BlobEndpoint.Host,
                Path = request.Path,
                Query = HttpUtility.ParseQueryString(request.Url.Query).ToString()
            };
            return forwardUri.Uri;
        }

        public static Uri GetRedirectUri(HttpRequestBase request, 
            CloudStorageAccount account, 
            string containerName, 
            string blobName,
            bool decodeQueryParams = true)
        {
            return GetRedirectUri(request.Url, request.HttpMethod, account, containerName, blobName, decodeQueryParams);
        }

        public static Uri GetRedirectUri(Uri originalUri, 
            string method, 
            CloudStorageAccount account, 
            string containerName, 
            string blobName,
            bool decodeQueryParams = true)
        {
            var redirectUri = GetRedirectUriBuilder(method, originalUri.Scheme, account, containerName, blobName, true, originalUri.Query, decodeQueryParams);
            return redirectUri.Uri;
        }

        public static UriBuilder GetRedirectUriBuilder(string method, 
            string scheme, 
            CloudStorageAccount account, 
            string containerName, 
            string blobName, 
            bool useSas, 
            string queryString, 
            bool decodeQueryParams = true)
        {
            CloudBlobContainer container = NamespaceHandler.GetContainerByName(account, containerName);
            // Strip any existing SAS query params as we'll replace them with our own SAS calculation
            var queryParams = RequestQueryParameters.Create(queryString, decodeQueryParams);
            SharedAccessSignature.RemoveSasQueryParameters(queryParams);
            if (useSas)
            {
                // Be careful to preserve the URL encoding in the signature
                queryParams.Append(CalculateSASStringForContainer(method, container), false);
            }
            return new UriBuilder
            {
                Scheme = scheme,
                Host = account.BlobEndpoint.Host,
                Path = PathUtils.CombineContainerAndBlob(containerName, PathUtils.PathEncode(blobName)),
                Query = queryParams.ToString(),
            };
        }

        //calculates Shared Access Signature (SAS) string based on type of request (GET, HEAD, DELETE, PUT)
        static SharedAccessBlobPolicy GetSasPolicy(HttpRequestBase request)
        {
            return GetSasPolicy(request.HttpMethod);
        }

        static SharedAccessBlobPolicy GetSasPolicy(string method)
        {
            return GetSasPolicy(new HttpMethod(method));
        }

        static SharedAccessBlobPolicy GetSasPolicy(HttpMethod httpMethod)
        {
            //Default to read only
            SharedAccessBlobPermissions permission = SharedAccessBlobPermissions.Read;
            if (httpMethod == HttpMethod.Delete)
            {
                permission = SharedAccessBlobPermissions.Delete;
            }
            else if (httpMethod == HttpMethod.Put)
            {
                permission = SharedAccessBlobPermissions.Write;
            }

            return new SharedAccessBlobPolicy()
            {
                Permissions = permission,
                SharedAccessStartTime = DateTime.Now.AddMinutes(-5),
                SharedAccessExpiryTime = DateTime.Now.AddMinutes(54)
            };
        }

        //calculates SAS string to have access to a container
        public static string CalculateSASStringForContainer(string method, CloudBlobContainer container)
        {
            SharedAccessBlobPolicy sasConstraints = GetSasPolicy(method);
            //Generate the shared access signature on the container, setting the constraints directly on the signature.
            return container.GetSharedAccessSignature(sasConstraints);
        }
    }
}