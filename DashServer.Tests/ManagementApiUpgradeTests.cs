//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http.Results;
using System.Xml.Linq;
using DashServer.ManagementAPI.Controllers;
using DashServer.ManagementAPI.Models;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.Dash.Common.Update;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Moq;

namespace Microsoft.Tests
{
    [TestClass]
    public class ManagementApiUpgradeTests : ManagementApiTestBase
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
        static Mock<UpdateController> _controllerMock;

        [TestInitialize]
        public void Init()
        {
            _controllerMock = new Mock<UpdateController>();
            SetupTestClass(_controllerMock, _defaultServiceSettings);

        }

        [TestMethod]
        public void ManagementUpgradeCheckAvailableUpgradeTest()
        {
            var runner = new StandardUpgradeRunner
            {
                CurrentVersion = "0.1",
            };
            Func<StandardUpgradeRunner.ExecuteCoreOutputs> executeAndAssertNoUpgrade = () =>
            {
                var response = runner.UpgradeController.IsUpdateAvailable().Result as OkNegotiatedContentResult<AvailableUpgrade>;
                Assert.IsNotNull(response);
                var availableUpgrade = response.Content;
                Assert.IsNotNull(availableUpgrade);
                Assert.IsFalse(availableUpgrade.AvailableUpdate);
                return new StandardUpgradeRunner.ExecuteCoreOutputs();
            };
            var assertAvailableVersion = new GetAndAssertAvailableVersion();
            assertAvailableVersion.Execute(runner);
            // Increment the minor version beyond what's available
            var newTargetVersion = new Version(assertAvailableVersion.AvailableVersion.Major, assertAvailableVersion.AvailableVersion.Minor + 1);
            runner.CurrentVersion = newTargetVersion.ToString(2);
            runner.ExecuteCore(executeAndAssertNoUpgrade);
            // Increment the major version beyond what's available
            newTargetVersion = new Version(assertAvailableVersion.AvailableVersion.Major + 1, 0);
            runner.CurrentVersion = newTargetVersion.ToString(2);
            runner.ExecuteCore(executeAndAssertNoUpgrade);
        }

        [TestMethod]
        public void ManagementUpgradeGetAvailableUpgradesTest()
        {
            var runner = new StandardUpgradeRunner
            {
                CurrentVersion = "0.1",
            };
            Func<bool, StandardUpgradeRunner.ExecuteCoreOutputs> executeAndAssertAvailableUpgrades = (expectAvailableUpdates) =>
            {
                var response = runner.UpgradeController.Updates().Result as OkNegotiatedContentResult<UpgradePackages>;
                Assert.IsNotNull(response);
                var packages = response.Content;
                Assert.IsNotNull(packages);
                Assert.AreEqual(Version.Parse(runner.CurrentVersion), VersionUtils.ParseVersion(packages.CurrentVersion));
                Assert.IsNotNull(packages.AvailableUpdates);
                if (expectAvailableUpdates)
                {
                    Assert.IsTrue(packages.AvailableUpdates.Any());
                    var update = packages.AvailableUpdates.Last();
                    Assert.IsNotNull(update.Description);
                    Assert.IsNotNull(update.Severity);
                    Assert.IsTrue(Version.Parse(runner.CurrentVersion) < update.Version);
                    Assert.IsNotNull(update.AvailablePackages);
                    Assert.IsTrue(update.AvailablePackages.Any());
                    var package = update.AvailablePackages.First();
                    Assert.IsNotNull(package.PackageName);
                    Assert.IsTrue(package.FileInformations.Any());
                    Assert.AreEqual(package.Files.Count(), package.FileInformations.Count());
                    var fileInfo = package.FileInformations.First();
                    Assert.IsNotNull(fileInfo.Name);
                    Assert.IsNotNull(fileInfo.SasUri);
                }
                else
                {
                    Assert.IsFalse(packages.AvailableUpdates.Any());
                }
                return new StandardUpgradeRunner.ExecuteCoreOutputs();
            };
            runner.ExecuteCore(() => executeAndAssertAvailableUpgrades(true));
            runner.CurrentVersion = "50.0";
            runner.ExecuteCore(() => executeAndAssertAvailableUpgrades(false));
        }

        [TestMethod]
        public void ManagementUpgradeLatestVersionWaitStatusTest()
        {
            var runner = new StandardUpgradeRunner
            {
                CurrentVersion = "0.1",
                VerifyUpgradeAction = (response, upgradeParams) =>
                {
                    Assert.AreEqual(DeploymentUpgradeMode.Auto, upgradeParams.Mode);
                    Assert.IsTrue(upgradeParams.PackageUri.Host.Contains("blob.core.windows.net"));
                    var configDoc = XDocument.Parse(upgradeParams.Configuration);
                    var settings = AzureServiceConfiguration.GetSettingsProjected(configDoc)
                        .ToDictionary(setting => setting.Item1, setting => setting.Item2);
                    Assert.AreEqual("dashtest", settings["AccountName"]);
                    Assert.AreEqual("0123456789", settings["AccountKey"]);
                    Assert.IsNotNull(settings["ScaleoutStorage0"]);
                    Assert.IsNotNull(settings["ScaleoutStorage2"]);
                    Assert.AreEqual("", settings["ScaleoutStorage3"]);
                },
            };
            var assertAvailableVersion = new GetAndAssertAvailableVersion();
            assertAvailableVersion.Execute(runner);
            runner.UpgradeVersion = assertAvailableVersion.AvailableVersion.SemanticVersionFormat();
            runner.ExecuteSoftwareUpgradeWaitStatus();
        }

        [TestMethod]
        public void ManagementUpgradeLatestVersionWaitOperationTest()
        {
            var runner = new StandardUpgradeRunner
            {
                CurrentVersion = "0.1",
            };
            var assertAvailableVersion = new GetAndAssertAvailableVersion();
            assertAvailableVersion.Execute(runner);
            runner.UpgradeVersion = assertAvailableVersion.AvailableVersion.SemanticVersionFormat();
            runner.ExecuteSoftwareUpgradeWaitOperation();
        }

        [TestMethod]
        public void ManagementUpgradeInvalidVersionTest()
        {
            new StandardUpgradeRunner
            {
                CurrentVersion = "0.1",
                UpgradeVersion = "50.0",
                ExpectedAbnormalResponse = true,
                VerifyUpgradeAction = (response, upgradeParams) =>
                {
                    Assert.IsNotNull(response as NotFoundResult);
                }
            }.ExecuteSoftwareUpgradeWaitStatus();
        }

        [TestMethod]
        public void ManagementUpgradeFailureWaitOperationTest()
        {
            var runner = new StandardUpgradeRunner
            {
                CurrentVersion = "0.1",
                CustomizeMockAction = (mock) =>
                {
                    mock.Setup(service => service.UpgradeDeployment(It.IsAny<DeploymentUpgradeParameters>(), DeploymentSlot.Production))
                        .ThrowsAsync(CloudException.Create(new HttpRequestMessage(), "", new HttpResponseMessage(HttpStatusCode.BadRequest), "{ 'code': 'MissingOrInvalidRequiredQueryParameter', 'message': 'A required query parameter was not specified for this request or was specified incorrectly.' }"));
                },
                VerifyUpgradeAction = (response, upgradeParams) =>
                {
                    var contentResponse = response as NegotiatedContentResult<OperationResult>;
                    Assert.IsNotNull(contentResponse);
                    Assert.AreEqual(HttpStatusCode.BadRequest, contentResponse.StatusCode);
                    Assert.AreEqual("MissingOrInvalidRequiredQueryParameter", contentResponse.Content.ErrorCode);
                },
                ExpectedAbnormalResponse = true,
                ExpectedCompletionOperation = "Failed",
            };
            var assertAvailableVersion = new GetAndAssertAvailableVersion();
            assertAvailableVersion.Execute(runner);
            runner.UpgradeVersion = assertAvailableVersion.AvailableVersion.SemanticVersionFormat();
            runner.ExecuteSoftwareUpgradeWaitOperation();
        }

        [TestMethod]
        public void ManagementUpgradeFailureAfterRetryWaitOperationTest()
        {
            int retryCount = 0;
            var runner = new StandardUpgradeRunner
            {
                CurrentVersion = "0.1",
                CustomizeMockAction = (mock) =>
                {
                    mock.Setup(service => service.GetOperationStatus(It.IsAny<string>()))
                        .Returns((string opId) =>
                        {
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
                VerifyUpgradeAction = (response, upgradeParams) =>
                {
                    Assert.IsNotNull(upgradeParams);
                    var contentResponse = response as NegotiatedContentResult<OperationResult>;
                    Assert.IsNotNull(contentResponse);
                    Assert.AreEqual(HttpStatusCode.Accepted, contentResponse.StatusCode);
                },
                UpdateChecksBeforeSuccess = 15,
                ExpectedCompletionOperation = "Failed",
            };
            var assertAvailableVersion = new GetAndAssertAvailableVersion();
            assertAvailableVersion.Execute(runner);
            runner.UpgradeVersion = assertAvailableVersion.AvailableVersion.SemanticVersionFormat();
            runner.ExecuteSoftwareUpgradeWaitOperation();
        }

        class StandardUpgradeRunner : ManagementApiRunner
        {
            public StandardUpgradeRunner()
            {
                this.UpgradeController = _controllerMock.Object;
                this.ServiceFactory = _serviceFactory;
                this.ServiceSettings = _defaultServiceSettings;

                _controllerMock.Setup(controller => controller.GetCurrentVersion())
                    .Returns(() => new Version(this.CurrentVersion));
                _controllerMock.Setup(controller => controller.GetMessageDelay())
                    .Returns(0);
            }
        }

        class GetAndAssertAvailableVersion
        {
            public Version AvailableVersion { get; private set; }

            public void Execute(StandardUpgradeRunner runner)
            {
                runner.ExecuteCore(() =>
                {
                    var response = runner.UpgradeController.IsUpdateAvailable().Result as OkNegotiatedContentResult<AvailableUpgrade>;
                    Assert.IsNotNull(response);
                    var availableUpgrade = response.Content;
                    Assert.IsNotNull(availableUpgrade);
                    Assert.IsTrue(availableUpgrade.AvailableUpdate);
                    Assert.IsNotNull(availableUpgrade.HighestSeverity);
                    Assert.IsNotNull(availableUpgrade.UpdateVersion);
                    this.AvailableVersion = VersionUtils.ParseVersion(availableUpgrade.UpdateVersion);
                    return new StandardUpgradeRunner.ExecuteCoreOutputs();
                });
            }
        }
    }
}
