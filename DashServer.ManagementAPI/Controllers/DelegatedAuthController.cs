//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Dash.Common.Authentication;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Dash.Common.Platform;
using System.Collections.Generic;
using Microsoft.Dash.Common.Platform.Payloads;

namespace DashServer.ManagementAPI.Controllers
{
    public class DelegatedAuthController : ApiController
    {
        protected async Task<IHttpActionResult> DoActionAsync(string operation, Func<AzureServiceManagementClient, Task<IHttpActionResult>> action)
        {
            return await DoActionAsync(operation, async () =>
            {
                using (var serviceClient = await AzureService.GetServiceManagementClient((await GetRdfeToken()).AccessToken))
                {
                    return await action(serviceClient);
                }
            });
        }

        protected async Task<IHttpActionResult> DoActionAsync(string operation, Func<Task<IHttpActionResult>> action)
        {
            var accessToken = await GetRdfeToken();
            if (accessToken == null)
            {
                return StatusCode(HttpStatusCode.Forbidden);
            }
            return await OperationRunner.DoActionAsync(operation, async () =>
            {
                return await action();
            });
        }

        protected async Task<AuthenticationResult> GetRdfeToken()
        {
            return await DelegationToken.GetRdfeToken(this.Request.Headers.Authorization.ToString());
        }

        protected async Task EnqueueServiceOperationUpdate(AzureServiceManagementClient serviceClient, string operationId, string refreshToken = null)
        {
            await EnqueueServiceOperationUpdate(serviceClient.SubscriptionId, serviceClient.ServiceName, operationId, refreshToken);
        }

        protected async Task EnqueueServiceOperationUpdate(string subscriptionId, string serviceName, string operationId, string refreshToken = null)
        {
            if (String.IsNullOrWhiteSpace(refreshToken))
            {
                var authToken = await GetRdfeToken();
                refreshToken = authToken.RefreshToken;
            }
            await new AzureMessageQueue().EnqueueAsync(new QueueMessage(MessageTypes.ServiceOperationUpdate,
                new Dictionary<string, string>
                {
                    { UpdateServicePayload.OperationId, operationId },
                    { UpdateServicePayload.SubscriptionId, subscriptionId },
                    { UpdateServicePayload.ServiceName, serviceName },
                    { UpdateServicePayload.RefreshToken, refreshToken },
                },
                DashTrace.CorrelationId));
        }
    }
}