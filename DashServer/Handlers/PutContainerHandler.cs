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

    class PutContainerHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            HttpRequestMessage requestKeeper = new HttpRequestMessage(request.Method, request.RequestUri);

            CreateMasterContainer(request, masterAccount);

            HttpClient client = new HttpClient();

            try
            {
                HttpResponseMessage response = new HttpResponseMessage();

                response.StatusCode = HttpStatusCode.Created;//FIXME

                request.Content = null;
                //response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                //if the creation of a namespace container is successfull we create all containers in other partition storage accounts

                Int32 numOfAccounts = Convert.ToInt32(ConfigurationManager.AppSettings["ScaleoutNumberOfAccounts"]);

                    //going through all storage accounts to create same container in all of them
                    for (int currAccount = 0; currAccount < numOfAccounts; currAccount++)
                    {
                        CreateContainer(request, currAccount, masterAccount);
                    }

                TreeCopyProxyTrace.TraceInformation("[ProxyHandler] Outgoing response: {0}.", response);

                return response;
            }
            catch (Exception e)
            {
                TreeCopyProxyTrace.TraceWarning("[ProxyHandler] Exception ocurred while relaying request {0}: {1}", request.RequestUri, e);
                throw;
            }
        }

        private void CreateMasterContainer(HttpRequestMessage request, CloudStorageAccount masterAccount)
        {
            string accountName = "";
            string accountKey = "";

            //get master container reference
            StorageCredentials credentials = new StorageCredentials(masterAccount.Credentials.AccountName, masterAccount.Credentials.ExportBase64EncodedKey());
            CloudStorageAccount account = new CloudStorageAccount(credentials, false);

            var blobContainer = ContainerFromRequest(account, request);

            blobContainer.CreateIfNotExists();
        }

        private void CreateContainer(HttpRequestMessage request, int currAccount, CloudStorageAccount masterAccount)
        {
            string accountName = "";
            string accountKey = "";

            base.readAccountData(masterAccount, currAccount, out accountName, out accountKey);

            //get container reference
            StorageCredentials credentials = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount account = new CloudStorageAccount(credentials, false);
            var blobContainer = ContainerFromRequest(account, request);

            blobContainer.CreateIfNotExists(); 
        }
    }
}
