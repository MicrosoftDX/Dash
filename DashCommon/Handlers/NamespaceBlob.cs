﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    public class NamespaceBlob : INamespaceBlob
    {
        private readonly INamespaceBlob _cachedNamespaceBlob;
        private readonly INamespaceBlob _cloudNamespaceBlob;
        internal static bool CacheIsEnabled = Boolean.Parse(AzureUtils.GetConfigSetting("CacheIsEnabled", Boolean.FalseString));

        internal NamespaceBlob(INamespaceBlob cachedNamespaceBlob, INamespaceBlob cloudNamespaceBlob)
        {
            _cachedNamespaceBlob = cachedNamespaceBlob;
            _cloudNamespaceBlob = cloudNamespaceBlob;
        }

        /// <summary>
        /// Fetches an instance of NamespaceBlob.
        /// </summary>
        /// <param name="container">container name</param>
        /// <param name="blobName">blob name</param>
        /// <param name="snapshot">snapshot name</param>
        /// <returns>NamespaceBlob</returns>
        public async static Task<NamespaceBlob> FetchAsync(string container, string blobName, string snapshot = null)
        {
            if (String.IsNullOrEmpty(container))
            {
                throw new ArgumentNullException("container");
            }

            if (String.IsNullOrEmpty(blobName))
            {
                throw new ArgumentNullException("blobName");
            }

            var cloudNamespaceBlob = new NamespaceBlobCloud(() => (CloudBlockBlob)NamespaceHandler.GetBlobByName(DashConfiguration.NamespaceAccount, container, blobName, snapshot));
            NamespaceBlobCache cachedNamespaceBlob = null;

            if (CacheIsEnabled)
            {
                cachedNamespaceBlob = await NamespaceBlobCache.FetchAsync(container, blobName, snapshot);
                if (cachedNamespaceBlob == null)
                {
                    // cache miss
                    cachedNamespaceBlob = new NamespaceBlobCache(cloudNamespaceBlob, container, blobName, snapshot);

                    if (await cloudNamespaceBlob.ExistsAsync())
                    {
                        // save to cache if the namespace blob actually exists in the cloud
                        await cachedNamespaceBlob.SaveAsync();
                    }
                }
            }

            return new NamespaceBlob(cachedNamespaceBlob, cloudNamespaceBlob);
        }

        public string AccountName
        {
            get 
            {
                return CacheIsEnabled ? _cachedNamespaceBlob.AccountName : _cloudNamespaceBlob.AccountName;
            }

            set
            {
                _cloudNamespaceBlob.AccountName = value;
                if (CacheIsEnabled)
                {
                    _cachedNamespaceBlob.AccountName = value;
                }
            }
        }

        public string Container
        {
            get 
            {
                return CacheIsEnabled ? _cachedNamespaceBlob.Container: _cloudNamespaceBlob.Container;
            }

            set
            {
                _cloudNamespaceBlob.Container = value;
                if (CacheIsEnabled)
                {
                    _cachedNamespaceBlob.Container = value;
                }
            }
        }

        public string BlobName
        {
            get
            {
                return CacheIsEnabled ? _cachedNamespaceBlob.BlobName: _cloudNamespaceBlob.BlobName;
            }

            set
            {
                _cloudNamespaceBlob.BlobName = value;
                if (CacheIsEnabled)
                {
                    _cachedNamespaceBlob.BlobName = value;
                }
            }
        }

        public bool? IsMarkedForDeletion
        {
            get
            {
                return CacheIsEnabled ? _cachedNamespaceBlob.IsMarkedForDeletion: _cloudNamespaceBlob.IsMarkedForDeletion ?? false;
            }

            set
            {
                _cloudNamespaceBlob.IsMarkedForDeletion = value;
                if (CacheIsEnabled)
                {
                    _cachedNamespaceBlob.IsMarkedForDeletion = value;
                }
            }
        }

        public async Task SaveAsync()
        {
            await _cloudNamespaceBlob.SaveAsync();

            if (CacheIsEnabled)
            {
                await _cachedNamespaceBlob.SaveAsync();
            }
        }

        public async Task<bool> ExistsAsync(bool forceRefresh = false)
        {
            var exists = false;

            if (CacheIsEnabled)
            {
                exists = await _cachedNamespaceBlob.ExistsAsync(forceRefresh);
            }

            if (!exists)
            {
                exists = await _cloudNamespaceBlob.ExistsAsync(forceRefresh);
            }

            return exists;
        }
    }
}