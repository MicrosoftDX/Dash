using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;

namespace Microsoft.WindowsAzure.Storage.TreeCopyProxy.ProxyServer.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Configuration;

    class ListContainersHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            HttpClient client = new HttpClient();

            try
            {
                HttpResponseMessage response = new HttpResponseMessage();
                request.Content = null;
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                TreeCopyProxyTrace.TraceInformation("[ProxyHandler] Outgoing response: {0}.", response);

                return response;
            }
            catch (Exception e)
            {
                TreeCopyProxyTrace.TraceWarning("[ProxyHandler] Exception ocurred while relaying request {0}: {1}", request.RequestUri, e);
                throw;
            }

        }

     
    }
}
