//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    public class NamespaceHandler
    {
        public static async Task<NamespaceBlob> CreateNamespaceBlobAsync(string container, string blob)
        {
            // create an namespace blob with hardcoded metadata
            return await PerformNamespaceOperation(container, blob, async (namespaceBlob) =>
            {
                bool exists = await namespaceBlob.ExistsAsync();
                if (exists && (bool)!namespaceBlob.IsMarkedForDeletion && !String.IsNullOrWhiteSpace(namespaceBlob.BlobName))
                {
                    return namespaceBlob;
                }
                //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
                var dataAccount = GetDataStorageAccountForBlob(blob);
                namespaceBlob.AccountName = dataAccount.Credentials.AccountName;
                namespaceBlob.Container = container;
                namespaceBlob.BlobName = blob;
                namespaceBlob.IsMarkedForDeletion = false;
                await namespaceBlob.SaveAsync();

                return namespaceBlob;
            });
        }

        public static async Task<NamespaceBlob> FetchNamespaceBlobAsync(string container, string blobName, string snapshot = null)
        {
            return await NamespaceBlob.FetchAsync(container, blobName, snapshot);
        }

        public static async Task<T> PerformNamespaceOperation<T>(string container, string blobName, Func<NamespaceBlob, Task<T>> operation)
        {
            const int createRetryCount = 3;

            // Allow namespace operations to be retried. Update operations (via NamespaceBlob.SaveAsync()) use pre-conditions to
            // resolve race conditions on the same namespace blob
            for (int retry = 0; retry < createRetryCount; retry++)
            {
                var startTime = DateTime.Now;
                try
                {
                    var namespaceBlob = await FetchNamespaceBlobAsync(container, blobName);
                    return await operation(namespaceBlob);
                }
                catch (StorageException ex)
                {
                    if ((ex.RequestInformation.HttpStatusCode != (int) HttpStatusCode.PreconditionFailed &&
                         ex.RequestInformation.HttpStatusCode != (int) HttpStatusCode.Conflict) ||
                        retry >= createRetryCount - 1)
                    {
                        throw;
                    }
                }
                finally
                {
                    Debug.WriteLine("Elapsed Time (minutes)={0}, Container={1}, BlobName={2}", DateTime.Now.Subtract(startTime).TotalMinutes, container, blobName);
                }
            }
            // Never get here
            return default(T);
        }

        //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
        public static CloudStorageAccount GetDataStorageAccountForBlob(string blobName)
        {
            return DashConfiguration.DataAccounts[GetHashCodeBucket(blobName, DashConfiguration.DataAccounts.Count)];
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

    }
}