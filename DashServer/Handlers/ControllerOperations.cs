//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Security.Cryptography;

namespace Microsoft.Dash.Server.Handlers
{
    public static class ControllerOperations
    {
        public static Uri GetForwardingUri(HttpRequestBase request, CloudStorageAccount account, string containerName, bool useSas = false)
        {
            CloudBlobContainer container = GetContainerByName(account, containerName);

            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = request.Url.Scheme,
                Host = account.BlobEndpoint.Host,
                Path = container.Uri.AbsolutePath,
                Query = request.Url.Query
            };

            return forwardUri.Uri;
        }

        public static Uri GetRedirectUri(HttpRequestBase request, CloudStorageAccount account, string containerName, string blobName)
        {
            var redirectUri = GetRedirectUriBuilder(request.HttpMethod, request.Url.Scheme, account, containerName, blobName, true);
            // creating redirection Uri
            if (!String.IsNullOrWhiteSpace(request.Url.Query))
            {
                var queryParams = HttpUtility.ParseQueryString(redirectUri.Query);
                queryParams.Add(HttpUtility.ParseQueryString(request.Url.Query));

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

        public static Uri ForwardUriToNamespace(HttpRequestBase request)
        {
            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = request.Url.Scheme,
                Host = DashConfiguration.NamespaceAccount.BlobEndpoint.Host,
                Query = request.Url.Query
            };
            return forwardUri.Uri;
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

        public static async Task<NamespaceBlob> CreateNamespaceBlobAsync(string container, string blob)
        {
            //create an namespace blob with hardcoded metadata
            var namespaceBlob = await FetchNamespaceBlobAsync(container, blob);
            bool exists = await namespaceBlob.ExistsAsync();
            if (exists && !namespaceBlob.IsMarkedForDeletion && !String.IsNullOrWhiteSpace(namespaceBlob.BlobName))
            {
                return namespaceBlob;
            }
            else if (!exists)
            {
                await namespaceBlob.CreateAsync();
            }
            //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
            var dataAccount = GetDataStorageAccountForBlob(blob);
            namespaceBlob.AccountName = dataAccount.Credentials.AccountName;
            namespaceBlob.Container = container;
            namespaceBlob.BlobName = blob;
            namespaceBlob.IsMarkedForDeletion = false;
            await namespaceBlob.SaveAsync();

            return namespaceBlob;
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

        public static async Task<NamespaceBlob> FetchNamespaceBlobAsync(string container, string blobName, string snapshot = null)
        {
            return await NamespaceBlob.FetchForBlobAsync(GetBlobByName(DashConfiguration.NamespaceAccount, container, blobName, snapshot));
        }

        public static CloudBlobContainer GetContainerByName(CloudStorageAccount account, string containerName)
        {
            CloudBlobClient client = account.CreateCloudBlobClient();
            return client.GetContainerReference(containerName);
        }

        public static CloudBlockBlob GetBlobByName(CloudStorageAccount account, string containerName, string blobName, string snapshot = null)
        {
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