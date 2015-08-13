//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Management.Storage;
using DashServer.ManagementAPI.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Management.Storage.Models;
using Microsoft.Dash.Common.Diagnostics;

namespace DashServer.ManagementAPI.Utils.Azure
{
    public class AzureServiceManagementClient : IDisposable
    {
        Lazy<ComputeManagementClient> _computeClient;
        Lazy<StorageManagementClient> _storageClient;

        public AzureServiceManagementClient(string subscriptionId, string serviceName, string bearerToken)
        {
            this.SubscriptionId = subscriptionId;
            this.ServiceName = serviceName;
            this.RdfeBearerToken = bearerToken;
            InitializeClients();
        }

        public void Dispose()
        {
            if (_computeClient.IsValueCreated)
            {
                _computeClient.Value.Dispose();
            }
            if (_storageClient.IsValueCreated)
            {
                _storageClient.Value.Dispose();
            }
            InitializeClients();
        }

        void InitializeClients()
        {
            var rdfeToken = new TokenCloudCredentials(this.SubscriptionId, this.RdfeBearerToken);
            _computeClient = new Lazy<ComputeManagementClient>(() => new ComputeManagementClient(rdfeToken));
            _storageClient = new Lazy<StorageManagementClient>(() => new StorageManagementClient(rdfeToken));
        }

        public string ServiceName { get; private set; }
        private string SubscriptionId { get; set; }
        private string RdfeBearerToken { get; set; }

        public async Task<OperationResponse> UpdateService(HostedServiceUpdateParameters updateParameters)
        {
            return await _computeClient.Value.HostedServices.UpdateAsync(this.ServiceName, updateParameters);
        }

        public async Task<OperationStatusResponse> CreateDeployment(DeploymentCreateParameters createParameters, DeploymentSlot slot = DeploymentSlot.Production)
        {
            return await _computeClient.Value.Deployments.CreateAsync(this.ServiceName, slot, createParameters);
        }

        public async Task<OperationResponse> UpgradeDeployment(DeploymentUpgradeParameters upgradeParameters, DeploymentSlot slot = DeploymentSlot.Production)
        {
            return await _computeClient.Value.Deployments.BeginUpgradingBySlotAsync(this.ServiceName, slot, upgradeParameters);
        }

        public async Task<OperationResponse> UpgradeDeployment(DeploymentChangeConfigurationParameters changeConfigParameters, DeploymentSlot slot = DeploymentSlot.Production)
        {
            return await _computeClient.Value.Deployments.BeginChangingConfigurationBySlotAsync(this.ServiceName, slot, changeConfigParameters);
        }

        public async Task<string> GetServiceLocation()
        {
            var response = await _computeClient.Value.HostedServices.GetAsync(this.ServiceName);
            return  response.Properties.Location;
        }

        public async Task<OperationStatusResponse> GetOperationStatus(string requestId)
        {
            return await _computeClient.Value.GetOperationStatusAsync(requestId);
        }

        public async Task<XDocument> GetDeploymentConfiguration(DeploymentSlot slot = DeploymentSlot.Production)
        {
            var deployment = await _computeClient.Value.Deployments.GetBySlotAsync(this.ServiceName, slot);
            return XDocument.Parse(deployment.Configuration);
        }

        public async Task<string> CreateStorageAccount(string accountName, string location)
        {
            try
            {
                var response = await _storageClient.Value.StorageAccounts.CreateAsync(new StorageAccountCreateParameters
                {
                    Name = accountName,
                    Location = location,
                    AccountType = "Standard_LRS",
                });
                var keysResponse = await _storageClient.Value.StorageAccounts.GetKeysAsync(accountName);
                return keysResponse.PrimaryKey;
            }
            catch (CloudException ex)
            {
                DashTrace.TraceWarning("Failed to create new storage account [{0}]. Details: {1}", accountName, ex);
                return String.Empty;
            }
        }

        public async Task<bool> ValidateStorageAccountName(string storageAccountName)
        {
            var response = await _storageClient.Value.StorageAccounts.CheckNameAvailabilityAsync(storageAccountName);
            return response.IsAvailable;
        }

        public async Task ValidateStorageAccount(string storageAccountName, string storageAccountKey, StorageValidation results)
        {
            try
            {
                // We don't use the management API here because the storage account credentials may be for a subscription that this
                // use does not have management access to
                var key = Convert.FromBase64String(storageAccountKey);
                var creds = new StorageCredentials(storageAccountName, key);
                var storageAccount = new CloudStorageAccount(creds, false);
                var opts = await storageAccount.CreateCloudBlobClient().GetServicePropertiesAsync(new BlobRequestOptions 
                { 
                    RetryPolicy = new NoRetry(),
                    MaximumExecutionTime = TimeSpan.FromSeconds(5),
                }, null);
                results.ExistingStorageNameValid = true;
                results.StorageKeyValid = true;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 0)
                {
                    // Couldn't resolve the name to get the request off
                    results.ExistingStorageNameValid = false;
                    results.StorageKeyValid = true;
                }
                else
                {
                    results.ExistingStorageNameValid = true;
                    // We could check here for a Fobidden response
                    results.StorageKeyValid = false;
                }
            }
            catch (FormatException)
            {
                results.StorageKeyValid = false;
            }
        }
    }
}