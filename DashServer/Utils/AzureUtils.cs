//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Configuration;
using System.Linq;
using System.Web.Configuration;
using Microsoft.WindowsAzure;

namespace Microsoft.Dash.Server.Utils
{
    // Factored out to allow test mocking
    public interface IConfigurationProvider
    {
        string GetSetting(string name);
    }

    public static class AzureUtils
    {
        static public IConfigurationProvider ConfigProvider = new AzureConfigProvider();

        public static T GetConfigSetting<T>(string settingName, T defaultValue)
        {
            string configValue = ConfigProvider.GetSetting(settingName);
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

        class AzureConfigProvider : IConfigurationProvider
        {
            public string GetSetting(string name)
            {
 	            return CloudConfigurationManager.GetSetting(name);
            }
        }
    }
}