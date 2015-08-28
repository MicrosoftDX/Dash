//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Dash.Common.Authentication;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Platform.Payloads;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;

namespace DashServer.ManagementAPI.Controllers
{
    public class DelegatedAuthController : ApiController
    {
        protected async Task<IHttpActionResult> DoActionAsync(string operation, Func<AzureServiceManagementClient, Task<IHttpActionResult>> action)
        {
            return await OperationRunner.DoActionAsync(operation, async () =>
            {
                using (var serviceClient = await AzureService.GetServiceManagementClient(async () => await GetRdfeAccessToken()))
                {
                    if (serviceClient == null)
                    {
                        return StatusCode(HttpStatusCode.Forbidden);
                    }
                    return await action(serviceClient);
                }
            });
        }

        protected async Task<IHttpActionResult> DoActionAsync(string operation, Func<Task<IHttpActionResult>> action)
        {
            return await OperationRunner.DoActionAsync(operation, async () =>
            {
                return await action();
            });
        }

        private async Task<AuthenticationResult> GetRdfeTokenInternal()
        {
            return await DelegationToken.GetRdfeToken(this.Request.Headers.Authorization.ToString());
        }

        private async Task<string> GetRdfeTokenPart(Func<AuthenticationResult, string> partSelector)
        {
            var result = await GetRdfeTokenInternal();
            if (result != null)
            {
                return partSelector(result);
            }
            return String.Empty;
        }

        protected virtual async Task<string> GetRdfeAccessToken()
        {
            return await GetRdfeTokenPart(result => result.AccessToken);
        }

        protected virtual async Task<string> GetRdfeRefreshToken()
        {
            return await GetRdfeTokenPart(result => result.AccessToken);
        }

        protected async Task EnqueueServiceOperationUpdate(AzureServiceManagementClient serviceClient, string operationId, string refreshToken = null, CloudStorageAccount namespaceAccount = null, string asyncQueueName = null)
        {
            await EnqueueServiceOperationUpdate(serviceClient.SubscriptionId, serviceClient.ServiceName, operationId, refreshToken, namespaceAccount, asyncQueueName);
        }

        protected async Task EnqueueServiceOperationUpdate(string subscriptionId, string serviceName, string operationId, string refreshToken = null, CloudStorageAccount namespaceAccount = null, string asyncQueueName = null)
        {
            if (String.IsNullOrWhiteSpace(refreshToken))
            {
                refreshToken = await GetRdfeRefreshToken();
            }
            await new AzureMessageQueue(namespaceAccount, asyncQueueName).EnqueueAsync(new QueueMessage(MessageTypes.ServiceOperationUpdate,
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