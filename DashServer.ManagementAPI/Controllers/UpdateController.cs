//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;
using DashServer.ManagementAPI.Models;
using Microsoft.Dash.Async;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.Dash.Common.Update;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.OperationStatus;

namespace DashServer.ManagementAPI.Controllers
{
    public class UpdateController : DelegatedAuthController
    {
        [HttpGet, ActionName("Index")]
        public async Task<IHttpActionResult> IsUpdateAvailable()
        {
            var availableUpdates = await GetAvailableUpdates();
            if (availableUpdates.Any())
            {
                return Ok(new AvailableUpgrade
                {
                    AvailableUpdate = true,
                    HighestSeverity = availableUpdates.Max(manifest => manifest.Severity).ToString(),
                    UpdateVersion = availableUpdates.Max(manifest => manifest.Version).SemanticVersionFormat(2),
                });
            }
            return Ok(new AvailableUpgrade
            {
                AvailableUpdate = false,
            });
        }

        [HttpGet, ActionName("Updates")]
        public async Task<IHttpActionResult> Updates()
        {
            return Ok(new UpgradePackages
            {
                CurrentVersion = GetCurrentVersion().SemanticVersionFormat(2),
                AvailableUpdates = await GetAvailableUpdates(),
            });
        }

        [Authorize]
        [HttpPost, ActionName("Update")]
        public async Task<IHttpActionResult> Update(UpdateVersion version)
        {
            return await DoActionAsync("UpdateController.Update", async (serviceClient) =>
            {
                var updateClient = new UpdateClient(null, DashConfiguration.PackageUpdateServiceLocation);
                var updateManifest = await updateClient.GetUpdateVersionAsync(UpdateClient.Components.DashServer, version.version);
                if (updateManifest == null)
                {
                    return NotFound();
                }
                var package = updateManifest.GetPackage(UpdateClient.GetPackageFlavorLabel(AzureService.GetServiceFlavor()));
                if (package == null)
                {
                    return NotFound();
                }
                var servicePackage = package.FindFileByExtension(".cspkg");
                var serviceConfig = package.FindFileByExtension(".cscfg");
                if (servicePackage == null || serviceConfig == null)
                {
                    return NotFound();
                }
                var operationId = DashTrace.CorrelationId.ToString();
                try
                {
                    var operationStatus = await UpdateConfigStatus.GetConfigUpdateStatus(operationId);
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.PreServiceUpdate, "Begin software upgrade to version [{0}]. Operation Id: [{1}]", version.version, operationId);
                    // Do a couple of things up-front to ensure that we can upgrade & then kick the upgrade off & don't wait for it to complete.
                    // Make sure reverse-DNS is configured
                    var updateResponse = await serviceClient.UpdateService(new HostedServiceUpdateParameters
                    {
                        ReverseDnsFqdn = String.Format("{0}.cloudapp.net.", serviceClient.ServiceName),
                    });
                    // Copy current config to config for new version
                    var currentConfig = await serviceClient.GetDeploymentConfiguration();
                    var currentSettings = AzureServiceConfiguration.GetSettingsProjected(currentConfig);
                    var newConfigDoc = XDocument.Load(
                        await updateClient.DownloadPackageFileAsync(UpdateClient.Components.DashServer, updateManifest, package, serviceConfig.Name));
                    var newSettings = AzureServiceConfiguration.GetSettings(newConfigDoc);
                    // Keep the same number of instances
                    var ns = AzureServiceConfiguration.Namespace;
                    AzureServiceConfiguration.GetInstances(newConfigDoc).Value = AzureServiceConfiguration.GetInstances(currentConfig).Value;
                    foreach (var currentSetting in currentSettings)
                    {
                        var newSetting = AzureServiceConfiguration.GetSetting(newSettings, currentSetting.Item1);
                        if (newSetting != null)
                        {
                            newSetting.SetAttributeValue("value", currentSetting.Item2);
                        }
                    }
                    // Certificates (if there are any)
                    var currentCerts = AzureServiceConfiguration.GetCertificates(currentConfig);
                    if (currentCerts != null)
                    {
                        var newCerts = AzureServiceConfiguration.GetCertificates(newConfigDoc);
                        newCerts.RemoveNodes();
                        foreach (var currentCert in currentCerts.Elements())
                        {
                            var newCert = new XElement(ns + "Certificate");
                            foreach (var currentAttrib in currentCert.Attributes())
                            {
                                newCert.SetAttributeValue(currentAttrib.Name, currentAttrib.Value);
                            }
                            newCerts.Add(newCert);
                        }
                    }
                    // Send the update to the service
                    Uri packageUri;
                    if (!String.IsNullOrWhiteSpace(servicePackage.SasUri))
                    {
                        packageUri = new Uri(servicePackage.SasUri);
                    }
                    else
                    {
                        packageUri = await updateClient.GetPackageFileSasUriAsync(UpdateClient.Components.DashServer, updateManifest, package, servicePackage.Name);
                    }
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.PreServiceUpdate, "Service upgrade using package [{0}].", packageUri.ToString());
                    var upgradeResponse = await serviceClient.UpgradeDeployment(new DeploymentUpgradeParameters
                    {
                        Label = String.Format("Dash.ManagementAPI-{0:o}", DateTime.UtcNow),
                        PackageUri = packageUri,
                        Configuration = newConfigDoc.ToString(),
                        Mode = DeploymentUpgradeMode.Auto,
                    });
                    operationStatus.CloudServiceUpdateOperationId = upgradeResponse.RequestId;
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.UpdatingService, "Service upgrade in progress.");
                    await EnqueueServiceOperationUpdate(serviceClient, operationId);
                    // Manually fire up the async worker (so that we can supply it with the new namespace account)
                    var upgradeTask = Task.Factory.StartNew(() =>
                    {
                        int processed = 0, errors = 0;
                        MessageProcessor.ProcessMessageLoop(ref processed, ref errors, GetMessageDelay(), null);
                    });

                    return Content(upgradeResponse.StatusCode, new OperationResult 
                    { 
                        OperationId = operationId, 
                    });
                }
                catch (CloudException ex)
                {
                    return Content(ex.Response.StatusCode, new OperationResult
                    {
                        OperationId = operationId,
                        ErrorCode = ex.ErrorCode,
                        ErrorMessage = ex.ErrorMessage,
                    });
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            });
        }

        async Task<IEnumerable<PackageManifest>> GetAvailableUpdates()
        {
            var updateClient = new UpdateClient(null, DashConfiguration.PackageUpdateServiceLocation);
            var currentVersion = GetCurrentVersion();
            return (await updateClient.GetAvailableManifestsAsync(UpdateClient.Components.DashServer))
                .Where(manifest => manifest.Version > currentVersion)
                .ToList();
        }

        public virtual Version GetCurrentVersion()
        {
            // Can be mocked out for tests
            var version = Assembly.GetAssembly(this.GetType()).GetName().Version;
            return version;
        }

        public virtual int? GetMessageDelay()
        {
            // Can be mocked out during testing
            return null;
        }
    }
}
