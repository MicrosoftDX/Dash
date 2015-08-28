//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Update;

namespace Microsoft.Dash.Common.ServiceManagement
{
    public interface IAzureServiceFactory
    {
        Task<AzureServiceManagementClient> GetServiceManagementClient(string subscriptionId, string serviceName, Func<Task<string>> bearerTokenFactory);
        UpdateClient.PackageFlavors GetServiceFlavor();
    }
}
