﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    public class NamespaceBlob : INamespaceBlob
    {
        private readonly NamespaceBlobCache _cachedNamespaceBlob;
        private readonly NamespaceBlobCloub _cloudNamespaceBlob;
        private static readonly bool CacheIsEnabled = Boolean.Parse(AzureUtils.GetConfigSetting("CacheIsEnabled", Boolean.FalseString));

        private NamespaceBlob(NamespaceBlobCache cachedNamespaceBlob, NamespaceBlobCloub cloudNamespaceBlob)
        {
            _cachedNamespaceBlob = cachedNamespaceBlob;
            _cloudNamespaceBlob = cloudNamespaceBlob;
        }

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

            var cloudNamespaceBlob = new NamespaceBlobCloub(() => (CloudBlockBlob)NamespaceHandler.GetBlobByName(DashConfiguration.NamespaceAccount, container, blobName, snapshot));
            NamespaceBlobCache cachedNamespaceBlob = null;

            if (CacheIsEnabled)
            {
                cachedNamespaceBlob = (NamespaceBlobCache) await NamespaceBlobCache.FetchAsync(container, blobName, snapshot);
                if (cachedNamespaceBlob == null)
                {
                    // save to cache
                    cachedNamespaceBlob = new NamespaceBlobCache(cloudNamespaceBlob, container, blobName, snapshot);
                    await cachedNamespaceBlob.SaveAsync();
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
                SetProperty(p => _cloudNamespaceBlob.Container = p, p => _cachedNamespaceBlob.Container = p, value);
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
                SetProperty(p => _cloudNamespaceBlob.BlobName = p, p => _cachedNamespaceBlob.BlobName = p, value);
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
            if (CacheIsEnabled)
            {
                await _cachedNamespaceBlob.SaveAsync();
            }
            await _cloudNamespaceBlob.SaveAsync();
        }

        public async Task<bool> ExistsAsync(bool forceRefresh = false)
        {
            return await _cloudNamespaceBlob.ExistsAsync(forceRefresh);
        }

        private void SetProperty<T>(Action<T> cloudAction, Action<T> cacheAction, T value)
        {
            cloudAction(value);
            if (CacheIsEnabled)
            {
                cacheAction(value);
            }
        }
    }
}