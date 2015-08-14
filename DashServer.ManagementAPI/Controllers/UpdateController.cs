//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;
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
                return Json(new
                {
                    AvailableUpdate = true,
                    HighestSeverity = availableUpdates.Max(manifest => manifest.Severity).ToString(),
                    UpdateVersion = availableUpdates.Max(manifest => manifest.Version).ToString(),
                });
            }
            return Json(new
            {
                AvailableUpdate = false,
            });
        }

        [HttpGet, ActionName("Updates")]
        public async Task<IHttpActionResult> Updates()
        {
            return Json(await GetAvailableUpdates());
        }

        public class UpdateVersion
        {
            public string version { get; set; }
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
                try
                {
                    var operationId = DashTrace.CorrelationId.ToString();
                    var operationStatus = await UpdateConfigStatus.GetConfigUpdateStatus(operationId);
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.UpdatingService, "Begin software upgrade to version [{0}]. Operation Id: [{1}]", version.version, operationId);
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
                        await updateClient.DownloadPackageFileAsync(UpdateClient.Components.DashServer, updateManifest, package, serviceConfig));
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
                    var packageUri = await updateClient.GetPackageFileSasUriAsync(UpdateClient.Components.DashServer, updateManifest, package, servicePackage);
                    await operationStatus.UpdateStatus(UpdateConfigStatus.States.UpdatingService, "Service upgrade using package [{0}].", packageUri.ToString());
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

                    return Content(upgradeResponse.StatusCode, new { OperationId = upgradeResponse.RequestId });
                }
                catch (CloudException ex)
                {
                    return Content(HttpStatusCode.BadRequest, new
                    {
                        ErrorCode = ex.ErrorCode,
                        ErrorMessage = ex.ErrorMessage,
                        RequestId = ex.RequestId,
                        RoutingRequestId = ex.RoutingRequestId,
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
            var currentVersion = Assembly.GetAssembly(this.GetType()).GetName().Version;
            return (await updateClient.GetAvailableManifestsAsync(UpdateClient.Components.DashServer))
                .Where(manifest => manifest.Version > currentVersion)
                .ToList();
        }

    }
}
