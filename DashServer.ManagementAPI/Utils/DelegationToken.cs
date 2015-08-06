using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

namespace DashServer.ManagementAPI.Utils
{
    public class DelegationToken
    {
        const string BearerAuthType = "Bearer ";

        public static async Task<string> GetRdfeToken(string bearerToken)
        {
            return await GetDelegationToken("https://management.core.windows.net/", bearerToken);
        }

        public static async Task<string> GetDelegationToken(string resource, string bearerToken)
        {
            try
            {
                var signInUserId = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
                var tenantId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;

                var authContext = new AuthenticationContext(String.Format("https://login.windows.net/{0}", tenantId), new ADALTokenCache(signInUserId));
                var clientCredential = new ClientCredential(DashConfiguration.ConfigurationSource.GetSetting("ida:ClientID", String.Empty),
                    DashConfiguration.ConfigurationSource.GetSetting("ida:AppKey", String.Empty));
                if (bearerToken.StartsWith(BearerAuthType, StringComparison.OrdinalIgnoreCase))
                {
                    bearerToken = bearerToken.Substring(BearerAuthType.Length);
                }
                var authResult = await authContext.AcquireTokenAsync(resource, clientCredential, new UserAssertion(bearerToken));
                return authResult.AccessToken;
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error attempting to retrieve delegation token for resource [{0}]. Details: {1}", resource, ex);
            }
            return String.Empty;
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