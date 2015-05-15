//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Cache;

namespace Microsoft.Dash.Common.Handlers
{
    [Serializable]
    internal class NamespaceBlobCache : INamespaceBlob
    {
        internal static readonly CacheStore CacheStore = new CacheStore();

        public string CacheKey { get; private set; }

        public string AccountName { get; set; }

        public string Container { get; set; }

        public string BlobName { get; set; }

        public bool? IsMarkedForDeletion { get; set; }

        public async Task SaveAsync()
        {
            await CacheStore.SetAsync(CacheKey, this, TimeSpan.FromHours(1));
        }

        public NamespaceBlobCache(INamespaceBlob namespaceBlob, string container, string blobName, string snapshot = null)
        {
            this.AccountName = namespaceBlob.AccountName;
            this.Container = namespaceBlob.Container;
            this.BlobName = namespaceBlob.BlobName;
            this.IsMarkedForDeletion = namespaceBlob.IsMarkedForDeletion;
            this.CacheKey = BuildCacheKey(container, blobName, snapshot);
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