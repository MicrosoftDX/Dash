//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Processors
{
    public static class AccountManager
    {
        public static void ImportAccounts(IEnumerable<string> importAccounts)
        {
            importAccounts
                .AsParallel()
                .ForAll(account => {
                    var completionTask = ImportAccountAsync(account);
                });
        }

        public static async Task ImportAccountAsync(string accountName)
        {
            await OperationRunner.DoActionAsync(String.Format("Importing data account: {0}", accountName), async () =>
            {
                // This method will only import the blobs into the namespace. A future task may be
                // to redistribute the blobs to balance the entire virtual account.
                var account = DashConfiguration.GetDataAccountByAccountName(accountName);
                if (account == null)
                {
                    DashTrace.TraceWarning("Failure importing storage account: {0}. The storage account has not been configured as part of this virtual account",
                        accountName);
                    return;
                }
                // Check if we've already imported this account
                var accountClient = account.CreateCloudBlobClient();
                var namespaceClient = DashConfiguration.NamespaceAccount.CreateCloudBlobClient();
                var accountContainers = await ListContainersAsync(accountClient);
                if (!accountContainers.Any())
                {
                    // No containers - nothing to import
                    DashTrace.TraceInformation("Importing storage account: {0}. This account has no blob containers and so there is nothing to import.");
                    return;
                }
                var status = await AccountStatus.GetAccountStatus(accountName);
                await status.UpdateStatusInformation(AccountStatus.States.Healthy, "Importing storage account: {0} into virtual account", accountName);
                bool alreadyImported = false;
                await GetAccountBlobs(accountClient, async (blobItem) => 
                    {
                        var blob = (ICloudBlob)blobItem;
                        var namespaceBlob = await NamespaceBlob.FetchForBlobAsync(
                            (CloudBlockBlob)NamespaceHandler.GetBlobByName(DashConfiguration.NamespaceAccount, blob.Container.Name, blob.Name, blob.IsSnapshot ? blob.SnapshotTime.ToString() : String.Empty));
                        alreadyImported = await namespaceBlob.ExistsAsync(true) && String.Equals(accountName, namespaceBlob.AccountName, StringComparison.OrdinalIgnoreCase);
                        return false;
                    });
                if (alreadyImported)
                {
                    await status.UpdateStatusWarning("Importing storage account: {0} has already been imported. This account cannot be imported again.", accountName);
                    return;
                }
                // Sync the container structure first - add containers in the imported account to the virtual account
                await status.UpdateStatusInformation("Importing storage account: {0}. Synchronizing container structure", accountName);
                int containersAddedCount = 0, containersWarningCount = 0;
                var namespaceContainers = await ListContainersAsync(namespaceClient);
                await ProcessContainerDifferencesAsync(accountContainers, namespaceContainers, async (newContainerName, accountContainer) =>
                    {
                        var createContainerResult = await ContainerHandler.DoForAllContainersAsync(newContainerName,
                            HttpStatusCode.Created,
                            async newContainer => await CopyContainer(accountContainer, newContainer),
                            true,
                            new[] { account });
                        if (createContainerResult.StatusCode < HttpStatusCode.OK || createContainerResult.StatusCode >= HttpStatusCode.Ambiguous)
                        {
                            await status.UpdateStatusWarning("Importing storage account: {0}. Failed to create container: {1} in virtual account. Details: {2}, {3}",
                                accountName,
                                newContainerName,
                                createContainerResult.StatusCode.ToString(),
                                createContainerResult.ReasonPhrase);
                            containersWarningCount++;
                        }
                        else
                        {
                            containersAddedCount++;
                        }
                    },
                    (newContainerName, ex) =>
                    {
                        status.UpdateStatusWarning("Importing storage account: {0}. Error processing container {1}. Details: {2}",
                            accountName,
                            newContainerName,
                            ex.ToString()).Wait();
                        containersWarningCount++;
                    });
                // Sync the other way
                await ProcessContainerDifferencesAsync(namespaceContainers, accountContainers, async (newContainerName, namespaceContainer) =>
                    {
                        await CopyContainer(namespaceContainer, accountClient.GetContainerReference(newContainerName));
                    },
                    (newContainerName, ex) =>
                    {
                        status.UpdateStatusWarning("Importing storage account: {0}. Error replicating container {1} to imported account. Details: {2}",
                            accountName,
                            newContainerName,
                            ex.ToString()).Wait();
                        containersWarningCount++;
                    });
                DashTrace.TraceInformation("Importing storage account: {0}. Synchronized containers structure. {1} containers added to virtual account. {2} failures/warnings.",
                    accountName,
                    containersAddedCount,
                    containersWarningCount);

                // Start importing namespace entries
                await status.UpdateStatusInformation("Importing storage account: {0}. Adding blob entries to namespace", accountName);
                int blobsAddedCount = 0, warningCount = 0, duplicateCount = 0;
                await GetAccountBlobs(accountClient, async (blobItem) =>
                {
                    var blob = (ICloudBlob)blobItem;
                    try
                    {
                        var namespaceBlob = await NamespaceBlob.FetchForBlobAsync(
                            (CloudBlockBlob)NamespaceHandler.GetBlobByName(DashConfiguration.NamespaceAccount, blob.Container.Name, blob.Name, blob.IsSnapshot ? blob.SnapshotTime.ToString() : String.Empty));
                        if (await namespaceBlob.ExistsAsync())
                        {
                            if (!String.Equals(namespaceBlob.AccountName, accountName, StringComparison.OrdinalIgnoreCase))
                            {
                                await status.UpdateStatusWarning("Importing storage account: {0}. Adding blob: {1}/{2} would result in a duplicate blob entry. This blob will NOT be imported into the virtual account. Manually add the contents of this blob to the virtual account.",
                                    accountName, 
                                    blob.Container.Name,
                                    blob.Name);
                                duplicateCount++;
                            }
                        }
                        else
                        {
                            namespaceBlob.AccountName = accountName;
                            namespaceBlob.Container = blob.Container.Name;
                            namespaceBlob.BlobName = blob.Name;
                            namespaceBlob.IsMarkedForDeletion = false;
                            await namespaceBlob.SaveAsync();
                            blobsAddedCount++;
                        }
                    }
                    catch (StorageException ex)
                    {
                        status.UpdateStatusWarning("Importing storage account: {0}. Error importing blob: {0}/{1} into virtual namespace. Details: {3}",
                            accountName,
                            blob.Container.Name,
                            blob.Name,
                            ex.ToString()).Wait();
                        warningCount++;
                    }
                    return true;
                });

                if (status.State < AccountStatus.States.Warning)
                {
                    await status.UpdateStatus(String.Empty, AccountStatus.States.Unknown, TraceLevel.Off);
                }
                DashTrace.TraceInformation("Successfully imported the contents of storage account: '{0}' into the virtual namespace. Blobs added: {1}, duplicates detected: {2}, errors encountered: {3}",
                    accountName, blobsAddedCount, duplicateCount, warningCount);
            }, 
            ex =>
            {
                var status = AccountStatus.GetAccountStatus(accountName).Result;
                status.UpdateStatusWarning("Error importing storage account: {0} into virtual account. Details: {1}", accountName, ex.ToString()).Wait();
            }, false, true);
        }

        static async Task<bool> GetAccountBlobs(CloudBlobClient client, Func<IListBlobItem, Task<bool>> processBlob)
        {
            return await StorageListAsync.ListCallbackAsync(client, async (containers) =>
            {
                foreach (var container in containers)
                {
                    bool continueEnum = await StorageListAsync.ListCallbackAsync(container, async (blobs) => 
                    {
                        foreach (var blob in blobs)
                        {
                            if (!await processBlob(blob))
                            {
                                return false;
                            }
                        }
                        return true;
                    });
                    if (!continueEnum)
                    {
                        return false;
                    }
                }
                return true;
            });
        }

        static async Task<IDictionary<string, CloudBlobContainer>> ListContainersAsync(CloudBlobClient client)
        {
            return (await Task.Factory.StartNew(() => client.ListContainers(null, ContainerListingDetails.All, null, null)))
                .ToDictionary(container => container.Name, StringComparer.OrdinalIgnoreCase);
        }

        static async Task ProcessContainerDifferencesAsync(IDictionary<string, CloudBlobContainer> targetContainers, 
            IDictionary<string, CloudBlobContainer> sourceContainers,
            Func<string, CloudBlobContainer, Task> action,
            Action<string, StorageException> exceptionHandler)
        {
            foreach (var newContainer in targetContainers
                                            .Where(container => !sourceContainers.ContainsKey(container.Key)))
            {
                try
                {
                    await action(newContainer.Key, newContainer.Value);
                }
                catch (StorageException ex)
                {
                    exceptionHandler(newContainer.Key, ex);
                }
            }
        }

        static async Task CopyContainer(CloudBlobContainer sourceContainer, CloudBlobContainer destContainer)
        {
            await sourceContainer.FetchAttributesAsync();
            var access = await sourceContainer.GetPermissionsAsync();
            await destContainer.CreateIfNotExistsAsync(access.PublicAccess, null, null);
            await destContainer.SetPermissionsAsync(access);
            destContainer.Metadata.Clear();
            foreach (var metadatum in sourceContainer.Metadata)
            {
                destContainer.Metadata.Add(metadatum);
            }
            await destContainer.SetMetadataAsync();
        }
    }
}
