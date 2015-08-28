//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Update;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Subscriptions;

namespace Microsoft.Dash.Common.ServiceManagement
{
    public class AzureService 
    {
        static IAzureServiceFactory _serviceFactory = new AzureServiceFactory();

        // Test Hook
        public static IAzureServiceFactory ServiceFactory
        {
            set { _serviceFactory = value; }
        }

        public static async Task<AzureServiceManagementClient> GetServiceManagementClient(Func<Task<string>> bearerTokenFactory)
        {
            return await _serviceFactory.GetServiceManagementClient(null, null, bearerTokenFactory);
        }

        public static async Task<AzureServiceManagementClient> GetServiceManagementClient(string subscriptionId, string serviceName, Func<Task<string>> bearerTokenFactory)
        {
            return await _serviceFactory.GetServiceManagementClient(subscriptionId, serviceName, bearerTokenFactory);
        }

        public static UpdateClient.PackageFlavors GetServiceFlavor()
        {
            return _serviceFactory.GetServiceFlavor();
        }

        private class AzureServiceFactory : IAzureServiceFactory
        {
            static string _lazyInitBearerToken = String.Empty;
            static Lazy<Task<ServiceInformation>> _serviceInformation = new Lazy<Task<ServiceInformation>>(() => GetAzureServiceInformation(_lazyInitBearerToken));

            public async Task<AzureServiceManagementClient> GetServiceManagementClient(string subscriptionId, string serviceName, Func<Task<string>> bearerTokenFactory)
            {
                _lazyInitBearerToken = await bearerTokenFactory();
                if (String.IsNullOrWhiteSpace(_lazyInitBearerToken))
                {
                    return null;
                }
                if (String.IsNullOrWhiteSpace(subscriptionId) || String.IsNullOrWhiteSpace(serviceName))
                {
                    var serviceInfo = await GetServiceInformation();
                    subscriptionId = serviceInfo.SubscriptionId;
                    serviceName = serviceInfo.ServiceName;
                }
                return new AzureServiceManagementClient(subscriptionId, serviceName, _lazyInitBearerToken);
            }

            public async Task<ServiceInformation> GetServiceInformation(Func<Task<string>> bearerTokenFactory = null)
            {
                try
                {
                    if (AzureUtils.IsRunningInAzureWebRole())
                    {
                        if (bearerTokenFactory != null)
                        {
                            _lazyInitBearerToken = await bearerTokenFactory();
                        }
                        return await _serviceInformation.Value;
                    }
                    else
                    {
                        // Dev flow - use configured values
                        return new ServiceInformation
                        {
                            SubscriptionId = DashConfiguration.ConfigurationSource.GetSetting("SubscriptionId", String.Empty),
                            ServiceName = DashConfiguration.ConfigurationSource.GetSetting("ServiceName", String.Empty),
                        };
                    }
                }
                catch (Exception ex)
                {
                    DashTrace.TraceWarning("Failed to obtain service information. Details: {0}", ex);
                }
                return null;
            }

            public UpdateClient.PackageFlavors GetServiceFlavor()
            {
                if (AzureUtils.IsRunningInAzureWebRole())
                {
                    return GetAzureServiceFlavor();
                }
                return UpdateClient.PackageFlavors.Http;
            }

            private static async Task<ServiceInformation> GetAzureServiceInformation(string bearerToken)
            {
                string deploymentId = RoleEnvironment.DeploymentId;
                using (var subscriptionClient = new SubscriptionClient(new TokenCloudCredentials(bearerToken)))
                {
                    foreach (var subscription in (await subscriptionClient.Subscriptions.ListAsync())
                                                        .Where(sub => sub.SubscriptionStatus == Microsoft.WindowsAzure.Subscriptions.Models.SubscriptionStatus.Active))
                    {
                        using (var cloudServiceClient = new ComputeManagementClient(new TokenCloudCredentials(subscription.SubscriptionId, bearerToken)))
                        {
                            foreach (var cloudService in (await cloudServiceClient.HostedServices.ListAsync()))
                            {
                                var deployment = await cloudServiceClient.Deployments.GetBySlotAsync(cloudService.ServiceName, Microsoft.WindowsAzure.Management.Compute.Models.DeploymentSlot.Production);
                                if (deployment.PrivateId == deploymentId)
                                {
                                    return new ServiceInformation
                                    {
                                        SubscriptionId = subscription.SubscriptionId,
                                        ServiceName = cloudService.ServiceName,
                                    };
                                }
                            }
                        }
                    }
                }
                DashTrace.TraceWarning("Unable to identify running service. Possible unauthorized user.");
                return null;
            }

            private static UpdateClient.PackageFlavors GetAzureServiceFlavor()
            {
                bool isHttps = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints.Any((endpoint) => String.Equals(endpoint.Value.Protocol, "https", StringComparison.OrdinalIgnoreCase));
                bool isVnet = RoleEnvironment.CurrentRoleInstance.VirtualIPGroups.Any();
                if (isHttps && isVnet)
                {
                    return UpdateClient.PackageFlavors.HttpsWithIlb;
                }
                else if (isVnet)
                {
                    return UpdateClient.PackageFlavors.HttpWithIlb;
                }
                else if (isHttps)
                {
                    return UpdateClient.PackageFlavors.Https;
                }
                return UpdateClient.PackageFlavors.Http;
            }
        }
    }
}