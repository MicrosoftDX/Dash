using Microsoft.Dash.Common.Update;
using Microsoft.Dash.Common.Utils;
//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace DashServer.ManagementAPI.Controllers
{
    public class UpdateController : ApiController
    {
        [HttpGet]
        public async Task<HttpResponseMessage> IsUpdateAvailable()
        {
            var availableUpdates = await GetAvailableUpdates();
            return this.Request.CreateResponse(HttpStatusCode.OK, new
            {
                AvailableUpdate = availableUpdates.Any(),
                HighestServerify = availableUpdates.Max(manifest => manifest.Severity).ToString(),
                UpdateVersion = availableUpdates.Max(manifest => manifest.Version).ToString(),
            });
        }

        public async Task<HttpResponseMessage> Updates()
        {
            return this.Request.CreateResponse(HttpStatusCode.OK, await GetAvailableUpdates());
        }

        [HttpPost, ActionName("Update")]
        public async Task<HttpResponseMessage> PostUpdate(string version)
        {
            var updateClient = new UpdateClient(this.Request.ServerVariables["SERVER_NAME"],
                Configuration.PackageUpdateServiceLocation);
            var updateManifest = await updateClient.GetUpdateVersionAsync(UpdateClient.Components.ManagementConsole, version);
            if (updateManifest == null)
            {
                return HttpNotFound();
            }
            var package = updateManifest.GetPackage(FilePackage.PackageNameConsole);
            if (package == null)
            {
                return HttpNotFound();
            }
            var msDeployPackage = package.FindFileByExtension(".zip");
            var msDeployParameters = package.FindFileByExtension(".xml");
            if (msDeployPackage == null || msDeployParameters == null)
            {
                return HttpNotFound();
            }
            try
            {
                string localPackageLocation = await updateClient.DownloadPackageFileToTempFileAsync(UpdateClient.Components.ManagementConsole, updateManifest, package, msDeployPackage);
                string parametersFile = String.Empty;
                if (!String.IsNullOrWhiteSpace(localPackageLocation))
                {
                    using (var parametersStream = await updateClient.DownloadPackageFileToTempFileStreamAsync(UpdateClient.Components.ManagementConsole, updateManifest, package, msDeployParameters))
                    {
                        var parametersDoc = XDocument.Load(parametersStream);
                        var parameters = parametersDoc.Element("parameters")
                            .Elements()
                            .ToDictionary(element => element.Attribute("name").Value, element => element.Attribute("value"), StringComparer.OrdinalIgnoreCase);
                        // Process specific parameters that we know about
                        parameters["AD_RealmAppSetting"].Value = "1";
                        parameters["AD_AudienceUriAppSetting"].Value = "1";
                        parameters["AD_ClientID"].Value = Configuration.ClientID;
                        parameters["AD_ClientPassword"].Value = Configuration.ClientKey;
                        parameters["AD_APPIDUri"].Value = Url.Action("Index", "Home", null, "http");
                        parameters["ConsoleContext-Web.config Connection String"].Value = Configuration.ConsoleConnectionString;
                        parameters["AD_Tenant"].Value = Configuration.Tenant;
                        // Process all other parameters that we don't need specific handling for. ie. there's a 1:1 mapping between parameter & appSetting
                        foreach (var setting in ConfigurationManager.AppSettings.AllKeys
                                                    .Where(setting => parameters.ContainsKey(setting)))
                        {
                            parameters[setting].Value = ConfigurationManager.AppSettings[setting];
                        }

                        parametersStream.Seek(0, SeekOrigin.Begin);
                        parametersStream.SetLength(0);
                        parametersDoc.Save(parametersStream);

                        parametersFile = ((FileStream)parametersStream).Name;
                    }
                }

                var msdeploy = new Process();
                msdeploy.StartInfo.FileName = Environment.ExpandEnvironmentVariables("%systemdrive%\\Program Files\\IIS\\Microsoft Web Deploy V3\\msdeploy.exe");
                string sitePath = Environment.ExpandEnvironmentVariables("%systemdrive%\\inetpub\\wwwroot");
                if (!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEPLOYMENT_TARGET")))
                {
                    sitePath = Environment.ExpandEnvironmentVariables("%DEPLOYMENT_TARGET%");
                }
                else if (!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOME")))
                {
                    sitePath = Environment.ExpandEnvironmentVariables("%HOME%\\site\\wwwroot");
                }
                else
                {
                    Trace.TraceInformation("DEPLOYMENT_TARGET or HOME environment variables not set. Maybe not running in WAWS?");
                }
                msdeploy.StartInfo.Arguments = String.Format("-verb:sync -useCheckSum -source:package='{0}' -dest:contentPath='{1}' -setParamFile={2}",
                    localPackageLocation, sitePath, parametersFile);
                msdeploy.StartInfo.UseShellExecute = false;
                msdeploy.StartInfo.RedirectStandardOutput = true;
                msdeploy.StartInfo.RedirectStandardError = true;
                msdeploy.Start();
                return Json(new { Output = msdeploy.StandardOutput.ReadToEnd(), Error = msdeploy.StandardError.ReadToEnd() });
            }
            catch (Exception ex)
            {
                return Json(new { Output = "", Error = ex.ToString() });
            }
        }

        async Task<IEnumerable<PackageManifest>> GetAvailableUpdates()
        {
            var updateClient = new UpdateClient(this.Request.ServerVariables["SERVER_NAME"],
                DashConfiguration.PackageUpdateServiceLocation);
            var currentVersion = Assembly.GetAssembly(typeof(HomeController)).GetName().Version;
            return (await updateClient.GetAvailableManifestsAsync(UpdateClient.Components.ManagementConsole))
                .Where(manifest => manifest.Version > currentVersion)
                .ToList();
        }

    }
}
