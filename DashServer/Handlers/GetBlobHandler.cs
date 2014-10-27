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

    class GetBlobHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            String accountName = "";
            String accountKey = "";
            Uri blobUri;
            String containerName = request.RequestUri.AbsolutePath.Substring(1,
                                                                         request.RequestUri.AbsolutePath
                                                                                .IndexOf('/', 2) - 1); ;
            String blobName = "";
            HttpResponseMessage response;

            //reading metadata from namespace blob
            ReadMetaDataForGetOperation(request, masterAccount, out blobUri, out accountName, out accountKey, out containerName, out blobName);

            response = new HttpResponseMessage();
            base.FormRedirectResponse(blobUri, accountName, accountKey, containerName, blobName, request, ref response);

            return response;

            //old forwarding code

            //forming forwarding request
            //base.FormRedirectRequest(blobUri, accountName, accountKey, ref request);

            //HttpClient client = new HttpClient();
            //try
            //{
            //    HttpResponseMessage response = new HttpResponseMessage();
            //    response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            //    TreeCopyProxyTrace.TraceInformation("[ProxyHandler] Outgoing response: {0}.", response);
            //    return response;
            //}
            //catch (Exception e)
            //{
            //    TreeCopyProxyTrace.TraceWarning("[ProxyHandler] Exception ocurred while relaying request {0}: {1}", request.RequestUri, e);
            //    throw;
            //}
        }

        protected void ReadMetaDataForGetOperation(HttpRequestMessage request, CloudStorageAccount masterAccount, out Uri blobUri, out String accountName, out String accountKey, out String containerName, out String blobName)
        {
            blobName = System.IO.Path.GetFileName(request.RequestUri.LocalPath);
            containerName = request.RequestUri.AbsolutePath.Substring(1, request.RequestUri.AbsolutePath.IndexOf('/', 2) - 1);

            CloudBlockBlob namespaceBlob = GetBlobByName(masterAccount, containerName, blobName);

            //Get blob metadata
            namespaceBlob.FetchAttributes();


            blobUri = new Uri(namespaceBlob.Metadata["link"]);
            accountName = namespaceBlob.Metadata["accountname"];
            accountKey = namespaceBlob.Metadata["accountkey"];
            containerName = namespaceBlob.Metadata["container"];
            blobName = namespaceBlob.Metadata["blobname"];
        }
    }
}
