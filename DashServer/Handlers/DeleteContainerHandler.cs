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

    class DeleteContainerHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            HttpClient client = new HttpClient();

            try
            {
                HttpResponseMessage response = new HttpResponseMessage();

                //FormRedirectRequest2(masterAccount, ref request);

                DeleteMasterContainer(request, masterAccount);

                response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

                response.StatusCode = HttpStatusCode.Accepted;

                //if the deletion of a namespace container is successfull we delete all containers in other partition storage accounts
                if (response.IsSuccessStatusCode)
                {
                    int numOfAccounts = Convert.ToInt32(ConfigurationManager.AppSettings["ScaleoutNumberOfAccounts"]);

                    //going through all storage accounts to create same container in all of them
                    for (int currAccount = 0; currAccount < numOfAccounts; currAccount++)
                    {
                        DeleteContainer(request, currAccount, masterAccount);
                    }
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

        private void DeleteMasterContainer(HttpRequestMessage request, CloudStorageAccount masterAccount)
        {
            var blobContainer = ContainerFromRequest(masterAccount, request);

            blobContainer.Delete();
        }

        private void DeleteContainer(HttpRequestMessage request, int currAccount, CloudStorageAccount masterAccount)
        {
            string accountName = "";
            string accountKey = "";

            base.readAccountData(masterAccount, currAccount, out accountName, out accountKey);

            //get container reference
            StorageCredentials credentials = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount account = new CloudStorageAccount(credentials, false);

            var blobContainer = ContainerFromRequest(account, request);

            blobContainer.DeleteIfExists();

        }


    }
}
