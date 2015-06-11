//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    /// <summary>
    /// NamespaceBlob that lives in the Namespace Storage Account
    /// </summary>
    internal class NamespaceBlobCloud : INamespaceBlob
    {
        private const string MetadataNameAccount    = "accountname";
        private const string MetadataNameContainer  = "container";
        private const string MetadataNameBlobName   = "blobname";
        private const string MetadataNameDeleteFlag = "todelete";

        private bool _isLoaded = false;
        private bool _cloudBlockBlobExists = false;

        private readonly Func<CloudBlockBlob> _getCloudBlockBlob;

        private CloudBlockBlob _cloudBlockBlob;
        private CloudBlockBlob CloudBlockBlob
        {
            get
            {
                if (_isLoaded == false)
                {
                    _cloudBlockBlob = _getCloudBlockBlob();
                    _cloudBlockBlobExists = _cloudBlockBlob.Exists();
                    _isLoaded = true;
                }
                return _cloudBlockBlob;
            }
        }

        public virtual string AccountName
        {
            get { return TryGetMetadataValue(MetadataNameAccount); }
            set { CloudBlockBlob.Metadata[MetadataNameAccount] = value; }
        }

        public virtual string Container
        {
            get { return TryGetMetadataValue(MetadataNameContainer); }
            set { CloudBlockBlob.Metadata[MetadataNameContainer] = value; }
        }

        public virtual string BlobName
        {
            get { return TryGetMetadataValue(MetadataNameBlobName); }
            set { CloudBlockBlob.Metadata[MetadataNameBlobName] = value; }
        }

        public virtual bool? IsMarkedForDeletion
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
                await CloudBlockBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(CloudBlockBlob.Properties.ETag), null, null);
            }
            else
            {
                await CloudBlockBlob.UploadTextAsync("", Encoding.UTF8, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null);
                _cloudBlockBlobExists = true;
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

        /// <summary>
        /// NamespaceBlobCloud
        /// </summary>
        /// <param name="getCloudBlockBlob">Function delegate to retrieve the NamespaceBlob from Storage</param>
        public NamespaceBlobCloud(Func<CloudBlockBlob> getCloudBlockBlob)
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