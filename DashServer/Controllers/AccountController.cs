﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.WindowsAzure.Storage;
using System.Web;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Account")]
    public class AccountController : CommonController
    {
        [HttpGet]
        public IHttpActionResult ListContainers()
        {
            HttpRequestBase request = RequestFromContext(HttpContext.Current);
            Uri forwardUri = ForwardUriToNamespace(request);
            return ForwardRequest();
        }

        [HttpGet]
        public IHttpActionResult GetBlobServiceComp(string comp)
        {
            //Can just ignore the comp parameter as it just gets passed through with the forward Uri
            //comp can be one of either stats or properties
            return ForwardRequest(comp);
        }

        private IHttpActionResult ForwardRequest(string comp = null)
        {
            HttpRequestBase request = RequestFromContext(HttpContext.Current);
            Uri forwardUri = ForwardUriToNamespace(request);
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