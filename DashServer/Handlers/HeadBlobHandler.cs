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

namespace Microsoft.WindowsAzure.Storage.DataAtScaleHub.ProxyServer
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Configuration;

    class HeadBlobHandler : Handler
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
            

            bool exists = checkIfExists(request, masterAccount, containerName);

            if (exists)
            {
                //reading metadata from namespace blob
                ReadMetaDataHeadGet(request, masterAccount, out blobUri, out accountName, out accountKey, out containerName, out blobName);

                response = new HttpResponseMessage();
                base.FormRedirectResponse(blobUri, accountName, accountKey, containerName, blobName, request, ref response);
            }
            else
            {
                response.StatusCode = HttpStatusCode.NotFound;
            }

            return response;

        }


        private bool checkIfExists(HttpRequestMessage request, CloudStorageAccount masterAccount, string containerName)
        {
            string blobName = request.RequestUri.LocalPath.Substring(containerName.Length + 2);

            CloudBlockBlob namespaceBlob = GetBlobByName(masterAccount, containerName, blobName);

            if (namespaceBlob.Exists())
                return true;
            else
                return false;
        }

        protected void ReadMetaDataHeadGet(HttpRequestMessage request, CloudStorageAccount masterAccount, out Uri blobUri, out String accountName, out String accountKey, out String containerName, out String blobName)
        {
            blobName = "";
            containerName = "";
            GetNamesFromUri(request.RequestUri, out containerName, out blobName);

            CloudBlockBlob namespaceBlob = GetBlobByName(masterAccount, blobName, containerName);

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
