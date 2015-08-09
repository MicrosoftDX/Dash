//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace DashServer.ManagementAPI.Utils.Azure
{
    public class AzureServiceManagementClient : IDisposable
    {
        ComputeManagementClient _realClient;

        public AzureServiceManagementClient(ComputeManagementClient realClient, string serviceName)
        {
            _realClient = realClient;
            this.ServiceName = serviceName;
        }

        public void Dispose()
        {
            if (_realClient != null)
            {
                _realClient.Dispose();
                _realClient = null;
            }
        }

        public string ServiceName { get; private set; }

        public async Task<OperationResponse> UpdateService(HostedServiceUpdateParameters updateParameters)
        {
            return await _realClient.HostedServices.UpdateAsync(this.ServiceName, updateParameters);
        }

        public async Task<OperationStatusResponse> CreateDeployment(DeploymentCreateParameters createParameters, DeploymentSlot slot = DeploymentSlot.Production)
        {
            return await _realClient.Deployments.CreateAsync(this.ServiceName, slot, createParameters);
        }

        public async Task<OperationResponse> UpgradeDeployment(DeploymentUpgradeParameters upgradeParameters, DeploymentSlot slot = DeploymentSlot.Production)
        {
            return await _realClient.Deployments.BeginUpgradingBySlotAsync(this.ServiceName, slot, upgradeParameters);
        }

        public async Task<OperationStatusResponse> GetOperationStatus(string requestId)
        {
            return await _realClient.GetOperationStatusAsync(requestId);
        }

        public async Task<XDocument> GetDeploymentConfiguration(DeploymentSlot slot = DeploymentSlot.Production)
        {
            var deployment = await _realClient.Deployments.GetBySlotAsync(this.ServiceName, slot);
            return XDocument.Parse(deployment.Configuration);
        }
    }
}