using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Subscriptions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Management.Models;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace DashServer.ManagementAPI.Utils.Azure
{
    public class AzureService
    {
        public static async Task<ServiceInformation> GetServiceInformation(string bearerToken)
        {
            try
            {
                if (AzureUtils.IsRunningInAzureWebRole())
                {
                    return await GetAzureServiceInformation(bearerToken);
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
    }
}