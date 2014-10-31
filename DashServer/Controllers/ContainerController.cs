//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Container")]
    public class ContainerController : CommonController
    {
        public async Task<IHttpActionResult> Get()
        {
            await Task.Delay(10);
            return Ok();
        }

        /// Put Container - http://msdn.microsoft.com/en-us/library/azure/dd179468.aspx
        [HttpPut]
        public async Task<HttpResponseMessage> CreateContainer(string container)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            //Create Master Container
            CloudBlobContainer masterContainer = GetContainerByName(masterAccount, container);
            await masterContainer.CreateIfNotExistsAsync();

            //if the creation of a namespace container is successful we create all containers in other partition storage accounts
            int numOfAccounts = NumOfAccounts();
            for (int currAccount = 0; currAccount < numOfAccounts; currAccount++)
            {
                CreateChildContainer(currAccount, masterAccount, container);
            }

            return new HttpResponseMessage(HttpStatusCode.Created);
        }

        /// Delete Container - http://msdn.microsoft.com/en-us/library/azure/dd179408.aspx
        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteContainer(string container)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            //Delete Master Container
            CloudBlobContainer masterContainer = GetContainerByName(masterAccount, container);
            await masterContainer.DeleteAsync();

            //if the deletion of a namespace container is successfull we delete all containers in other partition storage accounts
            int numOfAccounts = NumOfAccounts();

            //going through all storage accounts to create same container in all of them
            for (int currAccount = 0; currAccount < numOfAccounts; currAccount++)
            {
                DeleteChildContainer(masterAccount, currAccount, container);
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        [AcceptVerbs("GET", "HEAD")]
        //Get Container operations, with optional 'comp' parameter
        public async Task<IHttpActionResult> GetContainerData(string container, string comp)
        {
            switch (comp.ToLower())
            {
                case "list":
                    return await GetBlobList(container);
                default:
                    CloudStorageAccount masterAccount = GetMasterAccount();
                    Uri forwardUri = GetForwardingUri(RequestFromContext(HttpContext.Current), masterAccount.Credentials.AccountName, masterAccount.Credentials.ExportBase64EncodedKey(), container);
                    return Redirect(forwardUri);
            }
            
        }

        private async Task<IHttpActionResult> GetBlobList(string container)
        {
            //TODO
            await Task.Delay(10);
            return Ok();
        }

        private void CreateChildContainer(int currAccount, CloudStorageAccount masterAccount, string container)
        {
            string accountName = "";
            string accountKey = "";

            base.readAccountData(masterAccount, currAccount, out accountName, out accountKey);

            //get container reference
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            var blobContainer = GetContainerByName(account, container);

            blobContainer.CreateIfNotExists();
        }

        private void DeleteChildContainer(CloudStorageAccount masterAccount, int currAccount, string container)
        {
            string accountName = "";
            string accountKey = "";

            base.readAccountData(masterAccount, currAccount, out accountName, out accountKey);

            //get container reference
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            var blobContainer = GetContainerByName(account, container);

            blobContainer.DeleteIfExists();
        }
    }
}
