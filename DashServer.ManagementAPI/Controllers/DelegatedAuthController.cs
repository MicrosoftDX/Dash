//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Dash.Async;
using Microsoft.Dash.Common.Authentication;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.OperationStatus;
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
                try
                {
                    using (var serviceClient = await AzureService.GetServiceManagementClient(async () => await GetRdfeAccessToken()))
                    {
                        if (serviceClient == null)
                        {
                            return StatusCode(HttpStatusCode.Forbidden);
                        }
                        return await action(serviceClient);
                    }
                }
                catch (AdalServiceException ex)
                {
                    if (ex.ErrorCode == "interaction_required")
                    {
                        return Unauthorized(new AuthenticationHeaderValue("Bearer", String.Format("interaction_required={0}", DelegationToken.RdfeResource)));
                    }
                    return StatusCode(HttpStatusCode.Forbidden);
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
            return await GetRdfeTokenPart(result => result.RefreshToken);
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

        protected Task ProcessOperationMessageLoop(string operationId, CloudStorageAccount namespaceAccount, string namespaceConnectString, int? messageDelay, string queueName)
        {
            // Manually fire up the async worker (so that we can supply it with the new namespace account)
            return Task.Factory.StartNew(() =>
            {
                int processed = 0, errors = 0;
                MessageProcessor.ProcessMessageLoop(ref processed,
                    ref errors,
                    (msg) =>
                    {
                        if (msg == null)
                        {
                            Thread.Sleep(5000);
                        }
                        return true;
                    },
                    () =>
                    {
                        var updateStatus = UpdateConfigStatus.GetConfigUpdateStatus(operationId, namespaceAccount).Result;
                        return updateStatus != null && !updateStatus.IsFinalized;
                    },
                    messageDelay,
                    namespaceConnectString,
                    queueName);
            });
        }
    }
}