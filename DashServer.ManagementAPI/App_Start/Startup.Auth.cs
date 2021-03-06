﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.IdentityModel.Tokens;
using Microsoft.Dash.Common.Utils;
using Microsoft.Owin.Security.ActiveDirectory;
using Owin;

namespace DashServer.ManagementAPI
{
    public partial class Startup
    {
        // For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            app.UseWindowsAzureActiveDirectoryBearerAuthentication(
                new WindowsAzureActiveDirectoryBearerAuthenticationOptions
                {
                    Tenant = DashConfiguration.Tenant,
                    TokenValidationParameters = new TokenValidationParameters {
                        ValidAudience = DashConfiguration.ClientId,
                    },
                });
        }
    }
}
