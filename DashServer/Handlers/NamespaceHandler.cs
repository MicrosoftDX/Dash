//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Handlers
{
    public class NamespaceHandler
    {
        public static async Task<NamespaceBlob> CreateNamespaceBlobAsync(string container, string blob)
        {
            // create an namespace blob with hardcoded metadata
            return await PerformNamespaceOperation(container, blob, async (namespaceBlob) =>
            {
                bool exists = await namespaceBlob.ExistsAsync();
                if (exists && !namespaceBlob.IsMarkedForDeletion && !String.IsNullOrWhiteSpace(namespaceBlob.BlobName))
                {
                    return namespaceBlob;
                }
                //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
                var dataAccount = ControllerOperations.GetDataStorageAccountForBlob(blob);
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
            return await NamespaceBlob.FetchForBlobAsync(
                (CloudBlockBlob)ControllerOperations.GetBlobByName(DashConfiguration.NamespaceAccount, container, blobName, snapshot));
        }

        const int CreateRetryCount = 3;
        public static async Task<T> PerformNamespaceOperation<T>(string container, string blobName, Func<NamespaceBlob, Task<T>> operation)
        {
            // Allow namespace operations to be retried. Update operations (via NamespaceBlob.SaveAsync()) use pre-conditions to
            // resolve race conditions on the same namespace blob
            for (int retry = 0; retry < CreateRetryCount; retry++)
            {
                try
                {
                    var namespaceBlob = await FetchNamespaceBlobAsync(container, blobName);
                    return await operation(namespaceBlob);
                }
                catch (StorageException ex)
                {
                    if ((ex.RequestInformation.HttpStatusCode != (int)HttpStatusCode.PreconditionFailed &&
                        ex.RequestInformation.HttpStatusCode != (int)HttpStatusCode.Conflict) ||
                        retry >= CreateRetryCount - 1)
                    {
                        throw;
                    }
                }
            }
            // Never get here
            return default(T);
        }

    }
}