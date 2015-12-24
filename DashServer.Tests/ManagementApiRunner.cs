//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using System.Xml.Linq;
using DashServer.ManagementAPI.Controllers;
using DashServer.ManagementAPI.Models;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.OperationStatus;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Moq;

namespace Microsoft.Tests
{
    class ManagementApiRunner
    {
        public ManagementApiRunner()
        {
            this.UpdateConfigAction = (updatedConfig) => { };
            this.VerifyConfigAction = (updatedConfig, configState, ex) => { };
            this.VerifyUpgradeAction = (result, upgradeParams) => { };
            this.UpdateChecksBeforeSuccess = 1;
            this.ExpectedCompletionState = UpdateConfigStatus.States.Completed;
            this.ExpectedCompletionOperation = "Succeeded";
            this.ExpectedException = false;
            this.ExpectedAbnormalResponse = false;
        }

        public Action<DashServer.ManagementAPI.Models.Configuration> UpdateConfigAction { get; set; }
        public Action<IEnumerable<Tuple<string, string>>, UpdateConfigStatus.ConfigUpdate, Exception> VerifyConfigAction { get; set; }
        public Action<IHttpActionResult, DeploymentUpgradeParameters> VerifyUpgradeAction { get; set; }
        public Action<Mock<AzureServiceManagementClient>> CustomizeMockAction { get; set; }
        public Func<UpdateConfigStatus.ConfigUpdate, bool> ShouldContinueWait { get; set; }
        public Func<string, string> GetStorageKeys { get; set; }
        public int UpdateChecksBeforeSuccess { get; set; }
        public UpdateConfigStatus.States ExpectedCompletionState { get; set; }
        public string ExpectedCompletionOperation { get; set;}
        public IDictionary<string, string> ServiceSettings { get; set; }
        public bool ExpectedException { get; set; }
        public bool ExpectedAbnormalResponse { get; set; }
        public MockAzureService ServiceFactory { get; set; }
        public ConfigurationController ConfigController { get; set; }
        public XDocument UpdatedServiceConfig { get; private set; }
        public string ServiceOperationId { get; private set; }
        public UpdateController UpgradeController { get; set; }
        public DeploymentUpgradeParameters UpdatedUpgradeParams { get; private set; }
        public string CurrentVersion { get; set; }
        public string UpgradeVersion { get; set; }

        private IDictionary<string, int> _updateChecks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public void ExecuteUpdateConfigWaitStatus()
        {
            ExecuteUpdateConfigCore((updatedConfig) => WaitStatus(updatedConfig.OperationId));
        }

        public void ExecuteUpdateConfigWaitOperation()
        {
            ExecuteUpdateConfigCore((updatedConfig) => WaitOperation(updatedConfig.OperationId));
        }

        public void ExecuteSoftwareUpgradeWaitStatus()
        {
            ExecuteSoftwareUpgradeCore((operationId) => WaitStatus(operationId));
        }

        public void ExecuteSoftwareUpgradeWaitOperation()
        {
            ExecuteSoftwareUpgradeCore((operationId) => WaitOperation(operationId));
        }

        void ExecuteUpdateConfigCore(Func<DashServer.ManagementAPI.Models.Configuration, UpdateConfigStatus.ConfigUpdate> processResponse)
        {
            ExecuteCore(() =>
            {
                var retval = new ExecuteCoreOutputs();
                var currentConfig = this.ConfigController.GetCurrentConfiguration().Result as OkNegotiatedContentResult<DashServer.ManagementAPI.Models.Configuration>;
                Assert.IsNotNull(currentConfig);
                var updatedConfig = currentConfig.Content;
                this.UpdateConfigAction(updatedConfig);
                NegotiatedContentResult<DashServer.ManagementAPI.Models.Configuration> updateResponse = null;
                try
                {
                    updateResponse = this.ConfigController.UpdateConfiguration(updatedConfig).Result as NegotiatedContentResult<DashServer.ManagementAPI.Models.Configuration>;
                }
                catch (Exception ex)
                {
                    if (!this.ExpectedException)
                    {
                        throw;
                    }
                    retval.ThrownException = ex;
                }
                if (!this.ExpectedException && !this.ExpectedAbnormalResponse)
                {
                    Assert.IsNotNull(updateResponse);
                    Assert.AreEqual(HttpStatusCode.Accepted, updateResponse.StatusCode);
                    updatedConfig = updateResponse.Content;
                    retval.OperationStatus = processResponse(updatedConfig);
                    retval.OperationId = updatedConfig.OperationId;
                    retval.OperationCompletedNormally = retval.OperationStatus != null && retval.OperationStatus.State == UpdateConfigStatus.States.Completed;
                }
                this.VerifyConfigAction(this.UpdatedServiceConfig == null ?
                        null :
                        AzureServiceConfiguration.GetSettingsProjected(this.UpdatedServiceConfig),
                    retval.OperationStatus,
                    retval.ThrownException);
                return retval;
            });
        }

        void ExecuteSoftwareUpgradeCore(Func<string, UpdateConfigStatus.ConfigUpdate> processResponse)
        {
            ExecuteCore(() =>
            {
                var retval = new ExecuteCoreOutputs();
                IHttpActionResult response = null;
                try
                {
                    response = this.UpgradeController.Update(new UpdateVersion { Version = this.UpgradeVersion }).Result;
                }
                catch (Exception ex)
                {
                    if (!this.ExpectedException)
                    {
                        throw;
                    }
                    retval.ThrownException = ex;
                }
                if (!this.ExpectedException && !this.ExpectedAbnormalResponse)
                {
                    var contentResponse = response as NegotiatedContentResult<OperationResult>;
                    Assert.IsNotNull(contentResponse);
                    Assert.AreEqual(HttpStatusCode.Accepted, contentResponse.StatusCode);
                    retval.OperationStatus = processResponse(retval.OperationId = contentResponse.Content.OperationId);
                    retval.OperationCompletedNormally = true;
                }
                this.VerifyUpgradeAction(response, this.UpdatedUpgradeParams);
                return retval;
            });
        }

        public class ExecuteCoreOutputs
        {
            public UpdateConfigStatus.ConfigUpdate OperationStatus { get; set; }
            public string OperationId { get; set; }
            public Exception ThrownException { get; set; }
            public bool OperationCompletedNormally { get; set; }
        }

        public void ExecuteCore(Func<ExecuteCoreOutputs> doOperation)
        {
            this.ServiceOperationId = Guid.NewGuid().ToString("N");
            // Setup the mock for the service client
            var updateMock = SetupAzureClientMock();
            if (this.CustomizeMockAction != null)
            {
                this.CustomizeMockAction(updateMock);
            }
            using (var lockObj = this.ServiceFactory.LockServiceConfiguration(updateMock))
            {
                DashTrace.CorrelationId = Guid.NewGuid();
                var outputs = doOperation();
                if (this.ExpectedException)
                {
                    Assert.IsNotNull(outputs.ThrownException);
                }
                else
                {
                    Assert.IsNull(outputs.ThrownException);
                }
                if (outputs.OperationCompletedNormally)
                {
                    Assert.AreEqual(DashTrace.CorrelationId.ToString(), outputs.OperationId);
                }
            }
        }

        private Mock<AzureServiceManagementClient> SetupAzureClientMock()
        {
            var updateMock = new Mock<AzureServiceManagementClient>();
            updateMock.CallBase = true;
            updateMock.Setup(service => service.GetDeploymentConfiguration(DeploymentSlot.Production))
                .Returns(() => Task.FromResult(MockAzureService.GetServiceConfiguration(this.ServiceSettings)));
            updateMock.Setup(service => service.ChangeDeploymentConfiguration(It.IsAny<XDocument>(), DeploymentSlot.Production))
                .Callback((XDocument serviceConfiguration, DeploymentSlot slot) => this.UpdatedServiceConfig = serviceConfiguration)
                .Returns((XDocument serviceConfiguration, DeploymentSlot slot) => Task.FromResult(new OperationResponse
                {
                    RequestId = this.ServiceOperationId,
                }));
            updateMock.Setup(service => service.GetOperationStatus(It.IsAny<string>()))
                .Returns((string operationId) =>
                {
                    int numChecks = 0;
                    if (this._updateChecks.ContainsKey(operationId))
                    {
                        numChecks = ++this._updateChecks[operationId];
                    }
                    else
                    {
                        numChecks = this._updateChecks[operationId] = 0;
                    }
                    return Task.FromResult(new OperationStatusResponse
                    {
                        Id = this.ServiceOperationId,
                        Status = numChecks >= this.UpdateChecksBeforeSuccess ? WindowsAzure.OperationStatus.Succeeded : WindowsAzure.OperationStatus.InProgress,
                    });
                });
            updateMock.Setup(service => service.CreateStorageAccount(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string accountName, string location) => Task.FromResult(this.GetStorageKeys(accountName)));
            updateMock.Setup(service => service.BeginCreateStorageAccount(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(() => Task.FromResult(Guid.NewGuid().ToString("N")));
            updateMock.Setup(service => service.GetStorageAccountKey(It.IsAny<string>()))
                .Returns((string accountName) => Task.FromResult(this.GetStorageKeys(accountName)));
            updateMock.Setup(service => service.GetServiceLocation())
                .Returns(Task.FromResult("West US"));
            updateMock.Setup(service => service.UpdateService(It.IsAny<HostedServiceUpdateParameters>()))
                .Returns(() => Task.FromResult(new OperationResponse
                {
                    RequestId = this.ServiceOperationId,
                    StatusCode = HttpStatusCode.OK,
                }));
            updateMock.Setup(service => service.UpgradeDeployment(It.IsAny<DeploymentUpgradeParameters>(), DeploymentSlot.Production))
                .Callback((DeploymentUpgradeParameters upgradeParams, DeploymentSlot slot) => this.UpdatedUpgradeParams = upgradeParams)
                .Returns(() => Task.FromResult(new OperationResponse
                {
                    RequestId = this.ServiceOperationId,
                    StatusCode = HttpStatusCode.Accepted,
                }));
            return updateMock;
        }

        UpdateConfigStatus.ConfigUpdate WaitStatus(string operationId)
        {
            // Verify that the process has completed from the satus table
            var configStatus = UpdateConfigStatus.GetConfigUpdateStatus(operationId).Result;
            while (configStatus.State != UpdateConfigStatus.States.Completed &&
                configStatus.State != UpdateConfigStatus.States.Failed &&
                configStatus.State != this.ExpectedCompletionState &&
                (this.ShouldContinueWait == null || this.ShouldContinueWait(configStatus)))
            {
                Task.Delay(1000).Wait();
                configStatus = UpdateConfigStatus.GetConfigUpdateStatus(operationId).Result;
            }
            return configStatus;
        }

        UpdateConfigStatus.ConfigUpdate WaitOperation(string operationId)
        {
            var operationsController = new OperationsController();
            string currentState = String.Empty;
            DateTime expiry = DateTime.UtcNow.AddMinutes(1);
            do
            {
                Task.Delay(2500).Wait();
                var operationResponse = operationsController.Get(operationId).Result as OkNegotiatedContentResult<OperationState>;
                Assert.IsNotNull(operationResponse);
                var operationState = operationResponse.Content;
                currentState = operationState.Status;
            }
            while (currentState != "Succeeded" &&
                currentState != "Failed" &&
                currentState != this.ExpectedCompletionOperation &&
                (this.ShouldContinueWait == null || this.ShouldContinueWait(null)) &&
                DateTime.UtcNow < expiry);
            Assert.AreEqual(this.ExpectedCompletionOperation, currentState);
            return UpdateConfigStatus.GetConfigUpdateStatus(operationId).Result;
        }
    }
}
