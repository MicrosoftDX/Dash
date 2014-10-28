//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;

namespace Microsoft.Dash.Server.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Configuration;
    using Microsoft.WindowsAzure.Storage;

    class DeleteBlobHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            Uri namespaceBlobUri = request.RequestUri;

            String accountName = "";
            String accountKey = "";
            Uri blobUri;
            String containerName = "";
            String blobName = "";

            // Set Namespace Blob for deletion
            //create an namespace blob with hardcoded metadata
            CloudBlockBlob namespaceBlob = GetBlobByUri(masterAccount, request.RequestUri);

            if (!namespaceBlob.Exists())
            {
                throw new FileNotFoundException("Namespace blob not found");
            }

            namespaceBlob.FetchAttributes();
            namespaceBlob.Metadata["todelete"] = "true";
            namespaceBlob.SetMetadata();

            //reading metadata from namespace blob
            base.ReadMetaData(request, masterAccount, out blobUri, out accountName, out accountKey, out containerName, out blobName);

            HttpResponseMessage response = new HttpResponseMessage();
            base.FormRedirectResponse(blobUri, accountName, accountKey, containerName, blobName, request, ref response);

            CloudBlockBlob blob = GetBlobByUri(masterAccount, namespaceBlobUri);

            blob.Delete();

            return response;
        }
    }
}
