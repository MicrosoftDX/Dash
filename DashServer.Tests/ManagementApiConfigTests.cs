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

        [TestInitialize]
        public void Init()
        {
            _controllerMock = new Mock<ConfigurationController>();
            SetupTestClass(_controllerMock, _defaultServiceSettings);
            _controllerMock.Setup(controller => controller.GetMessageDelay())
                .Returns(0);
        }

        [TestMethod]
        public void ManagementGetDefaultConfigurationTest()
        {
            using (var lockObj = _serviceFactory.LockServiceConfiguration(_defaultServiceMock))
            {
                var controller = _controllerMock.Object;
                var currentConfig = controller.GetCurrentConfiguration().Result as OkNegotiatedContentResult<Configuration>;
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
                Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==",
                    currentConfig.Content.AccountSettings["StorageConnectionStringMaster"]);
                // ScaleOutSettings
                Assert.AreEqual(16, currentConfig.Content.ScaleAccounts.MaxAccounts);
                Assert.AreEqual(3, currentConfig.Content.ScaleAccounts.Accounts.Count);
                Assert.AreEqual("dashtestdata1", currentConfig.Content.ScaleAccounts.Accounts.First().AccountName);
                Assert.AreEqual("wXrVhqMZF/E5sJCstDUNMNG6AoVakaJHC5XkkNnAYeT/b0h9JGh7WxTEpblqGZx9pjXviHRBKBSVODhX4hGT7A==", 
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
            using (var lockObj = _serviceFactory.LockServiceConfiguration(_defaultServiceMock))
            {
                string operationId = Guid.NewGuid().ToString("N");
                var configStatus = UpdateConfigStatus.GetConfigUpdateStatus(operationId).Result;
                configStatus.UpdateStatus(UpdateConfigStatus.States.UpdatingService, "Updating service").Wait();

                var controller = _controllerMock.Object;
                var currentConfig = controller.GetCurrentConfiguration().Result as OkNegotiatedContentResult<Configuration>;
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
            new StandardUpdateConfigRunner
            {
                UpdateConfigAction = (updatedConfig) =>
                {
                    updatedConfig.GeneralSettings["LogNormalOperations"] = "true";
                },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                {
                    var updatedSetting = updatedSettings.First(setting => String.Equals(setting.Item1, "LogNormalOperations", StringComparison.OrdinalIgnoreCase));
                    Assert.AreEqual("true", updatedSetting.Item2);
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
            new StandardUpdateConfigRunner
            {
                UpdateConfigAction = (updatedConfig) =>
                {
                    updatedConfig.ScaleAccounts.Accounts.Add(new ScaleAccount
                    {
                        AccountName = "dashtestimport",
                        AccountKey = "eQrjK2CLvgjrMkMqamI05DZWzQwVxVevKJThNc1pK2U7Kjs1Vcs9TAWV09OLj/FWe25gqPYHHdS1ZzGLFZw9rw==",
                    });
                },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                {
                    var newStorageSetting = updatedSettings.FirstOrDefault(setting => setting.Item1 == "ScaleoutStorage3");
                    Assert.IsNotNull(newStorageSetting);
                    Assert.AreNotEqual("", newStorageSetting.Item2);
                    Assert.AreEqual(0, configStatus.AccountsToBeImported.Count);
                    Assert.AreEqual(0, configStatus.AccountsImportedFailed.Count);
                    Assert.AreEqual(0, configStatus.AccountsToBeCreated.Count);
                    Assert.AreEqual(1, configStatus.AccountsImportedSuccess.Count);
                    Assert.AreEqual("dashtestimport", configStatus.AccountsImportedSuccess.First());
                },
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateImportNewAccountsTest()
        {
            // Here, we 'create' a new storage account (the creation operations are mocked out & we're left with a pre-existing real storage account).
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
                    AssertStorageSetting(updatedSettings, "ScaleoutStorage3", "dashtestimport", "eQrjK2CLvgjrMkMqamI05DZWzQwVxVevKJThNc1pK2U7Kjs1Vcs9TAWV09OLj/FWe25gqPYHHdS1ZzGLFZw9rw==");
                    Assert.AreEqual(0, configStatus.AccountsToBeImported.Count);
                    Assert.AreEqual(0, configStatus.AccountsImportedFailed.Count);
                    Assert.AreEqual(1, configStatus.AccountsToBeCreated.Count);
                    //Assert.AreEqual("dashtestimport", configStatus.AccountsImportedSuccess.First());
                    Assert.AreEqual(1, configStatus.AccountsImportedSuccess.Count);
                    Assert.AreEqual("dashtestimport", configStatus.AccountsImportedSuccess.First());
                },
                GetStorageKeys = (accountName) => "eQrjK2CLvgjrMkMqamI05DZWzQwVxVevKJThNc1pK2U7Kjs1Vcs9TAWV09OLj/FWe25gqPYHHdS1ZzGLFZw9rw==",
            }.ExecuteUpdateConfigWaitStatus();
        }

        [TestMethod]
        public void ManagementUpdateFromEmptyTest()
        {
            // Start with a completely empty configuration - the service has been deployed directly from the package manager
            var emptySettings = _defaultServiceSettings
                .ToDictionary(setting => setting.Key, setting => String.Empty);
            CloudStorageAccount savedNamespaceAccount = null;
            new StandardUpdateConfigRunner
            {
                ServiceSettings = emptySettings,
                UpdateConfigAction = (updatedConfig) =>
                    {
                        savedNamespaceAccount = DashConfiguration.NamespaceAccount;
                        DashConfiguration.NamespaceAccount = null;
                        updatedConfig.AccountSettings["AccountName"] = "dashtest";
                        updatedConfig.AccountSettings["AccountKey"] = "0123456789";
                        updatedConfig.AccountSettings["StorageConnectionStringMaster"] = "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=;";
                        updatedConfig.ScaleAccounts.Accounts.Add(new ScaleAccount
                        {
                            AccountName = "dashtestimport",
                            AccountKey = "",
                        });
                    },
                VerifyConfigAction = (updatedSettings, configStatus, ex) =>
                    {
                        AssertStorageSetting(updatedSettings, "StorageConnectionStringMaster", "dashtestnamespace", "N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==");
                        AssertStorageSetting(updatedSettings, "ScaleoutStorage0", "dashtestimport", "eQrjK2CLvgjrMkMqamI05DZWzQwVxVevKJThNc1pK2U7Kjs1Vcs9TAWV09OLj/FWe25gqPYHHdS1ZzGLFZw9rw==");
                        Assert.AreEqual(0, configStatus.AccountsToBeImported.Count);
                        Assert.AreEqual(0, configStatus.AccountsImportedFailed.Count);
                        Assert.AreEqual(1, configStatus.AccountsToBeCreated.Count);
                        Assert.AreEqual(1, configStatus.AccountsImportedSuccess.Count);
                        Assert.AreEqual("dashtestimport", configStatus.AccountsImportedSuccess.First());
                        // Restore the global settings
                        DashConfiguration.NamespaceAccount = savedNamespaceAccount;
                    },
                GetStorageKeys = (accountName) =>
                    {
                        switch (accountName.ToLower())
                        {
                            case "dashtestimport":
                                return "eQrjK2CLvgjrMkMqamI05DZWzQwVxVevKJThNc1pK2U7Kjs1Vcs9TAWV09OLj/FWe25gqPYHHdS1ZzGLFZw9rw==";

                            case "dashtestnamespace":
                                return "N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==";
                        }
                        Assert.Fail();
                        return String.Empty;
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
                    updatedConfig.GeneralSettings["LogNormalOperations"] = "true";
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
                    updatedConfig.GeneralSettings["LogNormalOperations"] = "true";
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

        static void AssertStorageSetting(IEnumerable<Tuple<string, string>> settings, string settingName, string accountName, string accountKey)
        {
            var newStorageSetting = settings.FirstOrDefault(setting => setting.Item1 == settingName);
            Assert.IsNotNull(newStorageSetting);
            Assert.AreNotEqual("", newStorageSetting.Item2);
            var parts = newStorageSetting.Item2.Split(';')
                .Select(part =>
                {
                    int delimPos = part.IndexOf('=');
                    return Tuple.Create(part.Substring(0, delimPos), part.Substring(delimPos + 1));
                })
                .ToDictionary(part => part.Item1, part => part.Item2);
            Assert.AreEqual(accountName, parts["AccountName"]);
            Assert.AreEqual(accountKey, parts["AccountKey"]);
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
                this.ServiceFactory = _serviceFactory;
                this.ServiceSettings = _defaultServiceSettings;
            }
        }
    }
}
