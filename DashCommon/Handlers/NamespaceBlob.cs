//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    [Serializable]
    public class NamespaceBlob
    {
        private const string MetadataNameAccount    = "accountname";
        private const string MetadataNameContainer  = "container";
        private const string MetadataNameBlobName   = "blobname";
        private const string MetadataNameDeleteFlag = "todelete";
        private bool _blobExists;

        private readonly CloudBlockBlob _namespaceBlob;

        internal NamespaceBlob(CloudBlockBlob namespaceBlob)
        {
            _namespaceBlob = namespaceBlob;
        }

        public string AccountName
        {
            get { return TryGetMetadataValue(MetadataNameAccount); }
            set { _namespaceBlob.Metadata[MetadataNameAccount] = value; }
        }

        public string Container
        {
            get { return TryGetMetadataValue(MetadataNameContainer); }
            set { _namespaceBlob.Metadata[MetadataNameContainer] = value; }
        }

        public string BlobName
        {
            get { return TryGetMetadataValue(MetadataNameBlobName); }
            set { _namespaceBlob.Metadata[MetadataNameBlobName] = value; }
        }

        public bool IsMarkedForDeletion
        {
            get
            {
                var retval = false;
                var deleteFlag = TryGetMetadataValue(MetadataNameDeleteFlag);
                bool.TryParse(deleteFlag, out retval);
                return retval;
            }
            set
            {
                if (value)
                {
                    _namespaceBlob.Metadata[MetadataNameDeleteFlag] = "true";
                }
                else
                {
                    _namespaceBlob.Metadata.Remove(MetadataNameDeleteFlag);
                }
            }
        }

        public async static Task<NamespaceBlob> FetchForBlobAsync(CloudBlockBlob namespaceBlob)
        {
            var retval = new NamespaceBlob(namespaceBlob);
            await retval.RefreshAsync();
            return retval;
        }

        public async Task RefreshAsync()
        { 
            // Exists() populates attributes
            _blobExists = await _namespaceBlob.ExistsAsync();
        }

        public async Task SaveAsync()
        {
            if (!_blobExists)
            {
                await _namespaceBlob.UploadTextAsync("", Encoding.UTF8, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null);
            }
            else
            {
                await _namespaceBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(_namespaceBlob.Properties.ETag), null, null);
            }
        }

        public async Task MarkForDeletionAsync()
        {
            await RefreshAsync();
            this.IsMarkedForDeletion = true;
            await SaveAsync();
        }

        public async Task<bool> ExistsAsync(bool forceRefresh = false)
        {
            if (forceRefresh)
            {
                _blobExists = await _namespaceBlob.ExistsAsync();
            }
            return _blobExists;
        }

        private string TryGetMetadataValue(string metadataName)
        {
            string retval = String.Empty;
            _namespaceBlob.Metadata.TryGetValue(metadataName, out retval);
            return retval;
        }
    }
}