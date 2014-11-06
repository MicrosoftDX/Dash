using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

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