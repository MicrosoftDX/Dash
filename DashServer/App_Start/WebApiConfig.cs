//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Cors;

namespace Microsoft.Dash.Server
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "BlobsAndContainers",
                routeTemplate: "{controller}/{container}/{*blob}");
            config.Routes.MapHttpRoute(
                name: "Account",
                routeTemplate: "{controller}");

            config.EnableCors(new EnableCorsAttribute("*", "*", "*"));
        }
    }
}
