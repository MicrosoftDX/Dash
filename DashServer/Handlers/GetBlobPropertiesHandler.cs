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

namespace Microsoft.WindowsAzure.Storage.DataAtScaleHub.ProxyServer.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Configuration;

    class GetBlobPropertiesHandler : Handler
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

            HttpResponseMessage response = new HttpResponseMessage();

            //reading metadata from namespace blob
            ReadMetaData(request, masterAccount, out blobUri, out accountName, out accountKey, out containerName, out blobName);
            base.FormRedirectResponse(blobUri, accountName, accountKey, containerName, blobName, request, ref response);

            return response;
        }
    }
}
