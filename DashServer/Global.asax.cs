//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using System.Threading.Tasks;

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
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }

        async Task AuthorizeRequestAsync(Object sender, EventArgs e)
        {
            if (!await RequestAuthorization.IsRequestAuthorizedAsync(DashHttpRequestWrapper.Create(this.Request)))
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
            var result = await StorageOperationsHandler.HandlePrePileOperationAsync(DashHttpRequestWrapper.Create(this.Request));
            if (result != null)
            {
                switch (result.StatusCode)
                {
                    case HttpStatusCode.Redirect:
                        this.Response.Redirect(result.Location, false);
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
