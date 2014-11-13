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

namespace Microsoft.Dash.Server
{
    public class WebApiApplication : System.Web.HttpApplication
    {

        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }

        void Application_AuthorizeRequest()
        {
            if (!RequestAuthorization.IsRequestAuthorized(new RequestAuthorization.HttpRequestWrapper(this.Request)))
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

        void Application_PreRequestHandlerExecute()
        {
            // Insert handling here for any requests which can potentially contain a body. We must process the request here
            // because if the client is using the Expect: 100-Continue header, then we should issue our final (redirect) status
            // BEFORE IIS sends the 100 Continue response. This way the blob content is never sent to us.
            if (this.Request.HttpMethod == HttpMethod.Put.Method)
            {
                string redirectUri = String.Empty;
                var requestUriParts = RequestUriParts.Create(this.Request.Url);
                if (requestUriParts.IsAccountRequest)
                {
                    // TODO: Take appropriate pre-body action for account PUT operations
                }
                else if (requestUriParts.IsContainerRequest)
                {
                    // TODO: Take appropriate pre-body action for container PUT operations
                }
                else if (requestUriParts.IsBlobRequest)
                { 
                    switch (StorageOperations.GetBlobOperationFromCompParam(this.Request.QueryString["comp"], this.Request.GetHeaders()))
                    {
                        case StorageOperationTypes.GetPutBlob:
                            // TODO: Insert call to common function to lookup blob in namespace account & generate redirect SAS URI
                            redirectUri = "http://dashstorage2.blob.core.windows.net/test/test.txt?sv=2014-02-14&sr=c&sig=AGw1j7kMvb41HuXZo6TX2Z%2BpJntlMqWfhmU6cw491zU%3D&se=2014-10-29T05%3A24%3A30Z&sp=rwdl";
                            break;
                    }
                }
                else
                {
                    // Incorrectly formed URI
                    System.Diagnostics.Debug.Assert(false);
                }
                if (!String.IsNullOrWhiteSpace(redirectUri))
                {
                    this.Response.Redirect(redirectUri, false);
                    this.CompleteRequest();
                }
            }
        }
    }
}
