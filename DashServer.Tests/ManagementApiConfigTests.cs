//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http.Results;
using System.Xml.Linq;
using DashServer.ManagementAPI.Controllers;
using DashServer.ManagementAPI.Models;
using Microsoft.Dash.Common.OperationStatus;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Storage;
using Moq;

namespace Microsoft.Tests
{
    [TestClass]
    public class ManagementApiConfigTests : ManagementApiTestBase
    {
        static IDictionary<string, string> _defaultServiceSettings = new Dictionary<string, string>
        {
            { "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", "DefaultEndpointsProtocol=https;AccountName=dashdeployment;AccountKey=uuEc6YFOVVS1YXplaQiMOO/F5BoQn/Phdce10dofYcQl0JnhDwA5rlz1BilyUjTIMLvmYXv+1YhybXLfKhGcAg==" },
            { "AccountName", "dashtest" },
            { "AccountKey", "0123456789" },
            { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
            { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
            { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestdata2;AccountKey=OOXSVWWpImRf79sbiEtpIwFsggv7VAhdjtKdt7o0gOLr2krzVXwZ+cb/gJeMqZRlXHTniRN6vnKKjs1glijihA==" },
            { "ScaleoutStorage2", "DefaultEndpointsProtocol=https;AccountName=dashtestdata3;AccountKey=wXrVhqMZF/E5sJCstDUNMNG6AoVakaJHC5XkkNnAYeT/b0h9JGh7WxTEpblqGZx9pjXviHRBKBSVODhX4hGT7A==" },
            { "ScaleoutStorage3", "" },
            { "ScaleoutStorage4", "" },
            { "ScaleoutStorage5", "" },
            { "ScaleoutStorage6", "" },
            { "ScaleoutStorage7", "" },
            { "ScaleoutStorage8", "" },
            { "ScaleoutStorage9", "" },
            { "ScaleoutStorage10", "" },
            { "ScaleoutStorage11", "" },
            { "ScaleoutStorage12", "" },
            { "ScaleoutStorage13", "" },
            { "ScaleoutStorage14", "" },
            { "ScaleoutStorage15", "" },
            { "LogNormalOperations", "false" }, 
            { "ReplicationPathPattern", "" }, 
            { "ReplicationMetadataName", "" }, 
            { "ReplicationMetadataValue", "" }, 
            { "WorkerQueueName", "" }, 
            { "AsyncWorkerTimeout", "" }, 
            { "WorkerQueueInitialDelay", "" }, 
            { "WorkerQueueDequeueLimit", "" }, 
            { "Tenant", "" }, 
            { "ClientId", "" }, 
            { "AppKey", "" }, 
            { "Microsoft.WindowsAzure.Plugins.RemoteAccess.Enabled", "true" },
            { "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountUsername", "dash" },
            { "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountEncryptedPassword", "MIIBnQYJKoZIhvcNAQcDoIIBjjCCAYoCAQAxggFOMIIBSgIBADAyMB4xHDAaBgNVBAMME1dpbmRvd3MgQXp1cmUgVG9vbHMCEGgFaCwmRya3T3OV2crpyTgwDQYJKoZIhvcNAQEBBQAEggEAA/+NsaaUE3wiLUs8LsoMfa+bHpRseLTtB0uK2eBDJ6BCgTQI6UCq4XZ9kulWLXGGoyivCH7FGc8WbwLQfdLg6w+yBOlrnAGswcsHeaoCwsBn8yH/U+NmBIxmjeJBWlJ1kq99cMYLvCW17SEoeLeoaxoXcELiJVVsjDmBShU9B2wGbAVAl45kgbTZ3lMEpUIN9dFVKrUnsspg9UvF8Tcf1twNSO8wSsFloT1qf11iYnZ5XJh0CF2W6naeNyQAk9blbFvzn5S2CW8QoTsFaBBdEUeq6GqlhUV3EvyKfJugCOaeXPHLpJ4LW39AFYaik4SnGWL2QvEyusf4NfhXpzhi+jAzBgkqhkiG9w0BBwEwFAYIKoZIhvcNAwcECD7A1XXMiYTogBAuAoXQZO6Wy/nkcJS7j9tv" },
            { "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountExpiration", "2016-08-24T23:59:59.0000000-07:00" },
            { "Microsoft.WindowsAzure.Plugins.RemoteForwarder.Enabled", "true" },
        };
        static Mock<ConfigurationController> _controllerMock;
        static ManagementApiTestContext _ctx;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _controllerMock = new Mock<ConfigurationController>();
            _ctx = SetupTestClass(_controllerMock, _defaultServiceSettings, ctx);
            _controllerMock.Setup(controller => controller.GetMessageDelay())
                .Returns(0);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            Cleanup(_ctx);
        }

        [TestMethod]
        public void ManagementGetDefaultConfigurationTest()
        {
            using (var lockObj = _ctx.ServiceFactory.LockServiceConfiguration(_ctx.DefaultServiceMock))
            {
                var controller = _controllerMock.Object;
                var currentConfig = controller.GetCurrentConfiguration().Result as OkNegotiatedContentResult<DashServer.ManagementAPI.Models.Configuration>;
                Assert.IsNotNull(currentConfig);
                Assert.IsNotNull(currentConfig.Content);
                Assert.IsNull(currentConfig.Content.OperationId);
                // AccountSettings
                Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=dashdeployment;AccountKey=uuEc6YFOVVS1YXplaQiMOO/F5BoQn/Phdce10dofYcQl0JnhDwA5rlz1BilyUjTIMLvmYXv+1YhybXLfKhGcAg==",
                    currentConfig.Content.AccountSettings["Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"]);
                Assert.AreEqual("dashtest",
                    currentConfig.Content.AccountSettings["AccountName"]);
                Assert.AreEqual("0123456789",
                    currentConfig.Content.AccountSettings["AccountKey"]);
                Assert.AreEqual(_defaultServiceSettings["StorageConnectionStringMaster"], currentConfig.Content.AccountSettings["StorageConnectionStringMaster"]);
                // ScaleOutSettings
                Assert.AreEqual(16, currentConfig.Content.ScaleAccounts.MaxAccounts);
                Assert.AreEqual(3, currentConfig.Content.ScaleAccounts.Accounts.Count);
                var account = CloudStorageAccount.Parse(_defaultServiceSettings["ScaleoutStorage0"]);
                Assert.AreEqual(account.Credentials.AccountName, currentConfig.Content.ScaleAccounts.Accounts.First().AccountName);
                Assert.AreEqual(CloudStorageAccount.Parse(_defaultServiceSettings["ScaleoutStorage2"]).Credentials.ExportBase64EncodedKey(), 
                    currentConfig.Content.ScaleAccounts.Accounts.Last().AccountKey);
                // Others
                Assert.IsFalse(currentConfig.Content.GeneralSettings.Keys.Any(
                    key => key.StartsWith("Microsoft.WindowsAzure.Plugins.RemoteAccess", StringComparison.OrdinalIgnoreCase) ||
                        key.StartsWith("Microsoft.WindowsAzure.Plugins.RemoteForwarder", StringComparison.OrdinalIgnoreCase)));
            }
        }

        [TestMethod]
        public void ManagementGetDefaultConfigurationWithOperationPendingTest()
        {
            using (var lockObj = _ctx.ServiceFactory.LockServiceConfiguration(_ctx.DefaultServiceMock))
            {
                string operationId = Guid.NewGuid().ToString("N");
                var configStatus = UpdateConfigStatus.GetConfigUpdateStatus(operationId).Result;
                configStatus.UpdateStatus(UpdateConfigStatus.States.UpdatingService, "Updating service").Wait();

                var controller = _controllerMock.Object;
                var currentConfig = controller.GetCurrentConfiguration().Result as OkNegotiatedContentResult<DashServer.ManagementAPI.Models.Configuration>;
                Assert.IsNotNull(currentConfig);
                Assert.IsNotNull(currentConfig.Content);
                Assert.AreEqual(operationId, currentConfig.Content.OperationId);
                // Set the status to complete to not interfere with outher tests
                configStatus.UpdateStatus(UpdateConfigStatus.States.Completed, "Complete").Wait();
            }
        }

        [TestMethod]
        public void ManagementUpdateDefaultConfigurationTest()
        {
            bool newSetting = false;
            new StandardUpdateConfigRunner
            {
                UpdateConfigAction = (updatedConfig) =>
                {
                    newSetting = !bool.Parse(updatedConfig.GeneralSettings["LogNormalOperations"]);
                    updatedConfig.GeneralSettings["LogNormalOperations"] = newSetting.ToString();
                },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                {
                    var updatedSetting = updatedSettings.First(setting => String.Equals(setting.Item1, "LogNormalOperations", StringComparison.OrdinalIgnoreCase));
                    Assert.AreEqual(newSetting, bool.Parse(updatedSetting.Item2));
                    Assert.AreEqual(0, configStatus.AccountsToBeImported.Count);
                    Assert.AreEqual(0, configStatus.AccountsImportedSuccess.Count);
                    Assert.AreEqual(0, configStatus.AccountsImportedFailed.Count);
                    Assert.AreEqual(0, configStatus.AccountsToBeCreated.Count);
                },
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateImportExistingAccountsTest()
        {
            // Remove the data accounts from the settings
            var adjustedSettings = _defaultServiceSettings
                .ToDictionary(setting => setting.Key, setting => setting.Key.StartsWith("ScaleoutStorage") ? String.Empty : setting.Value);
            var accountsConfig = _testConfig.Configurations["datax3"];
            new StandardUpdateConfigRunner
            {
                ServiceSettings = adjustedSettings,
                UpdateConfigAction = (updatedConfig) =>
                {
                    foreach (var dataAccount in accountsConfig.DataConnectionStrings)
                    {
                        var existingAccount = CloudStorageAccount.Parse(dataAccount);
                        updatedConfig.ScaleAccounts.Accounts.Add(new ScaleAccount
                        {
                            AccountName = existingAccount.Credentials.AccountName,
                            AccountKey = existingAccount.Credentials.ExportBase64EncodedKey(),
                        });
                    }
                },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                {
                    var newStorageSettings = updatedSettings
                        .Where(setting => !String.IsNullOrWhiteSpace(setting.Item2) && setting.Item1.StartsWith("ScaleoutStorage"));
                    Assert.AreEqual(accountsConfig.DataConnectionStrings.Count(), newStorageSettings.Count());
                    Assert.AreEqual(0, configStatus.AccountsToBeImported.Count);
                    Assert.AreEqual(0, configStatus.AccountsImportedFailed.Count);
                    Assert.AreEqual(0, configStatus.AccountsToBeCreated.Count);
                    Assert.AreEqual(accountsConfig.DataConnectionStrings.Count(), configStatus.AccountsImportedSuccess.Count);
                    Assert.IsTrue(accountsConfig.DataConnectionStrings
                        .All(connectString => configStatus.AccountsImportedSuccess.Contains(CloudStorageAccount.Parse(connectString).Credentials.AccountName)));
                },
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateImportNewAccountsTest()
        {
            // Here, we 'create' a new storage account (the creation operations are mocked out & we're left with a pre-existing real storage account).
            var accountsConfig = _testConfig.Configurations["dataimportx2"];
            IEnumerable<CloudStorageAccount> accountsToAdd = null;
            int firstEmptyAccountIndex = 0;
            new StandardUpdateConfigRunner
            {
                UpdateConfigAction = (updatedConfig) =>
                {
                    accountsToAdd = accountsConfig.DataConnectionStrings
                                        .Select(connectionSring => CloudStorageAccount.Parse(connectionSring))
                                        .Where(account => !updatedConfig.ScaleAccounts.Accounts.Any(currentAccount => String.Equals(currentAccount.AccountName, account.Credentials.AccountName, StringComparison.OrdinalIgnoreCase)))
                                        .ToList();
                    firstEmptyAccountIndex = updatedConfig.ScaleAccounts.Accounts.Count;
                    foreach (var newAccount in accountsToAdd)
                    {
                        updatedConfig.ScaleAccounts.Accounts.Add(new ScaleAccount
                        {
                            AccountName = newAccount.Credentials.AccountName,
                            AccountKey = "",
                        });
                    }
                },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                {
                    int accountIndex = firstEmptyAccountIndex;
                    foreach (var account in accountsToAdd)
                    {
                        AssertStorageSetting(updatedSettings, "ScaleoutStorage" + accountIndex.ToString(), account);
                    }
                    Assert.AreEqual(0, configStatus.AccountsToBeImported.Count);
                    Assert.AreEqual(0, configStatus.AccountsImportedFailed.Count);
                    Assert.AreEqual(accountsToAdd.Count(), configStatus.AccountsToBeCreated.Count);
                    Assert.AreEqual(accountsToAdd.Count(), configStatus.AccountsImportedSuccess.Count);
                    Assert.IsTrue(accountsToAdd
                        .All(account => configStatus.AccountsImportedSuccess.Contains(account.Credentials.AccountName)));
                },
                GetStorageKeys = (accountName) => accountsToAdd.First(account => String.Equals(accountName, account.Credentials.AccountName)).Credentials.ExportBase64EncodedKey(),
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateFromEmptyTest()
        {
            // Start with a completely empty configuration - the service has been deployed directly from the package manager
            var emptySettings = _defaultServiceSettings
                .ToDictionary(setting => setting.Key, setting => String.Empty);
            CloudStorageAccount savedNamespaceAccount = null;
            // The new account info that we're going to apply
            var accountsConfig = _testConfig.Configurations["datax3"];
            new StandardUpdateConfigRunner
            {
                ServiceSettings = emptySettings,
                UpdateConfigAction = (updatedConfig) =>
                    {
                        savedNamespaceAccount = DashConfiguration.NamespaceAccount;
                        DashConfiguration.NamespaceAccount = null;
                        updatedConfig.AccountSettings["AccountName"] = "dashtest";
                        updatedConfig.AccountSettings["AccountKey"] = "0123456789";
                        updatedConfig.AccountSettings["StorageConnectionStringMaster"] = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey=;", 
                                                                                            CloudStorageAccount.Parse(accountsConfig.NamespaceConnectionString).Credentials.AccountName);
                        foreach (var dataAccount in accountsConfig.DataConnectionStrings)
                        {
                            updatedConfig.ScaleAccounts.Accounts.Add(new ScaleAccount
                            {
                                AccountName = CloudStorageAccount.Parse(dataAccount).Credentials.AccountName,
                                AccountKey = "",
                            });
                        }
                    },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                    {
                        AssertStorageSetting(updatedSettings, "StorageConnectionStringMaster", accountsConfig.NamespaceConnectionString);
                        AssertStorageSetting(updatedSettings, "ScaleoutStorage0", accountsConfig.DataConnectionStrings.First());
                        Assert.AreEqual(0, configStatus.AccountsToBeImported.Count);
                        Assert.AreEqual(0, configStatus.AccountsImportedFailed.Count);
                        Assert.AreEqual(accountsConfig.DataConnectionStrings.Count(), configStatus.AccountsToBeCreated.Count);
                        Assert.AreEqual(accountsConfig.DataConnectionStrings.Count(), configStatus.AccountsImportedSuccess.Count);
                        Assert.IsTrue(accountsConfig.DataConnectionStrings
                            .All(connectString => configStatus.AccountsImportedSuccess.Contains(CloudStorageAccount.Parse(connectString).Credentials.AccountName)));
                        // Restore the global settings
                        DashConfiguration.NamespaceAccount = savedNamespaceAccount;
                    },
                GetStorageKeys = (accountName) =>
                    {
                        var storageAccount = accountsConfig.DataConnectionStrings
                            .Concat(Enumerable.Repeat(accountsConfig.NamespaceConnectionString, 1))
                            .Select(connectionString => CloudStorageAccount.Parse(connectionString))
                            .First(account => String.Equals(account.Credentials.AccountName, accountName));
                        return storageAccount.Credentials.ExportBase64EncodedKey();
                    },
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateFailCreateAccountTest()
        {
            new StandardUpdateConfigRunner
            {
                UpdateConfigAction = (updatedConfig) =>
                    {
                        updatedConfig.ScaleAccounts.Accounts.Add(new ScaleAccount
                        {
                            AccountName = "dashtestimport",
                            AccountKey = "",
                        });
                    },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                    {
                        Assert.AreEqual(UpdateConfigStatus.States.Failed, configStatus.State);
                    },
                GetStorageKeys = (acountName) => "eQrjK2CLvgjrMkMqamI05DZWzQwVxVevKJThNc1pK2U7Kjs1Vcs9TAWV09OLj/FWe25gqPYHHdS1ZzGLFZw9rw==",
                CustomizeMockAction = (mock) =>
                    {
                        mock.Setup(serviceClient => serviceClient.StorageAccountCreationTimeout())
                            .Returns(TimeSpan.Zero);
                    },
                UpdateChecksBeforeSuccess = 15,
                ExpectedCompletionState = UpdateConfigStatus.States.Failed,
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateFailCreateNamespaceAccountTest()
        {
            CloudStorageAccount savedNamespaceAccount = null;
            new StandardUpdateConfigRunner
            {
                UpdateConfigAction = (updatedConfig) =>
                    {
                        savedNamespaceAccount = DashConfiguration.NamespaceAccount;
                        updatedConfig.AccountSettings["StorageConnectionStringMaster"] = "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=;";
                    },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                    {
                        Assert.IsNull(updatedSettings);
                        Assert.IsNull(configStatus);
                        Assert.IsInstanceOfType(ex, typeof(AggregateException));
                        Assert.IsInstanceOfType(ex.InnerException, typeof(OperationCanceledException));
                        DashConfiguration.NamespaceAccount = savedNamespaceAccount;
                    },
                CustomizeMockAction = (mock) =>
                    {
                        mock.Setup(service => service.CreateStorageAccount(It.IsAny<string>(), It.IsAny<string>()))
                            .ThrowsAsync(new OperationCanceledException());
                    },
                ExpectedCompletionState = UpdateConfigStatus.States.Failed,
                ExpectedException = true,
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateFailUpdateServiceTest()
        {
            new StandardUpdateConfigRunner
            {
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                    {
                        Assert.IsNull(updatedSettings);
                        Assert.IsNull(ex);
                        Assert.AreEqual(UpdateConfigStatus.States.Failed, configStatus.State);
                    },
                CustomizeMockAction = (mock) =>
                    {
                        mock.Setup(service => service.ChangeDeploymentConfiguration(It.IsAny<XDocument>(), DeploymentSlot.Production))
                            .ThrowsAsync(new CloudException("Failed to update service configuration - BadRequest"));
                    },
                UpdateChecksBeforeSuccess = 15,
                ExpectedCompletionState = UpdateConfigStatus.States.Failed,
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateFailUpdateServiceAfterRetryTest()
        {
            int retryCount = 0;
            string operationId = String.Empty;
            new StandardUpdateConfigRunner
            {
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                    {
                        Assert.AreEqual(UpdateConfigStatus.States.Failed, configStatus.State);
                        Assert.AreEqual(operationId, configStatus.CloudServiceUpdateOperationId);
                    },
                CustomizeMockAction = (mock) =>
                    {
                        mock.Setup(service => service.GetOperationStatus(It.IsAny<string>()))
                            .Returns((string opId) =>
                            {
                                operationId = opId;
                                return Task.FromResult(new OperationStatusResponse
                                {
                                    Status = ++retryCount > 3 ? OperationStatus.Failed : OperationStatus.InProgress,
                                    Error = new OperationStatusResponse.ErrorDetails
                                    {
                                        Code = "FailedOperation",
                                        Message = "The operation failed",
                                    },
                                    HttpStatusCode = HttpStatusCode.Conflict,
                                    StatusCode = HttpStatusCode.OK,
                                });
                            });
                    },
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateCheckViaOperationTest()
        {
            new StandardUpdateConfigRunner
            {
                UpdateConfigAction = (updatedConfig) =>
                {
                    updatedConfig.GeneralSettings["LogNormalOperations"] = (!bool.Parse(updatedConfig.GeneralSettings["LogNormalOperations"])).ToString();
                },
                UpdateChecksBeforeSuccess = 10,
            }.ExecuteUpdateConfigWaitOperation();
        }

        [TestMethod]
        public void ManagementUpdateFailCheckViaOperationTest()
        {
            new StandardUpdateConfigRunner
            {
                UpdateConfigAction = (updatedConfig) =>
                {
                    updatedConfig.GeneralSettings["LogNormalOperations"] = (!bool.Parse(updatedConfig.GeneralSettings["LogNormalOperations"])).ToString();
                },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                {
                    Assert.IsNull(updatedSettings);
                    Assert.IsNull(ex);
                    Assert.AreEqual(UpdateConfigStatus.States.Failed, configStatus.State);
                },
                CustomizeMockAction = (mock) =>
                {
                    mock.Setup(service => service.ChangeDeploymentConfiguration(It.IsAny<XDocument>(), DeploymentSlot.Production))
                        .ThrowsAsync(new CloudException("Failed to update service configuration - BadRequest"));
                },
                UpdateChecksBeforeSuccess = 15,
                ShouldContinueWait = (configStatus) => !IsWorkerQueueEmpty(),
                ExpectedCompletionOperation = "Failed",
            }.ExecuteUpdateConfigWaitOperation();
        }

        static void AssertStorageSetting(IEnumerable<Tuple<string, string>> settings, string settingName, string connectionString)
        {
            AssertStorageSetting(settings, settingName, CloudStorageAccount.Parse(connectionString));
        }

        static void AssertStorageSetting(IEnumerable<Tuple<string, string>> settings, string settingName, CloudStorageAccount storageAccount)
        {
            AssertStorageSetting(settings, settingName, storageAccount.Credentials.AccountName, storageAccount.Credentials.ExportBase64EncodedKey());
        }

        static void AssertStorageSetting(IEnumerable<Tuple<string, string>> settings, string settingName, string accountName, string accountKey)
        {
            var newStorageSetting = settings.FirstOrDefault(setting => setting.Item1 == settingName);
            Assert.IsNotNull(newStorageSetting);
            Assert.AreNotEqual("", newStorageSetting.Item2);
            var storageAccount = CloudStorageAccount.Parse(newStorageSetting.Item2);
            Assert.AreEqual(accountName, storageAccount.Credentials.AccountName);
            Assert.AreEqual(accountKey, storageAccount.Credentials.ExportBase64EncodedKey());
        }

        bool IsWorkerQueueEmpty()
        {
            var queue = new AzureMessageQueue();
            var nextMessage = queue.Queue.PeekMessage();
            if (nextMessage == null)
            {
                Task.Delay(1000).Wait();
                nextMessage = queue.Queue.PeekMessage();
                return nextMessage == null;
            }
            return false;
        }

        private class StandardUpdateConfigRunner : ManagementApiRunner
        {
            public StandardUpdateConfigRunner()
            {
                this.ConfigController = _controllerMock.Object;
                this.ServiceFactory = _ctx.ServiceFactory;
                this.ServiceSettings = _defaultServiceSettings;
            }
        }
    }
}
