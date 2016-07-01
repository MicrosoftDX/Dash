//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    public static class ContainerHandler
    {
        public static async Task<SimpleHttpResponse> CreateContainer(string container, BlobContainerPublicAccessType access = BlobContainerPublicAccessType.Off, IEnumerable<CloudStorageAccount> excludeAccounts = null)
        {
            return await DoForAllContainersAsync(container,
                HttpStatusCode.Created,
                async containerObj => await containerObj.CreateAsync(access, null, null),
                false,
                excludeAccounts);
        }

        public static async Task<SimpleHttpResponse> DeleteContainer(string container, IEnumerable<CloudStorageAccount> excludeAccounts = null)
        {
            return await DoForAllContainersAsync(container,
                HttpStatusCode.Accepted,
                async containerObj => await containerObj.DeleteAsync(),
                false,
                excludeAccounts);
        }

        public static async Task<SimpleHttpResponse> DoForAllContainersAsync(string container, 
            HttpStatusCode successStatus, 
            Func<CloudBlobContainer, Task> action, 
            bool ignoreNotFound,
            IEnumerable<CloudStorageAccount> excludeAccounts = null)
        {
            return await DoForContainersAsync(container, successStatus, action, DashConfiguration.AllAccounts, ignoreNotFound, excludeAccounts);
        }

        public static async Task<SimpleHttpResponse> DoForDataContainersAsync(string container, 
            HttpStatusCode successStatus, 
            Func<CloudBlobContainer, Task> action, 
            bool ignoreNotFound,
            IEnumerable<CloudStorageAccount> excludeAccounts = null)
        {
            return await DoForContainersAsync(container, successStatus, action, DashConfiguration.DataAccounts, ignoreNotFound, excludeAccounts);
        }

        private static Task<SimpleHttpResponse> DoForContainersAsync(string container,
            HttpStatusCode successStatus,
            Func<CloudBlobContainer, Task> action,
            IEnumerable<CloudStorageAccount> accounts,
            bool ignoreNotFound,
            IEnumerable<CloudStorageAccount> excludeAccounts)
        {
            return NamespaceHandler.DoForAccountsAsync(successStatus,
                (client) => action(client.GetContainerReference(container)),
                accounts,
                ignoreNotFound,
                excludeAccounts);
        }
    }
}
