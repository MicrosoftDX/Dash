//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Dash.Common.ServiceManagement
{
    public static class AzureServiceConfiguration
    {
        static XNamespace _configNs = "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration";

        public static XElement GetSettings(XDocument configDoc)
        {
            return configDoc
                .Root
                .Element(_configNs + "Role")
                .Element(_configNs + "ConfigurationSettings");
        }

        public static IEnumerable<Tuple<string, string>> GetSettingsProjected(XDocument configDoc)
        {
            return GetSettings(configDoc)
                .Elements()
                .Select((elem) => Tuple.Create(elem.Attribute("name").Value, elem.Attribute("value").Value));
        }

        public static XNamespace Namespace
        {
            get { return _configNs; }
        }

        public static XAttribute GetInstances(XDocument configDoc)
        {
            return configDoc
                .Root
                .Element(_configNs + "Role")
                .Element(_configNs + "Instances")
                .Attribute("count");
        }

        public static XElement GetCertificates(XDocument configDoc)
        {
            return configDoc
                .Root
                .Element(_configNs + "Role")
                .Element(_configNs + "Certificates");
        }

        public static XElement GetSetting(XElement settings, string settingName)
        {
            return settings.Elements(_configNs + "Setting")
                .FirstOrDefault(x => String.Equals(x.Attribute("name").Value, settingName, StringComparison.OrdinalIgnoreCase));
        }

        public static XDocument ApplySettings(XDocument serviceConfig, IDictionary<string, string> newSettings)
        {
            var settingsElement = GetSettings(serviceConfig);
            foreach (var newSetting in newSettings)
            {
                var settingElement = AzureServiceConfiguration.GetSetting(settingsElement, newSetting.Key);
                if (settingElement != null)
                {
                    settingElement.SetAttributeValue("value", newSetting.Value);
                }
            }
            return serviceConfig;
        }

        // Special configuration attribute names
        static ISet<string> _specialConfigNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DashConfiguration.KeyDiagnosticsAccount,
            "AccountName",
            "AccountKey",
            "SecondaryAccountKey",
            DashConfiguration.KeyNamespaceAccount,
        };

        public static bool SettingPredicateSpecialName(Tuple<string, string> elem)
        {
            return _specialConfigNames.Contains(elem.Item1);
        }

        public static bool SettingPredicateScaleoutStorage(Tuple<string, string> elem)
        {
            return elem.Item1.StartsWith(DashConfiguration.KeyScaleoutAccountPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool SettingPredicateRdp(Tuple<string, string> elem)
        {
            return elem.Item1.StartsWith("Microsoft.WindowsAzure.Plugins.RemoteForwarder", StringComparison.OrdinalIgnoreCase) ||
                   elem.Item1.StartsWith("Microsoft.WindowsAzure.Plugins.RemoteAccess", StringComparison.OrdinalIgnoreCase) ||
                   elem.Item1.StartsWith("Microsoft.WindowsAzure.Plugins.RemoteDebugger", StringComparison.OrdinalIgnoreCase);
        }
    }
}