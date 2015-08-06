//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Configuration;
using System.Linq;
using System.Web.Configuration;
using Microsoft.WindowsAzure;
using System.Reflection;
using System.Diagnostics;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Subscriptions;
using Microsoft.Azure;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Management.Models;
using Microsoft.WindowsAzure.Management.Compute;

namespace Microsoft.Dash.Common.Utils
{
    public static class AzureUtils
    {
        public static T GetConfigSetting<T>(string settingName, T defaultValue)
        {
            string configValue = CloudConfigurationManager.GetSetting(settingName);
            if (!String.IsNullOrWhiteSpace(configValue))
            {
                try
                {
                    if (typeof(T).IsEnum)
                    {
                        return (T)Enum.Parse(typeof(T), configValue);
                    }
                    return (T)Convert.ChangeType(configValue, typeof(T));
                }
                catch
                {
                }
            }
            return defaultValue;
        }

        public static void SetConfigSetting<T>(string settingName, T value)
        {
            var config = WebConfigurationManager.OpenWebConfiguration("~/");
            if (config.AppSettings.Settings.AllKeys.Contains(settingName, StringComparer.OrdinalIgnoreCase))
            {
                config.AppSettings.Settings.Remove(settingName);
            }
            config.AppSettings.Settings.Add(settingName, value.ToString());
            config.Save(ConfigurationSaveMode.Modified);
        }

        public static bool IsRunningInAzureWebRole()
        {
            bool retval = false;
            try
            {
                var serviceRuntime = Assembly.Load("Microsoft.WindowsAzure.ServiceRuntime, Version=2.4.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35, processorArchitecture=MSIL");
                var roleEnvironment = serviceRuntime.GetType("Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment");
                var isAvailable = roleEnvironment.GetProperty("IsAvailable", BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.Public);
                return (bool)isAvailable.GetMethod.Invoke(null, null);
            }
            catch
            {
            }
            return retval;
        }

        public static void AddAzureDiagnosticsListener()
        {
            try
            {
                if (IsRunningInAzureWebRole())
                {
                    Trace.Listeners.Add(new DiagnosticMonitorTraceListener());
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("Failed to load diagnostic listener: " + e.Message);
            }
        }

        public class ServiceInformation
        {
            public string SubscriptionId { get; set; }
            public string ServiceName { get; set; }
        }

        public static async Task<ServiceInformation> GetServiceInformation(string bearerToken)
        {
            try
            {
                if (IsRunningInAzureWebRole())
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
                                                    .Where(sub => sub.SubscriptionStatus == WindowsAzure.Subscriptions.Models.SubscriptionStatus.Active))
                {
                    using (var cloudServiceClient = new ComputeManagementClient(new TokenCloudCredentials(subscription.SubscriptionId, bearerToken)))
                    {
                        foreach (var cloudService in (await cloudServiceClient.HostedServices.ListAsync()))
                        {
                            var deployment = await cloudServiceClient.Deployments.GetBySlotAsync(cloudService.ServiceName, WindowsAzure.Management.Compute.Models.DeploymentSlot.Production);
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