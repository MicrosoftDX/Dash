//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Web.Http;

namespace DashServer.ManagementAPI
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
