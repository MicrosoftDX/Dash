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

namespace Microsoft.WindowsAzure.Storage.DataAtScaleHub.ProxyServer.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Configuration;

    class OptionsMethodHandler : Handler
    {

        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            if (response.Content == null)
            {
                response.Content = new StringContent("");
            }
            response.Content.Headers.Allow.Add("HEAD");
            response.Content.Headers.Allow.Add("PUT");
            response.Content.Headers.Allow.Add("GET");
            response.Content.Headers.Allow.Add("OPTIONS");
            response.Content.Headers.Allow.Add("LIST");
            response.Content.Headers.Allow.Add("DELETE");
            response.Headers.Add("x-ms-redirect", "true");

            return response;
        }
    }
}
