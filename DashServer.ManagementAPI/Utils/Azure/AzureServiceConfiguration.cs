//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DashServer.ManagementAPI.Utils.Azure
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
    }
}