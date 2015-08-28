//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.Dash.Common.Update;
using Moq;

namespace Microsoft.Tests
{
    public class MockAzureService : IAzureServiceFactory
    {
        object _sentry = new object();
        Mock<AzureServiceManagementClient> _mockManagementClient;

        public IDisposable LockServiceConfiguration(Mock<AzureServiceManagementClient> mockManagementClient)
        {
            Monitor.Enter(_sentry);
            _mockManagementClient = mockManagementClient;
            return new LockResource(_sentry);
        }

        public static XDocument GetServiceConfiguration(IDictionary<string, string> settings)
        {
            XNamespace ns = "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration";
            var settingsElements = settings
                .Select(setting => new XElement(ns + "Setting",
                    new XAttribute("name", setting.Key),
                    new XAttribute("value", setting.Value)));
            return new XDocument(
                new XDeclaration("1.0", "utf8", "yes"),
                new XElement(ns + "ServiceConfiguration",
                    new XAttribute("serviceName", "DashServer.Azure"),
                    new XAttribute("osFamily", "4"),
                    new XAttribute("osVersion", "*"),
                    new XAttribute("schemaVersion", "2014-06.2.4"),
                    new XElement(ns + "Role",
                        new XAttribute("name", "DashServer"),
                        new XElement(ns + "Instances",
                            new XAttribute("count", "6")),
                        new XElement(ns + "ConfigurationSettings",
                            settingsElements))));
        }

        public async Task<AzureServiceManagementClient> GetServiceManagementClient(string subscriptionId, string serviceName, Func<Task<string>> bearerTokenFactory)
        {
            return await Task.FromResult(_mockManagementClient.Object);
        }

        public UpdateClient.PackageFlavors GetServiceFlavor()
        {
            return UpdateClient.PackageFlavors.Http;
        }

        private class LockResource : IDisposable
        {
            object _sentry;

            public LockResource(object sentry)
            {
                _sentry = sentry;
            }
            
            public void Dispose()
            {
                Monitor.Exit(_sentry);
            }
        }
    }
}
