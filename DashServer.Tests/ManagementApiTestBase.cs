//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dash.Common.OperationStatus;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.Dash.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Moq;
using Moq.Protected;

namespace Microsoft.Tests
{
    public class ManagementApiTestBase : DashTestBase
    {
        protected class ManagementApiTestContext : DashTestContext
        {
            public MockAzureService ServiceFactory { get; set; }
            public Mock<AzureServiceManagementClient> DefaultServiceMock { get; set; }
        }

        protected static ManagementApiTestContext SetupTestClass<T>(Mock<T> controllerMock, IDictionary<string, string> defaultSettings, TestContext ctx) where T : class
        {
            var retval = (ManagementApiTestContext)InitializeConfig(ctx, "datax1", new Dictionary<string, string>
                {
                    { DashConfiguration.KeyWorkerQueueName, Guid.NewGuid().ToString("N") },
                    { "LogNormalOperations", "true" }
                }, 
                "", 
                () => new ManagementApiTestContext());
            AzureService.ServiceFactory = retval.ServiceFactory = new MockAzureService();
            UpdateConfigStatus.TableName = "test" + Guid.NewGuid().ToString("N");

            // Fixup the supplied settings with configuration read from config file
            var secretsConfig = _testConfig.Configurations["datax3"];
            defaultSettings[DashConfiguration.KeyNamespaceAccount] = secretsConfig.NamespaceConnectionString;
            for (int index = 0; index < secretsConfig.DataConnectionStrings.Count(); index++)
            {
                defaultSettings[DashConfiguration.KeyScaleoutAccountPrefix + index.ToString()] = secretsConfig.DataConnectionStrings.ElementAt(index);
            }

            retval.DefaultServiceMock = new Mock<AzureServiceManagementClient>();
            retval.DefaultServiceMock.Setup(service => service.GetDeploymentConfiguration(DeploymentSlot.Production))
                .Returns(() => Task.FromResult(MockAzureService.GetServiceConfiguration(defaultSettings)));

            controllerMock.CallBase = true;
            controllerMock.Protected()
                .Setup<Task<string>>("GetRdfeAccessToken")
                .Returns(Task.FromResult(String.Empty));
            controllerMock.Protected()
                .Setup<Task<string>>("GetRdfeRefreshToken")
                .Returns(Task.FromResult(String.Empty));

            return retval;
        }

        protected static void Cleanup(ManagementApiTestContext ctx)
        {
            DashConfiguration.NamespaceAccount.CreateCloudTableClient().GetTableReference(UpdateConfigStatus.TableName).DeleteIfExists();
            CleanupTestBlobs(ctx);
        }

    }
}
