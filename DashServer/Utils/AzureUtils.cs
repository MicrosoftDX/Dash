//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Configuration;
using System.Linq;
using System.Web.Configuration;
using Microsoft.WindowsAzure;

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
    }
}