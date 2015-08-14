//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Common.Authentication;
using Microsoft.Dash.Common.OperationStatus;
using Microsoft.Dash.Common.ServiceManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Processors
{
    public static class ServiceUpdater
    {

        public static async Task<bool> UpdateOperationStatus(string subscriptionId, string serviceName, string operationId, string refreshToken)
        {
            var accessToken = await DelegationToken.GetAccessTokenFromRefreshToken(refreshToken);
            if (accessToken != null)
            {
                return await UpdateOperationStatus(new AzureServiceManagementClient(subscriptionId, serviceName, accessToken.AccessToken),
                    await UpdateConfigStatus.GetConfigUpdateStatus(operationId));
            }
            return false;
        }

        public static async Task<bool> UpdateOperationStatus(AzureServiceManagementClient serviceClient, UpdateConfigStatus.ConfigUpdate operationStatus)
        {
            var response = await serviceClient.GetOperationStatus(operationStatus.CloudServiceUpdateOperationId);
            switch (response.Status)
            {
                case Microsoft.WindowsAzure.OperationStatus.InProgress:
                    break;

                case Microsoft.WindowsAzure.OperationStatus.Failed:
                    if (operationStatus.EndTime == DateTime.MinValue)
                    {
                        operationStatus.EndTime = DateTime.UtcNow;
                    }
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.Failed, String.Format("Azure service managment failure. {0}:{1}", response.Error.Code, response.Error.Message));
                    break;

                case Microsoft.WindowsAzure.OperationStatus.Succeeded:
                    if (operationStatus.EndTime == DateTime.MinValue)
                    {
                        operationStatus.EndTime = DateTime.UtcNow;
                    }
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.Completed, "Operation completed successfully");
                    break;
            }
            return response.Status == Microsoft.WindowsAzure.OperationStatus.InProgress;
        }
    }
}
