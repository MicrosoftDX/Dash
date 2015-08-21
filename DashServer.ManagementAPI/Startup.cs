//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Owin;
using Microsoft.Owin;

[assembly: OwinStartup(typeof(DashServer.ManagementAPI.Startup))]

namespace DashServer.ManagementAPI
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
