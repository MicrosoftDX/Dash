using System;
using System.Collections.Generic;
using System.Configuration;
using System.IdentityModel.Tokens;
using System.Linq;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.ActiveDirectory;
using Owin;
using Microsoft.Dash.Common.Utils;

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
                    Tenant = DashConfiguration.ConfigurationSource.GetSetting("ida:Tenant", String.Empty),
                    TokenValidationParameters = new TokenValidationParameters {
                        ValidAudience = DashConfiguration.ConfigurationSource.GetSetting("ida:ClientID", String.Empty)
                    },
                });
        }
    }
}
