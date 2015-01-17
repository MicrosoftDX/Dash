//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using System.Collections.Generic;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Account")]
    public class AccountController : CommonController
    {
        [HttpGet]
        public IHttpActionResult ListContainers()
        {
            return ForwardRequest();
        }

        [HttpGet]
        public IHttpActionResult GetBlobServiceComp(string comp)
        {
            //Can just ignore the comp parameter as it just gets passed through with the forward Uri
            //comp can be one of either stats or properties
            return ForwardRequest(comp);
        }

        [HttpOptions]
        public async Task<HttpResponseMessage> OptionsCallAsync()
        {
            var request = HttpContextFactory.Current.Request;
            Uri forwardUri = ControllerOperations.ForwardUriToNamespace(request);
            HttpRequestMessage opRequest = new HttpRequestMessage(HttpMethod.Options, forwardUri);
            List<string> keysToCopy = new List<string>();
            keysToCopy.Add("Origin");
            keysToCopy.Add("Access-Control-Request-Method");
            keysToCopy.Add("Access-Control-Request-Headers");
            foreach (string key in keysToCopy)
            {
                opRequest.Headers.TryAddWithoutValidation(key, request.Headers.Get(key));
            }
            HttpClient client = new HttpClient();
            HttpResponseMessage opResponse = await client.SendAsync(opRequest);
            opResponse.Headers.Add("x-ms-dash-client", "true");
            return opResponse;
        }

        private IHttpActionResult ForwardRequest(string comp = null)
        {
            HttpRequestBase request = RequestFromContext(HttpContextFactory.Current);
            Uri forwardUri = ControllerOperations.ForwardUriToNamespace(request);
            return Redirect(forwardUri);
        }

        [HttpPut]
        public async Task<HttpResponseMessage> SetBlobServiceProperties()
        {
            await Task.Delay(10);
            return new HttpResponseMessage();
        }
    }
}