//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
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
    public class ManagementApiTestBase
    {
        protected static MockAzureService _serviceFactory;
        protected static Mock<AzureServiceManagementClient> _defaultServiceMock;

        protected void SetupTestClass<T>(Mock<T> controllerMock, IDictionary<string, string> defaultSettings) where T : class
        {
            WebApiTestRunner.InitializeConfig(new Dictionary<string, string>
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "WorkerQueueName", Guid.NewGuid().ToString("N") },
                    { "LogNormalOperations", "true" }
                });
            AzureService.ServiceFactory = _serviceFactory = new MockAzureService();
            UpdateConfigStatus.TableName = "test" + Guid.NewGuid().ToString("N");

            _defaultServiceMock = new Mock<AzureServiceManagementClient>();
            _defaultServiceMock.Setup(service => service.GetDeploymentConfiguration(DeploymentSlot.Production))
                .Returns(() => Task.FromResult(MockAzureService.GetServiceConfiguration(defaultSettings)));

            controllerMock.CallBase = true;
            controllerMock.Protected()
                .Setup<Task<string>>("GetRdfeAccessToken")
                .Returns(Task.FromResult(String.Empty));
            controllerMock.Protected()
                .Setup<Task<string>>("GetRdfeRefreshToken")
                .Returns(Task.FromResult(String.Empty));
        }

        [TestCleanup]
        public void Cleanup()
        {
            var queue = new AzureMessageQueue();
            queue.DeleteQueue();
            DashConfiguration.NamespaceAccount.CreateCloudTableClient().GetTableReference(UpdateConfigStatus.TableName).DeleteIfExists();
        }

    }
}
