//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Configuration;
using System.Linq;
using System.Web.Configuration;
using Microsoft.WindowsAzure;
using System.Reflection;
using System.Diagnostics;
using Microsoft.WindowsAzure.Diagnostics;

namespace Microsoft.Dash.Server.Utils
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
    }
}