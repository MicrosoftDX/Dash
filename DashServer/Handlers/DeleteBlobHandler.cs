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

            setNamespaceBlobForDeletion(request, masterAccount);

            //reading metadata from namespace blob
            base.ReadMetaData(request, masterAccount, out blobUri, out accountName, out accountKey, out containerName, out blobName);

            HttpResponseMessage response = new HttpResponseMessage();
            base.FormRedirectResponse(blobUri, accountName, accountKey, containerName, blobName, request, ref response);

            deleteNamespaceBlob(namespaceBlobUri, masterAccount);

            return response;


            ////old forwarding code

            //forming forwarding request
            //base.FormForwardingRequest(blobUri, accountName, accountKey, ref request);

            //HttpClient client = new HttpClient();
            //try
            //{
            //    HttpResponseMessage response = new HttpResponseMessage();
            //    response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            //    if (response.IsSuccessStatusCode)
            //    {
            //        deleteNamespaceBlob(namespaceBlobUri, masterAccount);
            //    }
            //    TreeCopyProxyTrace.TraceInformation("[ProxyHandler] Outgoing response: {0}.", response);
            //    return response;
            //}
            //catch (Exception e)
            //{
            //    TreeCopyProxyTrace.TraceWarning("[ProxyHandler] Exception ocurred while relaying request {0}: {1}", request.RequestUri, e);
            //    throw;
            //}
        }

        private void setNamespaceBlobForDeletion(HttpRequestMessage request, CloudStorageAccount masterAccount)
        {
            //create an namespace blob with hardcoded metadata
            CloudBlockBlob namespaceBlob = GetBlobByUri(masterAccount, request.RequestUri);

            if (!namespaceBlob.Exists())
            {
                throw new FileNotFoundException("Namespace blob not found");
            }

            namespaceBlob.FetchAttributes();
            namespaceBlob.Metadata["todelete"] = "true";
            namespaceBlob.SetMetadata();
        }

        private void deleteNamespaceBlob(Uri namespaceBlobUri, CloudStorageAccount masterAccount)
        {
            //creating blobClient for namespace Blob
            CloudBlockBlob blob = GetBlobByUri(masterAccount, namespaceBlobUri);

            blob.Delete();
        }
    }
}
