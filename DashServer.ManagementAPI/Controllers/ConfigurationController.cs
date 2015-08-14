//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;
using DashServer.ManagementAPI.Models;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.OperationStatus;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Platform.Payloads;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;

namespace DashServer.ManagementAPI.Controllers
{
    [Authorize]
    public class ConfigurationController : DelegatedAuthController
    {
        [HttpGet, ActionName("Index")]
        public async Task<IHttpActionResult> GetCurrentConfiguration()
        {
            return await DoActionAsync("ConfigurationController.GetCurrentConfiguration", async (serviceClient) =>
            {
                // Load the XML config from RDFE
                // Get only Dash storage related settings to show and make a dictionary
                // which we can use to return the properties dynamic
                var settings = AzureServiceConfiguration.GetSettingsProjected(await serviceClient.GetDeploymentConfiguration())
                    .ToList();
                return Ok(new Configuration
                    {
                        AccountSettings = settings
                                            .Where(AzureServiceConfiguration.SettingPredicateSpecialName)
                                            .ToDictionary(elem => elem.Item1, elem => elem.Item2),
                        ScaleAccounts = new ScaleAccounts
                            {
                                MaxAccounts = 16,
                                Accounts = settings
                                            .Where(AzureServiceConfiguration.SettingPredicateScaleoutStorage)
                                            .Select(setting => ParseConnectionString(setting.Item2))
                                            .Where(account => account != null)
                                            .ToList(),
                            },
                        GeneralSettings = settings
                                            .Where(elem => !AzureServiceConfiguration.SettingPredicateSpecialName(elem) && 
                                                           !AzureServiceConfiguration.SettingPredicateScaleoutStorage(elem) && 
                                                           !AzureServiceConfiguration.SettingPredicateRdp(elem))
                                            .ToDictionary(elem => elem.Item1, elem => elem.Item2),
                    });
            });
        }

        [HttpPut, ActionName("Index")]
        public async Task<IHttpActionResult> UpdateConfiguration(Configuration newConfig)
        {
            return await DoActionAsync("ConfigurationController.UpdateConfiguration", async (serviceClient) =>
            {
                // We have 2 modes for update:
                //  - Immediate - there's no actions on storage accounts and so we're only updating the config - we can do that from here
                //  - Long running - if we need to import accounts into the virtual account - we post a message to our async queue so that
                //                   we are resiliant to failure - we could get really screwed up if we partially import accounts and then
                //                   fail and don't attempt to recover. The queue gives us that recovery.
                // For both modes, we will return the updated config with a request id. The client can call the /Status action with that
                // request id to determine progress (progress information is stored in a Table in the namespace account).
                var operationId = DashTrace.CorrelationId.ToString();
                // Reconcile storage accounts - any new accounts (indicated by a blank key) we will create immediately & include the key in the returned config
                var newAccounts = new[] { 
                        ParseConnectionString(newConfig.GeneralSettings, DashConfiguration.KeyNamespaceAccount),
                        ParseConnectionString(newConfig.GeneralSettings, DashConfiguration.KeyDiagnosticsAccount)
                    }
                    .Concat(newConfig.ScaleAccounts.Accounts
                        .Select(account => Tuple.Create(account, String.Empty)))
                    .Where(account => account != null && String.IsNullOrWhiteSpace(account.Item1.AccountKey))
                    .ToList();
                // Start our operation log - use the existing namespace account, unless there isn't one
                var namespaceAccount = DashConfiguration.NamespaceAccount;
                if (namespaceAccount == null)
                {
                    // See if they've specified an account in the new config
                    var toBeCreatedNamespace = newAccounts
                        .FirstOrDefault(account => String.Equals(account.Item2, DashConfiguration.KeyNamespaceAccount, StringComparison.OrdinalIgnoreCase));
                    if (toBeCreatedNamespace == null)
                    {
                        CloudStorageAccount.TryParse(newConfig.GeneralSettings[DashConfiguration.KeyNamespaceAccount], out namespaceAccount);
                    }
                }
                var operationStatus = await UpdateConfigStatus.GetConfigUpdateStatus(operationId, namespaceAccount);
                await operationStatus.UpdateStatus(UpdateConfigStatus.States.NotStarted, "Begin service update process. OperationId: [{0}]", operationId);
                operationStatus.AccountsToBeCreated = newAccounts
                    .Select(account => account.Item1.AccountName)
                    .ToList();
                if (newAccounts.Any())
                {
                    string location = await serviceClient.GetServiceLocation();
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.CreatingAccounts, "Creating new storage accounts: [{0}]", String.Join(", ", operationStatus.AccountsToBeCreated));
                    foreach (var accountToCreate in newAccounts)
                    {
                        DashTrace.TraceInformation("Creating storage account: [{0}]", accountToCreate.Item1.AccountName);
                        accountToCreate.Item1.AccountKey = await serviceClient.CreateStorageAccount(accountToCreate.Item1.AccountName, location);
                        // Update the config
                        if (!String.IsNullOrWhiteSpace(accountToCreate.Item2))
                        {
                            newConfig.GeneralSettings[accountToCreate.Item2] = GenerateConnectionString(accountToCreate.Item1);
                        }
                    }
                    // If we didn't previously have a namespace account, wire it up now
                    if (namespaceAccount == null)
                    {
                        if (CloudStorageAccount.TryParse(newConfig.GeneralSettings[DashConfiguration.KeyNamespaceAccount], out namespaceAccount))
                        {
                            operationStatus.StatusHandler.UpdateCloudStorageAccount(namespaceAccount);
                        }
                    }
                    operationStatus.AccountsToBeCreated.Clear();
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.CreatingAccounts, "Creation of new storage accounts completed");
                }
                // Work out the list of accounts to import
                var newConfigSettings = newConfig.AccountSettings
                    .Select(setting => Tuple.Create(setting.Key, setting.Value))
                    .Concat(newConfig.GeneralSettings
                        .Select(setting => Tuple.Create(setting.Key, setting.Value)))
                    .Concat(newConfig.ScaleAccounts.Accounts
                        .Select((account, index) => Tuple.Create(String.Format("{0}{1}", DashConfiguration.KeyScaleoutAccountPrefix, index), GenerateConnectionString(account))))
                    .ToDictionary(setting => setting.Item1, setting => setting.Item2, StringComparer.OrdinalIgnoreCase);
                var serviceSettings = await serviceClient.GetDeploymentConfiguration();
                var scaleoutAccounts = AzureServiceConfiguration.GetSettingsProjected(serviceSettings)
                    .Where(AzureServiceConfiguration.SettingPredicateScaleoutStorage)
                    .Select(account => ParseConnectionString(account.Item2))
                    .Where(account => account != null)
                    .ToDictionary(account => account.AccountName, StringComparer.OrdinalIgnoreCase);
                var importAccounts = newConfig.ScaleAccounts.Accounts
                    .Where(newAccount => !scaleoutAccounts.ContainsKey(newAccount.AccountName))
                    .ToList();
                if (importAccounts.Any())
                {
                    // We have accounts to import - post the message to the async queue & let it process the remainder
                    var rdfeAccessToken = await GetRdfeToken();
                    operationStatus.AccountsToBeImported = importAccounts
                        .Select(account => account.AccountName)
                        .ToList();
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.ImportingAccounts, "Waiting to begin importing of new storage accounts");
                    var message = new QueueMessage(MessageTypes.UpdateService, 
                        new Dictionary<string, string>
                        {
                            { UpdateServicePayload.OperationId, operationId },
                            { UpdateServicePayload.SubscriptionId, serviceClient.SubscriptionId },
                            { UpdateServicePayload.ServiceName, serviceClient.ServiceName },
                            { UpdateServicePayload.RefreshToken, rdfeAccessToken.RefreshToken },
                        },
                        DashTrace.CorrelationId);
                    var messageWrapper = new UpdateServicePayload(message);
                    messageWrapper.ImportAccounts = importAccounts
                        .Select(account => GenerateConnectionString(account));
                    messageWrapper.Settings = newConfigSettings;
                    await new AzureMessageQueue().EnqueueAsync(message, 0);
                }
                else
                {
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.UpdatingService, "Updating service configuration");
                    // No accounts to import - move directly to the service config update
                    var response = await serviceClient.ChangeDeploymentConfiguration(AzureServiceConfiguration.ApplySettings(serviceSettings, newConfigSettings));
                    operationStatus.CloudServiceUpdateOperationId = response.RequestId;
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.UpdatingService, "Cloud service configuration update in progress");
                    await EnqueueServiceOperationUpdate(serviceClient, operationId);
                }
                newConfig.OperationId = operationId;
                return Content(HttpStatusCode.Accepted, newConfig);
            });
        }

        [HttpGet, ActionName("")]

        [HttpGet, ActionName("validate")]
        public async Task<IHttpActionResult> ValidateStorage(string storageAccountName, string storageAccountKey = null)
        {
            return await DoActionAsync("ConfigurationController.ValidateStorage", async (serviceClient) =>
            {
                var result = new StorageValidation();
                if (!String.IsNullOrWhiteSpace(storageAccountKey))
                {
                    var response = await serviceClient.ValidateStorageAccount(storageAccountName, storageAccountKey);
                    result.NewStorageNameValid = response.NewStorageNameValid;
                    result.ExistingStorageNameValid = response.ExistingStorageNameValid;
                    result.StorageKeyValid = response.StorageKeyValid;
                }
                else
                {
                    result.NewStorageNameValid = await serviceClient.ValidateStorageAccountName(storageAccountName);
                    result.StorageKeyValid = true;
                }

                return Ok(result);
            });
        }

        static Tuple<ScaleAccount, string> ParseConnectionString(IDictionary<string, string> settings, string settingKey)
        {
            string connectionString;
            if (settings.TryGetValue(settingKey, out connectionString))
            {
                return Tuple.Create(ParseConnectionString(connectionString), settingKey);
            }
            return null;
        }

        static ScaleAccount ParseConnectionString(string connectionString)
        {
            CloudStorageAccount parsedAccount;
            if (CloudStorageAccount.TryParse(connectionString, out parsedAccount))
            {
                return new ScaleAccount
                {
                    AccountName = parsedAccount.Credentials.AccountName,
                    AccountKey = parsedAccount.Credentials.ExportBase64EncodedKey(),
                    UseTls = parsedAccount.BlobEndpoint.Scheme == Uri.UriSchemeHttps,
                };
            }
            return null;
        }

        static string GenerateConnectionString(ScaleAccount account)
        {
            return String.Format("DefaultEndpointsProtocol={0};AccountName={1};AccountKey={2}",
                account.UseTls ? "https" : "http",
                account.AccountName,
                account.AccountKey);
        }
    }
}
