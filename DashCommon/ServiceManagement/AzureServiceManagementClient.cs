//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Management.Storage;
using Microsoft.WindowsAzure.Management.Storage.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace Microsoft.Dash.Common.ServiceManagement
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
        public string SubscriptionId { get; set; }
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

        public async Task<OperationResponse> ChangeDeploymentConfiguration(DeploymentChangeConfigurationParameters changeConfigParameters, DeploymentSlot slot = DeploymentSlot.Production)
        {
            return await _computeClient.Value.Deployments.BeginChangingConfigurationBySlotAsync(this.ServiceName, slot, changeConfigParameters);
        }

        public async Task<OperationResponse> ChangeDeploymentConfiguration(XDocument serviceConfiguration, DeploymentSlot slot = DeploymentSlot.Production)
        {
            return await ChangeDeploymentConfiguration(new DeploymentChangeConfigurationParameters
            {
                Configuration = serviceConfiguration.ToString(),
                Mode = DeploymentChangeConfigurationMode.Auto,
            }, slot);
        }

        public async Task<string> GetServiceLocation()
        {
            var response = await _computeClient.Value.HostedServices.GetAsync(this.ServiceName);
            return  response.Properties.Location;
        }

        public async Task<DeploymentGetResponse> GetDeploymentStatus(DeploymentSlot slot = DeploymentSlot.Production)
        {
            return await _computeClient.Value.Deployments.GetBySlotAsync(this.ServiceName, slot);
        }

        public async Task<OperationStatusResponse> GetOperationStatus(string requestId)
        {
            // Both compute & storage request ids are valid here (they both map to the same REST API)
            return await _computeClient.Value.GetOperationStatusAsync(requestId);
        }

        public async Task<OperationStatusResponse> WaitForOperationToComplete(string requestId, CancellationToken cancelToken)
        {
            // Both compute & storage request ids are valid here (they both map to the same REST API)
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                var response = await GetOperationStatus(requestId);
                switch (response.Status)
                {
                    case WindowsAzure.OperationStatus.Failed:
                    case WindowsAzure.OperationStatus.Succeeded:
                        return response;
                }
                await Task.Delay(2500, cancelToken);
            }
        }

        public async Task<XDocument> GetDeploymentConfiguration(DeploymentSlot slot = DeploymentSlot.Production)
        {
            var deployment = await _computeClient.Value.Deployments.GetBySlotAsync(this.ServiceName, slot);
            return XDocument.Parse(deployment.Configuration);
        }

        public async Task<string> BeginCreateStorageAccount(string accountName, string location)
        {
            var response = await _storageClient.Value.StorageAccounts.BeginCreatingAsync(new StorageAccountCreateParameters
            {
                Name = accountName,
                Location = location,
                AccountType = "Standard_LRS",
            });
            return response.RequestId;
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

        public async Task<string> GetStorageAccountKey(string accountName)
        {
            var keysResponse = await _storageClient.Value.StorageAccounts.GetKeysAsync(accountName);
            return keysResponse.PrimaryKey;
        }

        public async Task<string> GetStorageAccountKey(string accountName, int retryDelay, CancellationToken cancelToken)
        {
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                try
                {
                    var key = await GetStorageAccountKey(accountName);
                    if (!String.IsNullOrWhiteSpace(key))
                    {
                        return key;
                    }
                }
                catch (CloudException ex)
                {
                    if (ex.Response.StatusCode != HttpStatusCode.NotFound)
                    {
                        throw;
                    }
                }
                await Task.Delay(retryDelay, cancelToken);
            }
        }

        public async Task<bool> ValidateStorageAccountName(string storageAccountName)
        {
            var response = await _storageClient.Value.StorageAccounts.CheckNameAvailabilityAsync(storageAccountName);
            return response.IsAvailable;
        }

        public class StorageValidation
        {
            public bool NewStorageNameValid { get; set; }
            public bool ExistingStorageNameValid { get; set; }
            public bool StorageKeyValid { get; set; }
        }

        public async Task<StorageValidation> ValidateStorageAccount(string storageAccountName, string storageAccountKey)
        {
            var retval = new StorageValidation();
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
                retval.ExistingStorageNameValid = true;
                retval.StorageKeyValid = true;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 0)
                {
                    // Couldn't resolve the name to get the request off
                    retval.StorageKeyValid = true;
                }
                else
                {
                    retval.ExistingStorageNameValid = true;
                    // We could check here for a Fobidden response
                }
            }
            catch (FormatException)
            {
            }
            return retval;
        }
    }
}