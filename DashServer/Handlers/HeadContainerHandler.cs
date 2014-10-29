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
    class HeadContainerHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            request.Content = null;

            HttpResponseMessage response = new HttpResponseMessage();

            if (containerExists(request, masterAccount))
                response.StatusCode = HttpStatusCode.OK;
            else
                response.StatusCode = HttpStatusCode.NotFound;

            return response;


            //HttpClient client = new HttpClient();
            //try
            //{
            //    HttpResponseMessage response = new HttpResponseMessage();
            //    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            //    TreeCopyProxyTrace.TraceInformation("[ProxyHandler] Outgoing response: {0}.", response);
            //    return response;
            //}
            //catch (Exception e)
            //{
            //    TreeCopyProxyTrace.TraceWarning("[ProxyHandler] Exception ocurred while relaying request {0}: {1}", request.RequestUri, e);
            //    throw;
            //}
        }

        private bool containerExists(HttpRequestMessage request, CloudStorageAccount masterAccount)
        {
            
            StorageCredentials credentials = new StorageCredentials(masterAccount.Credentials.AccountName, masterAccount.Credentials.ExportBase64EncodedKey());
            CloudStorageAccount account = new CloudStorageAccount(credentials, false);

            var blobContainer = ContainerFromRequest(account, request);

            return blobContainer.Exists();
        }
    }
}
