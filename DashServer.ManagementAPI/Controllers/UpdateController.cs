﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;
using DashServer.ManagementAPI.Utils;
using DashServer.ManagementAPI.Utils.Azure;
using Microsoft.Dash.Common.Update;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace DashServer.ManagementAPI.Controllers
{
    public class UpdateController : ApiController
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
            var accessToken = await DelegationToken.GetRdfeToken(this.Request.Headers.Authorization.ToString());
            if (accessToken == null)
            {
                return StatusCode(HttpStatusCode.Forbidden);
            }

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
                // Do a couple of things up-front to ensure that we can upgrade & then kick the upgrade off & don't wait for it to complete.
                using (var serviceClient = await AzureService.GetServiceManagementClient(accessToken.AccessToken))
                {
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
                    var upgradeResponse = await serviceClient.UpgradeDeployment(new DeploymentUpgradeParameters
                    {
                        Label = String.Format("{0}-{1:o}", serviceClient.ServiceName, DateTime.UtcNow),
                        PackageUri = packageUri,
                        Configuration = newConfigDoc.ToString(),
                        Mode = DeploymentUpgradeMode.Auto,
                    });
                    return Content(upgradeResponse.StatusCode, upgradeResponse);
                }
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
        }

        async Task<IEnumerable<PackageManifest>> GetAvailableUpdates()
        {
            var updateClient = new UpdateClient(null, DashConfiguration.PackageUpdateServiceLocation);
            var currentVersion = Assembly.GetAssembly(this.GetType()).GetName().Version;
            currentVersion = new Version(0, 2);
            return (await updateClient.GetAvailableManifestsAsync(UpdateClient.Components.DashServer))
                .Where(manifest => manifest.Version > currentVersion)
                .ToList();
        }

    }
}
