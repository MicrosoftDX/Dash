//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Cache;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Common.Handlers
{
    [Serializable]
    internal class NamespaceBlobCache : INamespaceBlob
    {
        private static readonly Lazy<CacheStore> LazyCacheStore = new Lazy<CacheStore>(() => new CacheStore());
        private static readonly int CacheExpirationInMinutes = AzureUtils.GetConfigSetting("CacheExpirationInMinutes", 720);

        internal static CacheStore CacheStore
        {
            get { return LazyCacheStore.Value; }
        }

        public Func<string> GetCacheKey;

        public string AccountName { get; set; }

        public string Container { get; set; }

        public string BlobName { get; set; }

        public bool? IsMarkedForDeletion { get; set; }

        public async Task SaveAsync()
        {
            await CacheStore.SetAsync(GetCacheKey(), this, TimeSpan.FromMinutes(CacheExpirationInMinutes));
        }

        public async Task<bool> ExistsAsync(bool forceRefresh)
        {
            return await CacheStore.ExistsAsync(GetCacheKey());
        }

        public NamespaceBlobCache(INamespaceBlob namespaceBlob, string container, string blobName, string snapshot = null)
        {
            if (String.IsNullOrEmpty(container))
            {
                throw new ArgumentNullException("container");
            }

            if (String.IsNullOrEmpty(blobName))
            {
                throw new ArgumentNullException("blobName");
            }

            this.AccountName = namespaceBlob.AccountName;
            this.Container = namespaceBlob.Container;
            this.BlobName = namespaceBlob.BlobName;
            this.IsMarkedForDeletion = namespaceBlob.IsMarkedForDeletion;

            this.GetCacheKey = () => BuildCacheKey(container, blobName, snapshot);
        }

        public static async Task<INamespaceBlob> FetchAsync(string container, string blobName, string snapshot = null)
        {
            var key = BuildCacheKey(container, blobName, snapshot);
            return await CacheStore.GetAsync<INamespaceBlob>(key);
        }

        private static string BuildCacheKey(string container, string blobName, string snapshot = null)
        {
            return String.Format("{0}-{1}-{2}", container, blobName, snapshot);
        }
    }
}