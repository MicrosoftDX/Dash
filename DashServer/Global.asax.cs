﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Cors;
using System.Web.Http;
using Microsoft.Dash.Server.Diagnostics;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Processors;

namespace Microsoft.Dash.Server
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        public WebApiApplication()
        {
            var asyncHandler = new EventHandlerTaskAsyncHelper(AuthorizeRequestAsync);
            AddOnAuthorizeRequestAsync(asyncHandler.BeginEventHandler, asyncHandler.EndEventHandler);

            asyncHandler = new EventHandlerTaskAsyncHelper(PreRequestHandlerExecuteAsync);
            AddOnPreRequestHandlerExecuteAsync(asyncHandler.BeginEventHandler, asyncHandler.EndEventHandler);
        }

        protected void Application_Start()
        {
            AzureUtils.AddAzureDiagnosticsListener();
            DashTrace.TraceInformation("Starting application instance");
            GlobalConfiguration.Configure(WebApiConfig.Register);
            // Import any statically configured accounts
            var importAccounts = DashConfiguration.ImportAccounts;
            if (importAccounts.Any())
            {
                Task.Factory.StartNew(() => AccountManager.ImportAccounts(importAccounts));
            }
        }

        void Application_BeginRequest()
        {
            // Set the correlation id - this can come from the request's x-ms-client-request-id header or we gen up something unique
            HttpCorrelationContext.Set(this.Request);
        }

        void Application_EndRequest()
        {
            // Release request correlation context
            HttpCorrelationContext.Reset();
        }
        
        async Task AuthorizeRequestAsync(Object sender, EventArgs e)
        {
            if (!await OperationRunner.DoActionAsync("App.AuthorizeRequestAsync",
                async () => await RequestAuthorization.IsRequestAuthorizedAsync(DashHttpRequestWrapper.Create(this.Request))))
            {
                this.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                // Details lifted directly from Storage Service auth failure responses
                this.Response.ContentType = "application/xml";
                this.Response.StatusDescription = "Server failed to authenticate the request. Make sure the value of Authorization header is formed correctly including the signature.";
                this.Response.Write(String.Format(@"
<?xml version='1.0' encoding='utf-8'?>
<Error>
  <Code>AuthenticationFailed</Code>
  <Message>Server failed to authenticate the request. Make sure the value of Authorization header is formed correctly including the signature. Time:{0:o}</Message>
</Error>", DateTime.UtcNow));
                this.CompleteRequest();
            }
        }

        async Task PreRequestHandlerExecuteAsync(Object sender, EventArgs e)
        {
            // Insert handling here for any requests which can potentially contain a body and that we intend to redirect. We must 
            // process the request here because if the client is using the Expect: 100-Continue header, then we should issue our 
            // final (redirect) status BEFORE IIS sends the 100 Continue response. This way the blob content is never sent to us.
            var requestWrapper = DashHttpRequestWrapper.Create(this.Request);
            var result = await WebOperationRunner.DoHandlerAsync("App.PreRequestHandlerExecuteAsync", 
                async () => await StorageOperationsHandler.HandlePrePipelineOperationAsync(requestWrapper));
            if (result != null)
            {
                switch (result.StatusCode)
                {
                    case HttpStatusCode.Redirect:
                        this.Response.Redirect(result.Location, false);
                        if (!String.IsNullOrWhiteSpace(result.SignedLocation))
                        {
                            this.Response.AppendHeader("x-ms-redirect-signature", result.SignedLocation);
                        }
                        if (result.Headers != null)
                        {
                            foreach (var header in result.Headers
                                .SelectMany(headerGroup => headerGroup
                                    .Select(headerItem => Tuple.Create(headerGroup.Key, headerItem))))
                            {
                                this.Response.AppendHeader(header.Item1, header.Item2);
                            }
                        }
                        // Add the CORS headers to the response
                        if (requestWrapper.Headers.Contains(CorsConstants.Origin))
                        {
                            this.Response.AppendHeader(CorsConstants.AccessControlAllowOrigin, requestWrapper.Headers.Value<string>(CorsConstants.Origin));
                        }
                        break;

                    case HttpStatusCode.NotFound:
                        this.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        this.Response.StatusDescription = "The specified blob does not exist.";
                        this.Response.ContentType = "application/xml";
                        this.Response.Write(String.Format(@"
<?xml version='1.0' encoding='utf-8'?>
<Error>
  <Code>BlobNotFound</Code>
  <Message>The specified blob does not exist.\n Time:{0:o}</Message>
</Error>", DateTime.UtcNow));
                        break;

                    default:
                        System.Diagnostics.Debug.Assert(false);
                        this.Response.StatusCode = (int)result.StatusCode;
                        break;
                }

                this.CompleteRequest();
            }
        }
    }
}
