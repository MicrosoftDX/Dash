//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Authentication;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.OperationStatus;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Common.Processors
{
    public static class ServiceUpdater
    {
        public static async Task<string> ImportAccountsAndUpdateService(string subscriptionId, 
            string serviceName, 
            string operationId, 
            string refreshToken,
            IEnumerable<string> accountsToCreate,
            IEnumerable<string> accountsToImport,
            IDictionary<string, string> serviceSettings,
            bool abandonOperation)
        {
            return await OperationRunner.DoActionAsync("ServiceUpdater.ImportAccountsAndUpdateService", async () =>
            {
                UpdateConfigStatus.ConfigUpdate operationStatus = null;
                try
                {
                    operationStatus = await UpdateConfigStatus.GetConfigUpdateStatus(operationId);
                    if (abandonOperation)
                    {
                        await operationStatus.UpdateStatus(UpdateConfigStatus.States.Failed, "Service update operation abandoned after too many failed retries.");
                        return String.Empty;
                    }
                    using (var serviceClient = await GetServiceManagementClient(subscriptionId, serviceName, refreshToken, operationStatus))
                    {
                        if (serviceClient == null)
                        {
                            return String.Empty;
                        }
                        // Any accounts pending creation are waited on here
                        if (accountsToCreate.Any())
                        {
                            await operationStatus.UpdateStatus(UpdateConfigStatus.States.CreatingAccounts, "Waiting for storage accounts to be created");
                            using (var cancelSource = new CancellationTokenSource(serviceClient.StorageAccountCreationTimeout()))
                            {
                                var createTasks = accountsToCreate
                                    .Select(requestId => serviceClient.WaitForOperationToComplete(requestId, cancelSource.Token))
                                    .ToArray();
                                var createResults = await Task.WhenAll(createTasks);
                                if (createResults.Any(result => result.Status != WindowsAzure.OperationStatus.Succeeded))
                                {
                                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.Failed, "Failed creating storage account(s).");
                                    return String.Empty;
                                }
                            }
                        }

                        // Import new data accounts
                        if (accountsToImport.Any())
                        {
                            await operationStatus.UpdateStatus(UpdateConfigStatus.States.ImportingAccounts, "Importing storage accounts into virtual account");
                            var storageAccounts = accountsToImport
                                .Select(connectionString => CloudStorageAccount.Parse(connectionString))
                                .ToList();
                            DashConfiguration.AddDataAccounts(storageAccounts);
                            // See if the namespace has changed
                            var configuredNamespace = CloudStorageAccount.Parse(serviceSettings[DashConfiguration.KeyNamespaceAccount]);
                            DashConfiguration.NamespaceAccount = configuredNamespace;
                            // Import the accounts
                            var importTasks = AccountManager.ImportAccounts(storageAccounts.Select(account => account.Credentials.AccountName));
                            await Task.WhenAll(importTasks
                                .Select(importResult => importResult.Item2)
                                .ToArray());
                            operationStatus.AccountsToBeImported.Clear();
                            operationStatus.AccountsImportedSuccess = importTasks
                                .Where(importResult => importResult.Item2.Result)
                                .Select(importResult => importResult.Item1)
                                .ToList();
                            operationStatus.AccountsImportedFailed = importTasks
                                .Where(importResult => !importResult.Item2.Result)
                                .Select(importResult => importResult.Item1)
                                .ToList();
                            if (operationStatus.AccountsImportedFailed.Any())
                            {
                                // If we failed importing any account, then stop now. If we don't update
                                // the configuration, the failed accounts will not be effective
                                await operationStatus.UpdateStatus(UpdateConfigStatus.States.Failed, "Error importing data account(s) into namespace.");
                                return String.Empty;
                            }
                        }
                        // Update the service configuration
                        await operationStatus.UpdateStatus(UpdateConfigStatus.States.PreServiceUpdate, "Updating service configuration");
                        var serviceConfig = await serviceClient.GetDeploymentConfiguration();
                        var response = await serviceClient.ChangeDeploymentConfiguration(
                            AzureServiceConfiguration.ApplySettings(serviceConfig, serviceSettings));
                        operationStatus.CloudServiceUpdateOperationId = response.RequestId;
                        await operationStatus.UpdateStatus(UpdateConfigStatus.States.UpdatingService, "Cloud service configuration update in progress");
                        return operationId;
                    }
                }
                catch (OperationCanceledException)
                {
                    if (operationStatus != null)
                    {
                        operationStatus.UpdateStatus(operationStatus.State, "Waiting to retry failed operation").Wait();
                    }
                    throw;
                }
                catch (CloudException)
                {
                    if (operationStatus != null)
                    {
                        operationStatus.UpdateStatus(operationStatus.State, "Waiting to retry failed service operation").Wait();
                    }
                    throw;
                }
            },
            ex => String.Empty, false, true);
        }

        public static async Task<bool> UpdateOperationStatus(string subscriptionId, string serviceName, string operationId, string refreshToken, bool abandonOperation)
        {
            var operationStatus = await UpdateConfigStatus.GetConfigUpdateStatus(operationId);
            switch (operationStatus.State)
            {
                case UpdateConfigStatus.States.Failed:
                case UpdateConfigStatus.States.Completed:
                    // We're already done
                    return false;
            }
            if (abandonOperation)
            {
                await operationStatus.UpdateStatus(UpdateConfigStatus.States.Failed, "Service update operation abandoned after too many failed retries.");
                return false;
            }
            using (var serviceClient = await GetServiceManagementClient(subscriptionId, serviceName, refreshToken, operationStatus))
            {
                if (serviceClient == null)
                {
                    return false;
                }
                return await UpdateOperationStatus(serviceClient, operationStatus);
            }
        }

        public static async Task<bool> UpdateOperationStatus(AzureServiceManagementClient serviceClient, UpdateConfigStatus.ConfigUpdate operationStatus)
        {
            var response = await serviceClient.GetOperationStatus(operationStatus.CloudServiceUpdateOperationId);
            switch (response.Status)
            {
                case Microsoft.WindowsAzure.OperationStatus.InProgress:
                    break;

                case Microsoft.WindowsAzure.OperationStatus.Failed:
                    if (operationStatus.EndTime == DateTime.MinValue)
                    {
                        operationStatus.EndTime = DateTime.UtcNow;
                    }
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.Failed, String.Format("Azure service managment failure. {0}:{1}", response.Error.Code, response.Error.Message));
                    break;

                case Microsoft.WindowsAzure.OperationStatus.Succeeded:
                    if (operationStatus.EndTime == DateTime.MinValue)
                    {
                        operationStatus.EndTime = DateTime.UtcNow;
                    }
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.Completed, "Operation completed successfully");
                    break;
            }
            return response.Status == Microsoft.WindowsAzure.OperationStatus.InProgress;
        }

        static async Task<AzureServiceManagementClient> GetServiceManagementClient(string subscriptionId, 
            string serviceName, string refreshToken, UpdateConfigStatus.ConfigUpdate operationStatus)
        {
            Func<Task<string>> accessTokenFactory = async () =>
            {
                var accessToken = await DelegationToken.GetAccessTokenFromRefreshToken(refreshToken);
                if (accessToken == null)
                {
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.Failed, "Failed acquiring access token to perform service management operation");
                    return String.Empty;
                }
                return accessToken.AccessToken;
            };
            return await AzureService.GetServiceManagementClient(subscriptionId, serviceName, accessTokenFactory);
        }
    }
}
