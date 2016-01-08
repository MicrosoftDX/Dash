//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using DashServer.ManagementAPI.Models;
using Microsoft.Dash.Async;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.OperationStatus;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Platform.Payloads;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.Dash.Common.Utils;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;

namespace DashServer.ManagementAPI.Controllers
{
    [Authorize]
    public class ConfigurationController : DelegatedAuthController
    {
        [HttpGet]
        public async Task<IHttpActionResult> GetCurrentConfiguration()
        {
            return await DoActionAsync("ConfigurationController.GetCurrentConfiguration", async (serviceClient) =>
            {
                // Load the XML config from RDFE
                // Get only Dash storage related settings to show and make a dictionary
                // which we can use to return the properties dynamic
                var operationStatusTask = UpdateConfigStatus.GetActiveStatus();
                var serviceConfigTask = serviceClient.GetDeploymentConfiguration();
                await Task.WhenAll(operationStatusTask, serviceConfigTask);
                string operationId = null;
                if (operationStatusTask.Result != null)
                {
                    operationId = operationStatusTask.Result.OperationId;
                }
                var settings = AzureServiceConfiguration.GetSettingsProjected(serviceConfigTask.Result)
                    .ToList();
                return Ok(new Configuration
                {
                    OperationId = operationId,
                    AccountSettings = settings
                                        .Where(AzureServiceConfiguration.SettingPredicateSpecialName)
                                        .ToDictionary(elem => elem.Item1, elem => elem.Item2),
                    ScaleAccounts = new ScaleAccounts
                    {
                        MaxAccounts = DashConfiguration.MaxDataAccounts,
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

        private class StorageAccountCreationInfo
        {
            public ScaleAccount AccountInfo { get; set; }
            public string ConfigKey { get; set; }
            public bool IsBlockingOperation { get; set; }
        }

        [HttpPut]
        public async Task<IHttpActionResult> UpdateConfiguration(Configuration newConfig)
        {
            return await DoActionAsync("ConfigurationController.UpdateConfiguration", async (serviceClient) =>
            {
                UpdateConfigStatus.ConfigUpdate operationStatus = null;
                try
                {
                    // Do some validation checks first
                    var operationStatusTask = UpdateConfigStatus.GetActiveStatus();
                    var serviceConfigTask = serviceClient.GetDeploymentConfiguration();
                    await Task.WhenAll(operationStatusTask, serviceConfigTask);
                    if (operationStatusTask.Result != null)
                    {
                        return BadRequest(String.Format("Operation: {0} is already underway. Wait for this operation to complete before attempting further updates.", operationStatusTask.Result.OperationId));
                    }
                    var serviceSettings = AzureServiceConfiguration.GetSettingsProjected(serviceConfigTask.Result);
                    if (!CompareAccountName(serviceSettings, newConfig.AccountSettings, DashConfiguration.KeyNamespaceAccount))
                    {
                        return BadRequest("Cannot change the namespace account name once is has been set.");
                    }
                    int accountIndex = 0;
                    foreach (var currentAccount in serviceSettings
                                                        .Where(AzureServiceConfiguration.SettingPredicateScaleoutStorage))
                    {
                        if (!CompareAccountName(currentAccount, newConfig.ScaleAccounts.Accounts.ElementAtOrDefault(accountIndex++)))
                        {
                            var removedAccount = ParseConnectionString(currentAccount.Item2);
                            return BadRequest(String.Format("Data account [{0]} cannot be removed.", removedAccount.AccountName));
                        }
                    }

                    // Do some preamble work here synchronously (begin creation of any new storage accounts - we need to return the keys),
                    // but then enqueue a message on our async worker queue to complete the full update in a reliable manner without having
                    // the client wait forever for a response. The response will be an operationid which can be checked on by calling
                    // the /operations/operationid endpoint.
                    var operationId = DashTrace.CorrelationId.ToString();
                    // Reconcile storage accounts - any new accounts (indicated by a blank key) we will create immediately & include the key in the returned config
                    var newAccounts = new[] { 
                            ParseConnectionString(newConfig.AccountSettings, DashConfiguration.KeyNamespaceAccount, true),
                            ParseConnectionString(newConfig.AccountSettings, DashConfiguration.KeyDiagnosticsAccount, true)
                        }
                        .Concat(newConfig.ScaleAccounts.Accounts
                            .Select(account => new StorageAccountCreationInfo
                            {
                                AccountInfo = account,
                            }))
                        .Where(account => account != null && account.AccountInfo != null && String.IsNullOrWhiteSpace(account.AccountInfo.AccountKey))
                        .ToList();
                    // Start our operation log - use the namespace account specified in the configuration
                    var namespaceAccount = DashConfiguration.NamespaceAccount;
                    // See if they've specified an account in the new config
                    var toBeCreatedNamespace = newAccounts
                        .FirstOrDefault(account => String.Equals(account.ConfigKey, DashConfiguration.KeyNamespaceAccount, StringComparison.OrdinalIgnoreCase));
                    if (toBeCreatedNamespace == null)
                    {
                        CloudStorageAccount.TryParse(newConfig.AccountSettings[DashConfiguration.KeyNamespaceAccount], out namespaceAccount);
                    }
                    operationStatus = await UpdateConfigStatus.GetConfigUpdateStatus(operationId, namespaceAccount);
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.NotStarted, "Begin service update process. OperationId: [{0}]", operationId);
                    operationStatus.AccountsToBeCreated = newAccounts
                        .Select(account => account.AccountInfo.AccountName)
                        .ToList();
                    if (newAccounts.Any())
                    {
                        await operationStatus.UpdateStatus(UpdateConfigStatus.States.CreatingAccounts, "Creating new storage accounts: [{0}]", String.Join(", ", operationStatus.AccountsToBeCreated));
                        var newAccountTasks = await CreateStorageAccounts(serviceClient, newConfig, newAccounts);
                        await Task.WhenAll(newAccountTasks);
                        // If we just created a new account for the namespace, wire it up now
                        if (CloudStorageAccount.TryParse(newConfig.AccountSettings[DashConfiguration.KeyNamespaceAccount], out namespaceAccount))
                        {
                            operationStatus.StatusHandler.UpdateCloudStorageAccount(namespaceAccount);
                        }
                        // Switch AccountsToBeCreated to the request ids that the async task can verify are completed
                        operationStatus.AccountsToBeCreated = newAccountTasks
                            .Select(newAccoutTask => newAccoutTask.Result)
                            .Where(requestId => !String.IsNullOrWhiteSpace(requestId))
                            .ToList();
                        await operationStatus.UpdateStatus(UpdateConfigStatus.States.CreatingAccounts, "Creation of new storage accounts initiated.");
                    }
                    // TODO: When we enable storage analytics (or at least a utilization report), we will need to turn on metrics for
                    // every storage account here.
                    string asyncQueueName = newConfig.GeneralSettings[DashConfiguration.KeyWorkerQueueName];
                    // Work out the list of accounts to import
                    var newConfigSettings = newConfig.AccountSettings
                        .Select(setting => Tuple.Create(setting.Key, setting.Value))
                        .Concat(newConfig.GeneralSettings
                            .Select(setting => Tuple.Create(setting.Key, setting.Value)))
                        .Concat(newConfig.ScaleAccounts.Accounts
                            .Select((account, index) => Tuple.Create(String.Format("{0}{1}", DashConfiguration.KeyScaleoutAccountPrefix, index), GenerateConnectionString(account))))
                        .ToDictionary(setting => setting.Item1, setting => setting.Item2, StringComparer.OrdinalIgnoreCase);
                    var scaleoutAccounts = serviceSettings
                        .Where(AzureServiceConfiguration.SettingPredicateScaleoutStorage)
                        .Select(account => ParseConnectionString(account.Item2))
                        .Where(account => account != null)
                        .ToDictionary(account => account.AccountName, StringComparer.OrdinalIgnoreCase);
                    var importAccounts = newConfig.ScaleAccounts.Accounts
                        .Where(newAccount => !scaleoutAccounts.ContainsKey(newAccount.AccountName))
                        .ToList();
                    // Prepare the new accounts, import accounts & service configuration information into a message for the async worker.
                    // Despite kicking the async worker off directly here, we need the message durably enqueued so that any downstream
                    // failures will be retried.
                    var rdfeAccessToken = await GetRdfeRefreshToken();
                    operationStatus.AccountsToBeImported = importAccounts
                        .Select(account => account.AccountName)
                        .ToList();
                    var message = new QueueMessage(MessageTypes.UpdateService, 
                        new Dictionary<string, string>
                        {
                            { UpdateServicePayload.OperationId, operationId },
                            { UpdateServicePayload.SubscriptionId, serviceClient.SubscriptionId },
                            { UpdateServicePayload.ServiceName, serviceClient.ServiceName },
                            { UpdateServicePayload.RefreshToken, rdfeAccessToken },
                        },
                        DashTrace.CorrelationId);
                    var messageWrapper = new UpdateServicePayload(message);
                    messageWrapper.CreateAccountRequestIds = operationStatus.AccountsToBeCreated;
                    messageWrapper.ImportAccounts = importAccounts
                        .Select(account => GenerateConnectionString(account));
                    messageWrapper.Settings = newConfigSettings;
                    // Post message to the new namespace account (it may have changed) as that is where the async workers will read from after update
                    await new AzureMessageQueue(namespaceAccount, asyncQueueName).EnqueueAsync(message, 0);
                    // Manually fire up the async worker (so that we can supply it with the new namespace account)
                    var queueTask = ProcessOperationMessageLoop(operationId, namespaceAccount, GenerateConnectionString(namespaceAccount), GetMessageDelay(), asyncQueueName);
                    newConfig.OperationId = operationId;
                    newConfig.ScaleAccounts.MaxAccounts = DashConfiguration.MaxDataAccounts; 
                    return Content(HttpStatusCode.Accepted, newConfig);
                }
                catch (Exception ex)
                {
                    if (operationStatus != null)
                    {
                        operationStatus.UpdateStatus(UpdateConfigStatus.States.Failed, ex.ToString()).Wait();
                    }
                    throw;
                }
            });
        }

        [HttpGet, Route("configuration/validate")]
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

        [NonAction]
        public virtual int? GetMessageDelay()
        {
            // Can be mocked out during testing
            return null;
        }

        static bool CompareAccountName(IEnumerable<Tuple<string, string>> currentSettings, IDictionary<string, string> newSettings, string settingKey)
        {
            return DoCompareAccounts(currentSettings.FirstOrDefault(setting => String.Equals(setting.Item1, settingKey, StringComparison.OrdinalIgnoreCase)),
                () => {
                    var newAccount = ParseConnectionString(newSettings, settingKey, true);
                    if (newAccount == null)
                    {
                        return String.Empty;
                    }
                    return newAccount.AccountInfo.AccountName;
                });
        }

        static bool CompareAccountName(Tuple<string, string> currentSetting, ScaleAccount newAccount)
        {
            return DoCompareAccounts(currentSetting, () => newAccount == null ? String.Empty : newAccount.AccountName);
        }

        static bool DoCompareAccounts(Tuple<string, string> currentSetting, Func<string> getNewAccountName)
        {
            if (currentSetting == null)
            {
                // If we don't have a current setting, then we can set anything
                return true;
            }
            var currentAccount = ParseConnectionString(currentSetting.Item2);
            if (currentAccount == null)
            {
                return true;
            }
            var newAccountName = getNewAccountName();
            return String.Equals(currentAccount.AccountName, newAccountName, StringComparison.OrdinalIgnoreCase);
        }

        static StorageAccountCreationInfo ParseConnectionString(IDictionary<string, string> settings, string settingKey, bool blockingCall)
        {
            string connectionString;
            if (settings.TryGetValue(settingKey, out connectionString))
            {
                return new StorageAccountCreationInfo
                {
                    AccountInfo = ParseConnectionString(connectionString),
                    ConfigKey = settingKey,
                    IsBlockingOperation = blockingCall
                };
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
            return GenerateConnectionString(account.UseTls, account.AccountName, account.AccountKey);
        }

        static string GenerateConnectionString(CloudStorageAccount account)
        {
            return GenerateConnectionString(true, account.Credentials.AccountName, account.Credentials.ExportBase64EncodedKey());
        }

        static string GenerateConnectionString(bool useTls, string accountName, string accountKey)
        {
            return String.Format("DefaultEndpointsProtocol={0};AccountName={1};AccountKey={2}",
                useTls ? "https" : "http",
                accountName,
                accountKey);
        }

        static async Task<IEnumerable<Task<string>>> CreateStorageAccounts(AzureServiceManagementClient serviceClient, Configuration newConfig, IEnumerable<StorageAccountCreationInfo> newAccounts)
        {
            string location = await serviceClient.GetServiceLocation();
            return newAccounts
                .Select(accountToCreate =>
                {
                    DashTrace.TraceInformation("Creating storage account: [{0}]", accountToCreate.AccountInfo.AccountName);
                    // Determine if we're blocking or async
                    Func<Task<string>> createTask = accountToCreate.IsBlockingOperation ?
                        (Func<Task<string>>)(() => serviceClient.CreateStorageAccount(accountToCreate.AccountInfo.AccountName, location)) :
                        (Func<Task<string>>)(() => serviceClient.BeginCreateStorageAccount(accountToCreate.AccountInfo.AccountName, location));
                    return createTask()
                        .ContinueWith(antecedent =>
                        {
                            switch (antecedent.Status)
                            {
                                case TaskStatus.RanToCompletion:
                                    string accountKey = accountToCreate.IsBlockingOperation ? antecedent.Result : String.Empty;
                                    if (!accountToCreate.IsBlockingOperation)
                                    {
                                        // Although this isn't a blocking operation, we do have to block until we can retrieve the storage key
                                        try
                                        {
                                            accountKey = serviceClient.GetStorageAccountKey(accountToCreate.AccountInfo.AccountName, 
                                                2500,
                                                new CancellationTokenSource(serviceClient.StorageAccountGetKeysTimeout()).Token).Result;
                                        }
                                        catch (AggregateException ex)
                                        {
                                            throw new OperationCanceledException(
                                                String.Format("Failed to obtain access keys for storage account [{0}]. Details: {1}", 
                                                    accountToCreate.AccountInfo.AccountName, 
                                                    ex.InnerException));
                                        }
                                    }
                                    accountToCreate.AccountInfo.AccountKey = accountKey;
                                    // Update the config
                                    if (!String.IsNullOrWhiteSpace(accountToCreate.ConfigKey))
                                    {
                                        newConfig.AccountSettings[accountToCreate.ConfigKey] = GenerateConnectionString(accountToCreate.AccountInfo);
                                    }
                                    // For async operations return the request id
                                    return accountToCreate.IsBlockingOperation ? String.Empty : antecedent.Result;

                                case TaskStatus.Faulted:
                                    throw new OperationCanceledException(
                                        String.Format("Failed to create storage account [{0}]. Details: {1}",
                                            accountToCreate.AccountInfo.AccountName, 
                                            antecedent.Exception is AggregateException ? antecedent.Exception.InnerException : antecedent.Exception));

                                case TaskStatus.Canceled:
                                    throw new OperationCanceledException(String.Format("Creation of storage account [{0}] was cancelled.", accountToCreate.AccountInfo.AccountKey));

                                default:
                                    System.Diagnostics.Debug.Assert(false);
                                    throw new TaskCanceledException(String.Format("Creation of storage account [{0}] failed in unhandled state [{1}].", accountToCreate.AccountInfo.AccountKey, antecedent.Status));
                            }
                        });
                })
                .ToList();
        }
    }
}
