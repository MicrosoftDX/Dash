//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Web.Http;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;

namespace DashServer.ManagementAPI
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AzureUtils.AddAzureDiagnosticsListener();
            DashTrace.TraceInformation("Starting application instance");
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
