//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Server.Handlers
{
    public class NamespaceBlob
    {
        const string MetadataNameAccount    = "accountname";
        const string MetadataNameContainer  = "container";
        const string MetadataNameBlobName   = "blobname";
        const string MetadataNameDeleteFlag = "todelete";

        CloudBlockBlob _namespaceBlob;
        bool _blobExists;

        private NamespaceBlob(CloudBlockBlob namespaceBlob)
        {
            _namespaceBlob = namespaceBlob;
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
            await _namespaceBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(_namespaceBlob.Properties.ETag), null, null);
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

        public async Task CreateAsync()
        {
            await _namespaceBlob.UploadTextAsync("");
        }

        public string AccountName
        {
            get { return _namespaceBlob.Metadata[MetadataNameAccount]; }
            set { _namespaceBlob.Metadata[MetadataNameAccount] = value; }
        }

        public string Container
        {
            get { return _namespaceBlob.Metadata[MetadataNameContainer]; }
            set { _namespaceBlob.Metadata[MetadataNameContainer] = value; }
        }

        public string BlobName
        {
            get { return _namespaceBlob.Metadata[MetadataNameBlobName]; }
            set { _namespaceBlob.Metadata[MetadataNameBlobName] = value; }
        }

        public bool IsMarkedForDeletion
        {
            get
            {
                bool retval = false;
                string deleteFlag = String.Empty;
                if (_namespaceBlob.Metadata.TryGetValue(MetadataNameDeleteFlag, out deleteFlag))
                {
                    bool.TryParse(deleteFlag, out retval);
                }
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
    }
}