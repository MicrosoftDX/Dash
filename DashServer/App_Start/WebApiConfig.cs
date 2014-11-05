//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

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
                name: "Blobs",
                routeTemplate: "{controller}/{container}/{*blob}");
            config.Routes.MapHttpRoute(
                name: "Containers",
                routeTemplate: "{controller}/{container}");
            config.Routes.MapHttpRoute(
                name: "Account",
                routeTemplate: "");

        }
    }
}
