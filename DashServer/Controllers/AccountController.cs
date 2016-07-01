//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

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
            xmlFormatter.SetSerializer<RequestServiceProperites>(new ObjectSerializer<RequestServiceProperites>(AccountController.SerializeServiceProperties, AccountController.DeserializeServiceProperties, AccountController.IsServicePropertiesXML));
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetBlobServiceComp(string comp)
        {
            return await DoHandlerAsync(String.Format("AccountController.GetBlobServiceComp: {0}", comp), async () =>
            {
                switch (comp.ToLower())
                {
                    case "list":
                        return await ListContainersAsync();

                    case "properties":
                        return await GetServicePropertiesAsync();

                    default:
                        return ProcessResultResponse(new HandlerResult
                            {
                                StatusCode = HttpStatusCode.BadRequest,
                            });
                }
            });
        }

        [HttpPut]
        public async Task<HttpResponseMessage> SetBlobServiceProperties()
        {
            var requestWrapper = DashHttpRequestWrapper.Create(this.Request);
            return await DoHandlerAsync("AccountController.SetBlobServiceProperties", async () =>
            {
                var formatter = GlobalConfiguration.Configuration.Formatters.XmlFormatter;
                var stream = await Request.Content.ReadAsStreamAsync();
                var properties = (RequestServiceProperites)await formatter.ReadFromStreamAsync(typeof(RequestServiceProperites), stream, null, null);

                var result = await NamespaceHandler.DoForAccountsAsync(HttpStatusCode.Accepted,
                    (client) => client.SetServicePropertiesAsync(properties.Properties),
                    DashConfiguration.AllAccounts,
                    false);

                return CreateResponse(new DelegatedResponse(result), requestWrapper.Headers);
            });
        }

        static readonly string[] _corsOptionsHeaders = new[] { "Origin", "Access-Control-Request-Method", "Access-Control-Request-Headers" };

        [HttpOptions]
        public async Task<HttpResponseMessage> OptionsCallAsync()
        {
            return await DoHandlerAsync("AccountController.OptionsCallAsync", async () =>
            {
                var request = HttpContextFactory.Current.Request;
                HttpResponseMessage response;
                if (request.Headers["Origin"] != null)
                {
                    DashTrace.TraceInformation("Forwarding real CORS OPTIONS request");
                    Uri forwardUri = ControllerOperations.ForwardUriToNamespace(request, false);
                    var forwardRequest = new HttpRequestMessage(HttpMethod.Options, forwardUri);
                    foreach (string key in _corsOptionsHeaders)
                    {
                        forwardRequest.Headers.TryAddWithoutValidation(key, request.Headers[key]);
                    }
                    HttpClient client = new HttpClient();
                    response = await client.SendAsync(forwardRequest);
                    DashTrace.TraceInformation("CORS OPTIONS response: {0}, {1}", response.StatusCode, response.ReasonPhrase);
                }
                else
                {
                    response = this.Request.CreateResponse(HttpStatusCode.OK);
                }
                response.Headers.Add("x-ms-dash-client", "true");
                return response;
            });
        }

        private async Task<HttpResponseMessage> GetServicePropertiesAsync()
        {
            var client = DashConfiguration.NamespaceAccount.CreateCloudBlobClient();
            return CreateResponse(new RequestServiceProperites
            {
                RequestVersion = this.Request.GetHeaders().RequestVersion,
                Properties = await client.GetServicePropertiesAsync(),
            });
        }

        class RequestServiceProperites
        {
            public DateTimeOffset RequestVersion { get; set; }
            public ServiceProperties Properties { get; set; }
        }

        static void SerializeServiceProperties(XmlWriter writer, RequestServiceProperites results)
        {
            writer.WriteStartElement("StorageServiceProperties");
            writer.WriteStartElement("Logging");
            writer.WriteElementStringIfNotNull("Version", results.Properties.Logging.Version);
            // TODO: These values are hard-coded until we implement logging & metrics
            writer.WriteElementString("Delete", "false");
            writer.WriteElementString("Read", "false");
            writer.WriteElementString("Write", "false");
            SerializeRetentionPolicy(writer);
            writer.WriteEndElement();   // Logging
            if (results.RequestVersion <= StorageServiceVersions.Version_2012_02_12)
            {
                // TODO: These values are hard-coded until we implement logging & metrics
                writer.WriteStartElement("Metrics");
                writer.WriteElementStringIfNotNull("Version", results.Properties.HourMetrics.Version);
                writer.WriteElementString("Enabled", "false");
                writer.WriteElementString("IncludeAPIs", "false");
                SerializeRetentionPolicy(writer);
                writer.WriteEndElement();   // Metrics
            }
            else
            {
                // TODO: These values are hard-coded until we implement logging & metrics
                writer.WriteStartElement("HourMetrics");
                writer.WriteElementStringIfNotNull("Version", results.Properties.HourMetrics.Version);
                writer.WriteElementString("Enabled", "false");
                writer.WriteElementString("IncludeAPIs", "false");
                SerializeRetentionPolicy(writer);
                writer.WriteEndElement();   // HourMetrics
                // TODO: These values are hard-coded until we implement logging & metrics
                writer.WriteStartElement("MinuteMetrics");
                writer.WriteElementStringIfNotNull("Version", results.Properties.MinuteMetrics.Version);
                writer.WriteElementString("Enabled", "false");
                writer.WriteElementString("IncludeAPIs", "false");
                SerializeRetentionPolicy(writer);
                writer.WriteEndElement();   // MinuteMetrics
                writer.WriteStartElement("Cors");
                foreach (var corsRule in results.Properties.Cors.CorsRules)
                {
                    writer.WriteStartElement("CorsRule");
                    writer.WriteElementString("AllowedOrigins", String.Join(",", corsRule.AllowedOrigins));
                    writer.WriteElementString("AllowedMethods", corsRule.AllowedMethods.ToString());
                    writer.WriteElementString("MaxAgeInSeconds", corsRule.MaxAgeInSeconds.ToString());
                    writer.WriteElementString("ExposedHeaders", String.Join(",", corsRule.ExposedHeaders));
                    writer.WriteElementString("AllowedHeaders", String.Join(",", corsRule.AllowedHeaders));
                    writer.WriteEndElement();   // CorsRule
                }
                writer.WriteEndElement();   // Cors
            }
            writer.WriteElementStringIfNotNull("DefaultServiceVersion", results.Properties.DefaultServiceVersion);
            writer.WriteEndElement();   // StorageServiceProperties
        }

        static void SerializeRetentionPolicy(XmlWriter writer)
        {
            writer.WriteStartElement("RetentionPolicy");
            writer.WriteElementString("Enabled", "false");
            writer.WriteElementString("Days", "0");
            writer.WriteEndElement();   // RetentionPolicy
        }

        static bool IsServicePropertiesXML(XmlDictionaryReader reader)
        {
            return reader.IsStartElement("StorageServiceProperties");
        }

        static readonly IDictionary<string, CorsHttpMethods> _corsMethodsLookup = new Dictionary<string,CorsHttpMethods>(StringComparer.OrdinalIgnoreCase)
        {
            { "DELETE", CorsHttpMethods.Delete },
            { "GET",    CorsHttpMethods.Get },
            { "HEAD",   CorsHttpMethods.Head },
            { "MERGE",  CorsHttpMethods.Merge },
            { "POST",   CorsHttpMethods.Post },
            { "OPTIONS", CorsHttpMethods.Options },
            { "PUT",    CorsHttpMethods.Put },
        };
        const char CorsRuleDelimiter     = ',';

        static RequestServiceProperites DeserializeServiceProperties(XmlReader reader)
        {
            var retval = new RequestServiceProperites
            {
                Properties = new ServiceProperties
                {
                    Logging = null,
                    HourMetrics = null,
                    MinuteMetrics = null,
                },
            };
            Action<XmlReader, ServiceProperties> metricsHandler = (r, props) => ObjectDeserializer.ReadObject(r,
                r.Name.ToLowerInvariant() == "minutemetrics" ? props.MinuteMetrics = new MetricsProperties() : props.HourMetrics = new MetricsProperties(), 
                (metrics, name, value) =>
                {
                    switch (name)
                    {
                        case "version":
                            metrics.Version = value;
                            break;

                        case "enabled":
                            metrics.MetricsLevel = bool.Parse(value) ? MetricsLevel.Service : metrics.MetricsLevel;
                            break;

                        case "includeapis":
                            metrics.MetricsLevel = bool.Parse(value) ? MetricsLevel.ServiceAndApi : metrics.MetricsLevel;
                            break;
                    }
                }, 
                new Dictionary<string, Action<XmlReader, MetricsProperties>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "RetentionPolicy", (r1, metricsProps) => ObjectDeserializer.ReadObject(r1, metricsProps, 
                        (p, name, value) =>
                        {
                            switch (name)
                            {
                                case "days":
                                    p.RetentionDays = int.Parse(value);
                                    break;
                            }
                        }, null)},
                });

            reader.MoveToContent();
            ObjectDeserializer.ReadObject(reader, retval.Properties, (props, name, value) =>
            {
                switch (name)
                {
                    case "defaultserviceversion":
                        props.DefaultServiceVersion = value;
                        break;
                }
            }, 
            new Dictionary<string, Action<XmlReader, ServiceProperties>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Logging", (r, props) => ObjectDeserializer.ReadObject(reader, props.Logging = new LoggingProperties(), 
                    (loggingProps, name, value) =>
                    {
                        switch (name)
                        {
                            case "version":
                                loggingProps.Version = value;
                                break;

                            case "delete":
                                loggingProps.LoggingOperations |= LoggingOperations.Delete;
                                break;

                            case "read":
                                loggingProps.LoggingOperations |= LoggingOperations.Read;
                                break;

                            case "write":
                                loggingProps.LoggingOperations |= LoggingOperations.Write;
                                break;
                        }
                    }, 
                    new Dictionary<string, Action<XmlReader, LoggingProperties>>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "RetentionPolicy", (r1, loggingProps) => ObjectDeserializer.ReadObject(reader, loggingProps, 
                            (p, name, value) =>
                            {
                                switch (name)
                                {
                                    case "days":
                                        p.RetentionDays = int.Parse(value);
                                        break;
                                }
                            }, null)},
                    }) },
                { "Metrics", metricsHandler },
                { "HourMetrics", metricsHandler },
                { "MinuteMetrics", metricsHandler },
                { "Cors", (r, props) => ObjectDeserializer.ReadCollection(r, props.Cors.CorsRules, 
                    (rule, name, value) =>
                    {
                        switch (name)
                        {
                            case "allowedorigins":
                                rule.AllowedOrigins = value.Split(CorsRuleDelimiter);
                                break;

                            case "allowedmethods":
                                rule.AllowedMethods = ObjectDeserializer.TranslateEnumFlags(value.Split(CorsRuleDelimiter), _corsMethodsLookup, (a, f) => a | f, "CorsRule");
                                break;

                            case "exposedheaders":
                                rule.ExposedHeaders = value.Split(CorsRuleDelimiter);
                                break;

                            case "allowedheaders":
                                rule.AllowedHeaders = value.Split(CorsRuleDelimiter);
                                break;

                            case "maxageinseconds":
                                rule.MaxAgeInSeconds = int.Parse(value);
                                break;
                        }
                    })}
            });
            return retval;
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
                RequestVersion = this.Request.GetHeaders().RequestVersion,
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
    }
}