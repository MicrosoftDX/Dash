//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Account")]
    public class AccountController : CommonController
    {
        [HttpGet]
        public async Task<IHttpActionResult> ListContainers()
        {
            CloudStorageAccount masterAccount = GetMasterAccount();
            UriBuilder forwardUri = new UriBuilder(Request.RequestUri.Scheme + "://" + masterAccount.Credentials.AccountName + Endpoint() + Request.RequestUri.Query);
            return Redirect(forwardUri.Uri);
        }

        [HttpPut]
        public async Task<HttpResponseMessage> SetBlobServiceProperties()
        {
            await Task.Delay(10);
            return new HttpResponseMessage();
        }
    }
}