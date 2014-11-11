//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Server.Controllers
{
    public class NamespaceBlob
    {
        CloudBlockBlob _namespaceBlob;

        public NamespaceBlob(CloudBlockBlob namespaceBlob)
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
            if (await _namespaceBlob.ExistsAsync())
            {
                await _namespaceBlob.FetchAttributesAsync();
            }
            else
            {
                await _namespaceBlob.UploadTextAsync("");
            }
        }

        public async Task SaveAsync()
        {
            await _namespaceBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(_namespaceBlob.Properties.ETag), null, null);
        }

        public async Task MarkForDeletion()
        {
            await RefreshAsync();
            _namespaceBlob.Metadata["todelete"] = "true";
            await SaveAsync();
        }

        public async Task<bool> ExistsAsync()
        {
            return await _namespaceBlob.ExistsAsync();
        }

        public string AccountName
        {
            get { return _namespaceBlob.Metadata["accountname"]; }
            set { _namespaceBlob.Metadata["accountname"] = value; }
        }

        public string Container
        {
            get { return _namespaceBlob.Metadata["container"]; }
            set { _namespaceBlob.Metadata["container"] = value; }
        }

        public string BlobName
        {
            get { return _namespaceBlob.Metadata["blobname"]; }
            set { _namespaceBlob.Metadata["blobname"] = value; }
        }
    }
}