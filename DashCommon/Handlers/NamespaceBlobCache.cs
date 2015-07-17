//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Cache;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Common.Handlers
{
    internal class NamespaceBlobCache : INamespaceBlob
    {
        private static readonly int CacheExpirationInMinutes = AzureUtils.GetConfigSetting("CacheExpirationInMinutes", 720);

        public string AccountName { get; set; }

        public string Container { get; set; }

        public string BlobName { get; set; }

        public bool? IsMarkedForDeletion { get; set; }

        public string PrimaryAccountName { get; set; }

        public IList<string> DataAccounts { get; private set; }
        public bool AddDataAccount(string dataAccount)
        {
            var dataAccounts = this.DataAccounts;
            if (!dataAccounts.Contains(dataAccount, StringComparer.OrdinalIgnoreCase))
            {
                dataAccounts.Add(dataAccount);
                return true;
            }

            return false;
        }

        public bool RemoveDataAccount(string dataAccount)
        {
            var dataAccounts = this.DataAccounts;
            if (dataAccounts.Contains(dataAccount, StringComparer.OrdinalIgnoreCase))
            {
                dataAccounts.RemoveAt(((List<string>)dataAccounts)
                                            .FindIndex(account => String.Equals(account, dataAccount, StringComparison.OrdinalIgnoreCase)));

                return true;
            }

            return false;
        }

        public bool IsReplicated { get; private set; }

        private string Snapshot { get; set; }

        public NamespaceBlobCache()
        {
        }

        public NamespaceBlobCache(INamespaceBlob namespaceBlob, string snapshot = null)
        {
            if (namespaceBlob == null)
            {
                throw new ArgumentNullException("namespaceBlob");
            }

            AccountName = namespaceBlob.AccountName;
            Container = namespaceBlob.Container;
            BlobName = namespaceBlob.BlobName;
            IsMarkedForDeletion = namespaceBlob.IsMarkedForDeletion;
            PrimaryAccountName = namespaceBlob.PrimaryAccountName;
            DataAccounts = namespaceBlob.DataAccounts;
            IsReplicated = namespaceBlob.IsReplicated;
            Snapshot = snapshot;
        }

        public async Task SaveAsync()
        {
            await CacheStore.SetAsync(GetCacheKey(), this, TimeSpan.FromMinutes(CacheExpirationInMinutes));
        }

        public async Task<bool> ExistsAsync(bool forceRefresh)
        {
            return await CacheStore.ExistsAsync(GetCacheKey());
        }

        public async Task<bool> DeleteAsync()
        {
            return await CacheStore.DeleteAsync(GetCacheKey());
        }

        public static async Task<NamespaceBlobCache> FetchAsync(string container, string blobName, string snapshot = null)
        {
            var key = BuildCacheKey(container, blobName, snapshot);
            return await CacheStore.GetAsync<NamespaceBlobCache>(key);
        }

        private string GetCacheKey()
        {
            return BuildCacheKey(Container, BlobName, Snapshot);
        }

        private static string BuildCacheKey(string container, string blobName, string snapshot = null)
        {
            return String.Join("|", container, blobName, snapshot);
        }
    }
}