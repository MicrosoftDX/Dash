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
        public async Task<HttpResponseMessage> ListContainers()
        {
            CloudStorageAccount masterAccount = GetMasterAccount();
            HttpClient client = new HttpClient();
            HttpResponseMessage response = new HttpResponseMessage();
            return await client.SendAsync(Request, HttpCompletionOption.ResponseHeadersRead);
        }
    }
}