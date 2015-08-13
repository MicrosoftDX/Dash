//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace DashServer.ManagementAPI.Utils
{
    public class DelegationToken
    {
        const string BearerAuthType = "Bearer ";

        public static async Task<AuthenticationResult> GetRdfeToken(string bearerToken)
        {
            return await GetDelegationToken("https://management.core.windows.net/", bearerToken);
        }

        public static async Task<AuthenticationResult> GetDelegationToken(string resource, string bearerToken)
        {
            try
            {
                var signInUserId = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
                var tenantId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;

                var authContext = new AuthenticationContext(String.Format("https://login.windows.net/{0}", tenantId), new ADALTokenCache(signInUserId));
                var clientCredential = new ClientCredential(DashConfiguration.ClientId,
                    DashConfiguration.ConfigurationSource.GetSetting("AppKey", String.Empty));
                if (bearerToken.StartsWith(BearerAuthType, StringComparison.OrdinalIgnoreCase))
                {
                    bearerToken = bearerToken.Substring(BearerAuthType.Length);
                }
                return await authContext.AcquireTokenAsync(resource, clientCredential, new UserAssertion(bearerToken));
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error attempting to retrieve delegation token for resource [{0}]. Details: {1}", resource, ex);
            }
            return null;
        }

        class ADALTokenCache : TokenCache
        {
            static string _user;
            static byte[] _items;

            public ADALTokenCache(string userId)
            {
                this.AfterAccess = AfterAccessNotification;
                this.BeforeAccess = BeforeAccessNotification;
                this.BeforeWrite = BeforeWriteNotification;

                if (!String.Equals(userId, _user))
                {
                    _user = userId;
                    _items = null;
                }
                this.Deserialize(_items);
            }

            void AfterAccessNotification(TokenCacheNotificationArgs args)
            {
                if (this.HasStateChanged)
                {
                    _items = this.Serialize();
                    this.HasStateChanged = false;
                }
            }

            void BeforeAccessNotification(TokenCacheNotificationArgs args)
            {
                this.Deserialize(_items);
            }

            void BeforeWriteNotification(TokenCacheNotificationArgs args)
            {

            }
        }
    }
}