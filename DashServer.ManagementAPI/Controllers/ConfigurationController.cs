using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;
using DashServer.ManagementAPI.Models;
using DashServer.ManagementAPI.Utils;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DashServer.ManagementAPI.Controllers
{
    public class ConfigurationController : ApiController
    {
        private readonly string _subscriptionId = ConfigurationHelper.GetSetting("SubscriptionId");
        private readonly string _certificateBase64 = ConfigurationHelper.GetSetting("CertificateBase64");

        [HttpPut]
        public async Task<HttpResponseMessage> UpdateConfiguration(HttpRequestMessage request)
        {
            // Get json object from body and deserialize in dynamic object
            var content = await request.Content.ReadAsStringAsync();
            dynamic jsonSettings = JsonConvert.DeserializeObject<ExpandoObject>(content);
            
            // Avoid compiler error by providing type to the property
            string serviceName = jsonSettings.ServiceName;

            using (var computeClient = new ComputeManagementClient(new CertificateCloudCredentials(_subscriptionId,
                            new X509Certificate2(
                                Convert.FromBase64String(_certificateBase64)))))
            {
                // Get latest config from RDFE
                var deployment = await computeClient.Deployments.GetBySlotAsync(serviceName, DeploymentSlot.Production);
                
                // Load the XML config from RDFE
                var root = XDocument.Parse(deployment.Configuration).Root;
                var rootNamespace = root.Name.NamespaceName;

                // Find the correct element. We only look for "ConfigurationSettings"
                var scaleoutSettings = root.Elements().Single(elem => elem.Name.LocalName.Equals("Role"))
                    .Elements().Single(elem => elem.Name.LocalName.Equals("ConfigurationSettings"))
                    .Elements().Where(setting => setting.Attribute("name").Value.Contains("Scaleout"));
                
                // Remove all existing Scaleout settings
                scaleoutSettings.Remove();

                // Add new settings from UI
                // First get the element that contains the configuration settings
                var newSettings = root.Elements().Single(elem => elem.Name.LocalName.Equals("Role"))
                    .Elements().Single(elem => elem.Name.LocalName.Equals("ConfigurationSettings"));
                
                // Only loop the scaleout settings
                foreach (var property in ((IDictionary<String, Object>)jsonSettings).Where(setting => setting.Key.Contains("Scaleout")))
                {
                    // Create new elements for the new settings
                    newSettings.Add(new XElement(rootNamespace + "Setting",
                        new XAttribute("name", property.Key),
                        new XAttribute("value", property.Value)));
                }


                // Save the updated configuration
                var updateConfig = await computeClient.Deployments.ChangeConfigurationBySlotAsync(serviceName, DeploymentSlot.Production,
                    new DeploymentChangeConfigurationParameters()
                    {
                        Configuration = Convert.ToBase64String(Encoding.Unicode.GetBytes(newSettings.ToString()))
                    });

                var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Configuration updated",
                        new UnicodeEncoding(), "application/json")
                };
                return responseMessage;
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetCurrentConfiguration(string serviceName)
        {

            using (var computeClient = new ComputeManagementClient(new CertificateCloudCredentials(_subscriptionId,
                            new X509Certificate2(
                                Convert.FromBase64String(_certificateBase64)))))
            {
                var deployment = await computeClient.Deployments.GetBySlotAsync(serviceName, DeploymentSlot.Production);
                
                // Load the XML config from RDFE
                var root = XDocument.Parse(deployment.Configuration).Root;

                // Get only Dash storage related settings to show and make a dictionary
                // which we can use to return the properties dynamic
                var settings = root.Descendants()
                    .Where(desc => desc.Name.LocalName.Equals("Setting"))
                    .Where(attr => attr.Attributes().Any(name => name.Name.LocalName.Contains("name")))
                    .Where(attr => attr.Attributes().Any(value => value.Value.Contains("Azure")))
                    .ToDictionary<XElement, string, object>(setting => setting.Attribute("name").Value,
                        setting => setting.Attribute("value").Value);
                
                // Build a dynamic object out of the dictionary to output JSON 
                var output = new ExpandoObject() as IDictionary<string, object>;
                foreach(var setting in settings)
                    output.Add(setting.Key, setting.Value);

                var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(output), 
                        new UnicodeEncoding(), "application/json")
                };
                return responseMessage;
            }
        }
    }
}
