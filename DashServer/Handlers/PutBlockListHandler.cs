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

namespace Microsoft.WindowsAzure.Storage.DataAtScaleHub.ProxyServer
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Configuration;

    class PutBlockListHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            //we keep original request before we change it
            HttpRequestMessage requestKeeper = new HttpRequestMessage(request.Method, request.RequestUri);

            String accountName = "";
            String accountKey = "";
            Uri blobUri;
            String containerName = "";
            String blobName = "";

            //reading metadata from namespace blob
            base.ReadMetaData(request, masterAccount, out blobUri, out accountName, out accountKey, out containerName, out blobName);

            //HttpResponseMessage response = new HttpResponseMessage();
            //base.FormRedirectResponse(blobUri, accountName, accountKey, containerName, blobName, request, ref response);

            //return response;


            //old forwarding code

            //forming forward request
            base.FormForwardingRequest(blobUri, accountName, accountKey, ref request);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = new HttpResponseMessage();
            response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            return response;
        }

    }
}
