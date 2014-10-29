//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace Microsoft.Dash.Server
{
    public class WebApiApplication : System.Web.HttpApplication
    {

        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }

        void Application_BeginRequest()
        {
            // Insert handling here for any requests which can potentially contain a body. We must process the request here
            // because if the client is using the Expect: 100-Continue header, then we should issue our final (redirect) status
            // BEFORE IIS sends the 100 Continue response. This way the blob content is never sent to us.
            if (this.Request.HttpMethod == HttpMethod.Put.Method)
            {
                // Manually parse out container & blob names. Format is:
                //  /mvc-controller/container/blobseg1/blobseg2/.../blobsegn
                var urlSegments = this.Request.Url.Segments
                    .Select(segment => segment.Trim('/'))
                    .Where(segment => !String.IsNullOrWhiteSpace(segment))
                    .ToArray();
                if (urlSegments.Length >= 3)
                {
                    var container = urlSegments[1];
                    var blobName = String.Join("/", urlSegments.Skip(2));
                    // TODO: Insert call to common function to lookup blob in namespace account & generate redirect SAS URI
                    var redirectBlobUri = "http://dashstorage2.blob.core.windows.net/test/test.txt?sv=2014-02-14&sr=c&sig=AGw1j7kMvb41HuXZo6TX2Z%2BpJntlMqWfhmU6cw491zU%3D&se=2014-10-29T05%3A24%3A30Z&sp=rwdl";
                    if (!String.IsNullOrWhiteSpace(redirectBlobUri))
                    {
                        this.Response.Redirect(redirectBlobUri, true);
                    }
                }
            }
        }
    }
}
