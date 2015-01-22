//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Account")]
    public class AccountController : CommonController
    {
        static AccountController()
        {
            var xmlFormatter = GlobalConfiguration.Configuration.Formatters.XmlFormatter;
            xmlFormatter.WriterSettings.OmitXmlDeclaration = false;
            xmlFormatter.SetSerializer<ContainerListResults>(new ObjectSerializer<ContainerListResults>(AccountController.SerializeContainerListing));
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetBlobServiceComp(string comp)
        {
            switch (comp.ToLower())
            {
                case "list":
                    return await ListContainersAsync();

                default:
                    return ProcessResultResponse(new HandlerResult
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                        });
            }
        }

        [HttpOptions]
        public async Task<HttpResponseMessage> OptionsCallAsync()
        {
            var request = HttpContextFactory.Current.Request;
            Uri forwardUri = ControllerOperations.ForwardUriToNamespace(request);
            HttpRequestMessage opRequest = new HttpRequestMessage(HttpMethod.Options, forwardUri);
            List<string> keysToCopy = new List<string>();
            keysToCopy.Add("Origin");
            keysToCopy.Add("Access-Control-Request-Method");
            keysToCopy.Add("Access-Control-Request-Headers");
            foreach (string key in keysToCopy)
            {
                opRequest.Headers.TryAddWithoutValidation(key, request.Headers.Get(key));
            }
            HttpClient client = new HttpClient();
            HttpResponseMessage opResponse = await client.SendAsync(opRequest);
            opResponse.Headers.Add("x-ms-dash-client", "true");
            return opResponse;
        }

        private async Task<HttpResponseMessage> ListContainersAsync()
        {
            var client = DashConfiguration.NamespaceAccount.CreateCloudBlobClient();
            // Extract query parameters
            var queryParams = this.Request.GetQueryParameters();
            var includeFlags = String.Join(",", queryParams.Values<string>("include"));
            ContainerListingDetails listDetails = ContainerListingDetails.None;
            Enum.TryParse(includeFlags, true, out listDetails);
            var retval = new ContainerListResults
            {
                RequestVersion = new DateTimeOffset(this.Request.GetHeaders().Value("x-ms-version", StorageServiceVersions.Version_2009_09_19.UtcDateTime), TimeSpan.FromHours(0)),
                ServiceEndpoint = this.Request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped),
                Prefix = queryParams.Value<string>("prefix"),
                Marker = queryParams.Value<string>("marker"),
                MaxResults = queryParams.ValueOrNull<int>("maxresults"),
                IncludeDetails = listDetails,
            };

            retval.Containers = await client.ListContainersSegmentedAsync(
                retval.Prefix, 
                listDetails,
                retval.MaxResults == 0 ? (int?)null : retval.MaxResults, 
                new BlobContinuationToken
                {
                    NextMarker = retval.Marker, 
                },
                null, 
                null);
            return CreateResponse(retval);
        }

        class ContainerListResults
        {
            public DateTimeOffset RequestVersion { get; set; }
            public string ServiceEndpoint { get; set; }
            public string Prefix { get; set; }
            public string Marker { get; set; }
            public int? MaxResults { get; set; }
            public ContainerListingDetails IncludeDetails { get; set; }
            public ContainerResultSegment Containers { get; set; }
        }

        static void SerializeContainerListing(XmlWriter writer, ContainerListResults results)
        {
            writer.WriteStartElement("EnumerationResults");
            var uri = new UriBuilder(results.ServiceEndpoint);
            if (results.RequestVersion >= StorageServiceVersions.Version_2013_08_15)
            {
                writer.WriteAttributeString("ServiceEndpoint", results.ServiceEndpoint);
            }
            else
            {
                writer.WriteAttributeString("AccountName", results.ServiceEndpoint);
            }
            writer.WriteElementStringIfNotNull("Prefix", results.Prefix);
            writer.WriteElementStringIfNotNull("Marker", results.Marker);
            writer.WriteElementStringIfNotNull("MaxResults", results.MaxResults);
            writer.WriteStartElement("Containers");
            foreach (var container in results.Containers.Results)
            {
                writer.WriteStartElement("Container");
                writer.WriteElementString("Name", container.Name);
                if (results.RequestVersion < StorageServiceVersions.Version_2013_08_15)
                {
                    writer.WriteElementString("Url", uri.AddPathSegment(container.Name).Uri.ToString());
                }
                writer.WriteStartElement("Properties");
                writer.WriteElementString("Last-Modified", container.Properties.LastModified);
                writer.WriteElementString("Etag", container.Properties.ETag);
                if (results.RequestVersion >= StorageServiceVersions.Version_2012_02_12)
                {
                    writer.WriteElementStringIfNotEnumValue("LeaseStatus", container.Properties.LeaseStatus, LeaseStatus.Unspecified);
                    writer.WriteElementStringIfNotEnumValue("LeaseState", container.Properties.LeaseState, LeaseState.Unspecified);
                    writer.WriteElementStringIfNotEnumValue("LeaseDuration", container.Properties.LeaseDuration, LeaseDuration.Unspecified);
                }
                writer.WriteEndElement();       // Properties
                if (results.IncludeDetails.IsFlagSet(ContainerListingDetails.Metadata))
                {
                    writer.WriteStartElement("Metadata");
                    foreach (var metadataItem in container.Metadata)
                    {
                        writer.WriteElementString(metadataItem.Key, metadataItem.Value);
                    }
                    writer.WriteEndElement();   // Metadata
                }
                writer.WriteEndElement();       // Container
            }
            writer.WriteEndElement();           // Containers
            if (results.Containers.ContinuationToken != null)
            {
                writer.WriteElementStringIfNotNull("NextMarker", results.Containers.ContinuationToken.NextMarker);
            }
            writer.WriteEndElement();           // EnumerationResults 
        }

        [HttpPut]
        public async Task<HttpResponseMessage> SetBlobServiceProperties()
        {
            await Task.Delay(10);
            return new HttpResponseMessage();
        }
    }
}