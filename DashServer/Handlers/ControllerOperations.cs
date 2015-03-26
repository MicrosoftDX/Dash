//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
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
            CloudBlobContainer container = GetContainerByName(account, containerName);
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

        //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
        public static CloudStorageAccount GetDataStorageAccountForBlob(string blobName)
        {
            return DashConfiguration.DataAccounts[GetHashCodeBucket(blobName, DashConfiguration.DataAccounts.Count)];
        }

        static int GetHashCodeBucket(string stringToHash, int numBuckets)
        {
            System.Diagnostics.Debug.Assert(!String.IsNullOrWhiteSpace(stringToHash));

            var hash = new SHA256CryptoServiceProvider();
            byte[] hashText = hash.ComputeHash(Encoding.UTF8.GetBytes(stringToHash));
            long hashCodeStart = BitConverter.ToInt64(hashText, 0);
            long hashCodeMedium = BitConverter.ToInt64(hashText, 8);
            long hashCodeEnd = BitConverter.ToInt64(hashText, 24);
            long hashCode = hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;

            return (int)(Math.Abs(hashCode) % numBuckets);
        }

        //calculates SAS string to have access to a container
        public static string CalculateSASStringForContainer(string method, CloudBlobContainer container)
        {
            SharedAccessBlobPolicy sasConstraints = GetSasPolicy(method);
            //Generate the shared access signature on the container, setting the constraints directly on the signature.
            return container.GetSharedAccessSignature(sasConstraints);
        }

        public static CloudBlobContainer GetContainerByName(CloudStorageAccount account, string containerName)
        {
            CloudBlobClient client = account.CreateCloudBlobClient();
            return client.GetContainerReference(containerName);
        }

        public static ICloudBlob GetBlobByName(CloudStorageAccount account, string containerName, string blobName, string snapshot = null)
        {
            // ** WARNING ** We don't want to make a trip to storage for this, but we also don't know what kind of blob we're being asked for.
            // The returned object is actually a CloudBlockBlob, so don't try to do any page blob operations, otherwise it will throw an exception.
            CloudBlobContainer container = GetContainerByName(account, containerName);
            DateTimeOffset snapshotDateTime;
            if (DateTimeOffset.TryParse(snapshot, out snapshotDateTime))
            {
                return container.GetBlockBlobReference(blobName, snapshotDateTime);
            }
            return container.GetBlockBlobReference(blobName);
        }

    }
}