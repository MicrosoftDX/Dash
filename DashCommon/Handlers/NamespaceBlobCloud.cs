//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    internal class NamespaceBlobCloub : INamespaceBlob
    {
        private const string MetadataNameAccount    = "accountname";
        private const string MetadataNameContainer  = "container";
        private const string MetadataNameBlobName   = "blobname";
        private const string MetadataNameDeleteFlag = "todelete";

        private bool _isLoaded = false;
        private bool _cloudBlockBlobExists;

        private readonly Func<CloudBlockBlob> _getCloudBlockBlob;

        private CloudBlockBlob _cloudBlockBlob;
        private CloudBlockBlob CloudBlockBlob
        {
            get
            {
                _isLoaded = true;
                return _cloudBlockBlob ?? (_cloudBlockBlob = _getCloudBlockBlob());
            }
        }

        public string AccountName
        {
            get { return TryGetMetadataValue(MetadataNameAccount); }
            set { CloudBlockBlob.Metadata[MetadataNameAccount] = value; }
        }

        public string Container
        {
            get { return TryGetMetadataValue(MetadataNameContainer); }
            set { CloudBlockBlob.Metadata[MetadataNameContainer] = value; }
        }

        public string BlobName
        {
            get { return TryGetMetadataValue(MetadataNameBlobName); }
            set { CloudBlockBlob.Metadata[MetadataNameBlobName] = value; }
        }

        public bool? IsMarkedForDeletion
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
                if (value == null || value == false)
                {
                    CloudBlockBlob.Metadata.Remove(MetadataNameDeleteFlag);
                }
                else
                {
                    CloudBlockBlob.Metadata[MetadataNameDeleteFlag] = Boolean.TrueString;
                }
            }
        }

        public async Task SaveAsync()
        {
            if (await ExistsAsync())
            {
                await CloudBlockBlob.UploadTextAsync("", Encoding.UTF8, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null);
            }
            else
            {
                await CloudBlockBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(CloudBlockBlob.Properties.ETag), null, null);
            }
        }

        public async Task<bool> ExistsAsync(bool forceRefresh = false)
        {
            if (forceRefresh || !_isLoaded)
            {
                _cloudBlockBlobExists = await CloudBlockBlob.ExistsAsync();
            }
            return _cloudBlockBlobExists;
        }

        public NamespaceBlobCloub(Func<CloudBlockBlob> getCloudBlockBlob)
        {
            _getCloudBlockBlob = getCloudBlockBlob;
        }

        private string TryGetMetadataValue(string metadataName)
        {
            string retval;
            CloudBlockBlob.Metadata.TryGetValue(metadataName, out retval);
            return retval;
        }
    }
}