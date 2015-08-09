//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using DashServer.ManagementAPI.Utils;
using DashServer.ManagementAPI.Utils.Azure;
using Newtonsoft.Json;

namespace DashServer.ManagementAPI.Controllers
{
    [Authorize]
    public class ConfigurationController : ApiController
    {
        [HttpPut]
        public async Task<HttpResponseMessage> UpdateConfiguration(HttpRequestMessage request)
        {
            // Get json object from body and deserialize in dynamic object
            var content = await request.Content.ReadAsStringAsync();
            dynamic jsonSettings = JsonConvert.DeserializeObject<ExpandoObject>(content);
            
            // Avoid compiler error by providing type to the property
            string serviceName = jsonSettings.ServiceName;

            //using (var computeClient = new ComputeManagementClient(new CertificateCloudCredentials(_subscriptionId,
            //                new X509Certificate2(
            //                    Convert.FromBase64String(_certificateBase64)))))
            //{
            //    // Get latest config from RDFE
            //    var deployment = await computeClient.Deployments.GetBySlotAsync(serviceName, DeploymentSlot.Production);
                
            //    // Load the XML config from RDFE
            //    var root = XDocument.Parse(deployment.Configuration).Root;
            //    var rootNamespace = root.Name.NamespaceName;

            //    // Find the correct element. We only look for "ConfigurationSettings"
            //    var scaleoutSettings = root.Elements().Single(elem => elem.Name.LocalName.Equals("Role"))
            //        .Elements().Single(elem => elem.Name.LocalName.Equals("ConfigurationSettings"))
            //        .Elements().Where(setting => setting.Attribute("name").Value.Contains("Scaleout"));
                
            //    // Remove all existing Scaleout settings
            //    scaleoutSettings.Remove();

            //    // Add new settings from UI
            //    // First get the element that contains the configuration settings
            //    var newSettings = root.Elements().Single(elem => elem.Name.LocalName.Equals("Role"))
            //        .Elements().Single(elem => elem.Name.LocalName.Equals("ConfigurationSettings"));
                
            //    // Only loop the scaleout settings
            //    foreach (var property in ((IDictionary<String, Object>)jsonSettings).Where(setting => setting.Key.Contains("Scaleout")))
            //    {
            //        // Create new elements for the new settings
            //        newSettings.Add(new XElement(rootNamespace + "Setting",
            //            new XAttribute("name", property.Key),
            //            new XAttribute("value", property.Value)));
            //    }


            //    // Save the updated configuration
            //    var updateConfig = await computeClient.Deployments.ChangeConfigurationBySlotAsync(serviceName, DeploymentSlot.Production,
            //        new DeploymentChangeConfigurationParameters()
            //        {
            //            Configuration = Convert.ToBase64String(Encoding.Unicode.GetBytes(newSettings.ToString()))
            //        });

            //    var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            //    {
            //        Content = new StringContent("Configuration updated",
            //            new UnicodeEncoding(), "application/json")
            //    };
            //    return responseMessage;
            //}
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        // Special configuration attribute names
        static ISet<string> _specialConfigNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString",
            "AccountName",
            "AccountKey",
            "SecondaryAccountKey",
            "StorageConnectionStringMaster",
        };
        const string _scaleOutStoragePrefix = "ScaleoutStorage";

        [HttpGet, ActionName("Index")]
        public async Task<IHttpActionResult> GetCurrentConfiguration()
        {
            string accessToken = await DelegationToken.GetRdfeToken(this.Request.Headers.Authorization.ToString());
            if (String.IsNullOrWhiteSpace(accessToken))
            {
                return StatusCode(HttpStatusCode.Forbidden);
            }
            using (var serviceClient = await AzureService.GetServiceManagementClient(accessToken))
            {
                // Load the XML config from RDFE
                // Get only Dash storage related settings to show and make a dictionary
                // which we can use to return the properties dynamic
                Func<Tuple<string, string>, bool> filterSpecialName = (elem) => _specialConfigNames.Contains(elem.Item1);
                Func<Tuple<string, string>, bool> filterScaleoutStorage = (elem) => elem.Item1.StartsWith(_scaleOutStoragePrefix, StringComparison.OrdinalIgnoreCase);

                var settings = AzureServiceConfiguration.GetSettingsProjected(await serviceClient.GetDeploymentConfiguration())
                    .ToList();
                var response = settings
                    .Where(filterSpecialName)
                    .ToDictionary(elem => elem.Item1, elem => (object)elem.Item2);
                response[_scaleOutStoragePrefix] = settings
                    .Where(filterScaleoutStorage)
                    .ToDictionary(elem => elem.Item1, elem => (object)elem.Item2);
                response["GeneralSettings"] = settings
                    .Where(elem => !filterSpecialName(elem) && !filterScaleoutStorage(elem))
                    .ToDictionary(elem => elem.Item1, elem => (object)elem.Item2);

                return Ok(response);
            }
        }
    }
}
