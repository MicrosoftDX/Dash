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

namespace Microsoft.WindowsAzure.Storage.TreeCopyProxy.ProxyServer
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Configuration;

    class PutBlockHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            String accountName = "";
            String accountKey = "";
            Uri blobUri;
            String containerName = "";
            String blobName = "";

            base.CreateNamespaceBlob(request, masterAccount);

            //reading metadata from namespace blob
            base.ReadMetaData(request, masterAccount, out blobUri, out accountName, out accountKey, out containerName, out blobName);



            //if (request.Content.Headers.ContentLength == 0)
            //{
                //redirection code
                HttpResponseMessage response = new HttpResponseMessage();
                base.FormRedirectResponse(blobUri, accountName, accountKey, containerName, blobName, request, ref response);
                return response;
            //}
            //else
            //{
            //    //forwarding code
            //    base.FormForwardingRequest(blobUri, accountName, accountKey, ref request);

            //    HttpClient client = new HttpClient();
            //    try
            //    {
            //        HttpResponseMessage response = new HttpResponseMessage();
            //        response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            //        TreeCopyProxyTrace.TraceInformation("[ProxyHandler] Outgoing response: {0}.", response);
            //        return response;
            //    }
            //    catch (Exception e)
            //    {
            //        TreeCopyProxyTrace.TraceWarning("[ProxyHandler] Exception ocurred while relaying request {0}: {1}", request.RequestUri, e);
            //        throw;
            //    }
            //}

        }

    }
}
