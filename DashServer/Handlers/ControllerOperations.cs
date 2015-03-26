//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net.Http;
using System.Web;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
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

        public static Uri GetRedirectUri(HttpRequestBase request, CloudStorageAccount account, string containerName, string blobName)
        {
            return GetRedirectUri(request.Url, request.HttpMethod, account, containerName, blobName);
        }

        public static Uri GetRedirectUri(Uri originalUri, string method, CloudStorageAccount account, string containerName, string blobName)
        {
            var redirectUri = GetRedirectUriBuilder(method, originalUri.Scheme, account, containerName, blobName, true);
            // creating redirection Uri
            if (!String.IsNullOrWhiteSpace(originalUri.Query))
            {
                var queryParams = HttpUtility.ParseQueryString(redirectUri.Query);
                queryParams.Add(HttpUtility.ParseQueryString(originalUri.Query));

                redirectUri.Query = queryParams.ToString();
            }

            return redirectUri.Uri;
        }

        public static UriBuilder GetRedirectUriBuilder(string method, string scheme, CloudStorageAccount account, string containerName, string blobName, bool useSas)
        {
            CloudBlobContainer container = NamespaceHandler.GetContainerByName(account, containerName);
            return new UriBuilder
            {
                Scheme = scheme,
                Host = account.BlobEndpoint.Host,
                Path = containerName + "/" + blobName.TrimStart('/'),
                Query = useSas ? CalculateSASStringForContainer(method, container).TrimStart('?') : String.Empty,
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