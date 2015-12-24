//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Web.Http;
using System.Web.Http.Cors;

namespace DashServer.ManagementAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.EnableCors(new EnableCorsAttribute("*", "*", "*", "www-authenticate"));
        }
    }
}
