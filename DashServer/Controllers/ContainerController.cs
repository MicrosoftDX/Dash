//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Xml;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Container")]
    public class ContainerController : CommonController
    {

        static ContainerController()
        {
            var xmlFormatter = GlobalConfiguration.Configuration.Formatters.XmlFormatter;
            xmlFormatter.WriterSettings.OmitXmlDeclaration = false;
            xmlFormatter.SetSerializer<BlobListHandler.BlobListResults>(new ObjectSerializer<BlobListHandler.BlobListResults>(ContainerController.SerializeBlobListing));
            xmlFormatter.SetSerializer<SharedAccessBlobPolicies>(new ObjectSerializer<SharedAccessBlobPolicies>(ContainerController.SerializeAccessPolicies, ContainerController.DeserializeAccessPolicies, ContainerController.IsSharedAccessPolicyXML));
        }

        /// Put Container - http://msdn.microsoft.com/en-us/library/azure/dd179468.aspx
        [HttpPut]
        public async Task<HttpResponseMessage> CreateContainer(string container)
        {
            return new DelegatedResponse(await ContainerHandler.CreateContainer(container)).CreateResponse();
        }

        // Put Container operations, with 'comp' parameter'
        [HttpPut]
        public async Task<HttpResponseMessage> PutContainerComp(string container, string comp)
        {
            return await DoHandlerAsync(String.Format("ContainerController.PutContainerComp: {0}", comp), async () =>
                {
                    CloudBlobContainer containerObj = NamespaceHandler.GetContainerByName(DashConfiguration.NamespaceAccount, container);
                    HttpResponseMessage errorResponse = await ValidatePreconditions(containerObj);
                    if (errorResponse != null)
                    {
                        return errorResponse;
                    }
                    switch (comp.ToLower())
                    {
                        case "lease":
                            return await SetContainerLease(containerObj);
                        case "metadata":
                            return await SetContainerMetadata(containerObj);
                        case "acl":
                            return await SetContainerAcl(containerObj);
                        default:
                            return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                });
        }

        /// Delete Container - http://msdn.microsoft.com/en-us/library/azure/dd179408.aspx
        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteContainer(string container)
        {
            return new DelegatedResponse(await ContainerHandler.DeleteContainer(container)).CreateResponse();
        }

        [AcceptVerbs("GET", "HEAD")]
        public async Task<HttpResponseMessage> GetContainerProperties(string container)
        {
            return await DoHandlerAsync("ContainerController.GetContainerProperties", async () =>
                {
                    CloudBlobContainer containerObj = NamespaceHandler.GetContainerByName(DashConfiguration.NamespaceAccount, container);
                    HttpResponseMessage errorResponse = await ValidatePreconditions(containerObj);
                    if (errorResponse != null)
                    {
                        return errorResponse;
                    }
                    HttpResponseMessage response = await FormContainerMetadataResponse(containerObj);

                    response.Headers.Add("x-ms-lease-status", containerObj.Properties.LeaseStatus.ToString().ToLower());
                    response.Headers.Add("x-ms-lease-state", containerObj.Properties.LeaseState.ToString().ToLower());
                    //Only add Lease Duration information if the container is leased
                    if (containerObj.Properties.LeaseState == LeaseState.Leased)
                    {
                        response.Headers.Add("x-ms-lease-duration", containerObj.Properties.LeaseDuration.ToString().ToLower());
                    }

                    return response;
                });
        }

        [AcceptVerbs("GET", "HEAD")]
        //Get Container operations, with optional 'comp' parameter
        public async Task<HttpResponseMessage> GetContainerData(string container, string comp)
        {
            return await DoHandlerAsync(String.Format("ContainerController.GetContainerData: {0}", comp), async () =>
                {
                    CloudBlobContainer containerObj = NamespaceHandler.GetContainerByName(DashConfiguration.NamespaceAccount, container);
                    HttpResponseMessage errorResponse = await ValidatePreconditions(containerObj);
                    if (errorResponse != null)
                    {
                        return errorResponse;
                    }
                    switch (comp.ToLower())
                    {
                        case "list":
                            return await GetBlobList(container);
                        case "acl":
                            return await FormContainerAclResponse(containerObj);
                        case "metadata":
                            return await FormContainerMetadataResponse(containerObj);

                        default:
                            return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }
                });
            
        }

        private async Task<HttpResponseMessage> SetContainerAcl(CloudBlobContainer container)
        {
            SharedAccessBlobPolicies policies = null;
            if (this.Request.Content.Headers.ContentLength.HasValue && this.Request.Content.Headers.ContentLength.Value > 0)
            {
                var formatter = GlobalConfiguration.Configuration.Formatters.XmlFormatter;
                var stream = await Request.Content.ReadAsStreamAsync();
                policies = (SharedAccessBlobPolicies)await formatter.ReadFromStreamAsync(typeof(SharedAccessBlobPolicies), stream, null, null);
            }
            string accessLevel = TryGetHeader(Request.Headers, "x-ms-blob-public-access");
            BlobContainerPublicAccessType access = BlobContainerPublicAccessType.Off;
            if (!String.IsNullOrWhiteSpace(accessLevel))
            {
                if (accessLevel.ToLower() == "blob")
                {
                    access = BlobContainerPublicAccessType.Blob;
                }
                else if (accessLevel.ToLower() == "container")
                {
                    access = BlobContainerPublicAccessType.Container;
                }
            }
            BlobContainerPermissions perms = new BlobContainerPermissions()
            {
                PublicAccess = access
            };
            if (policies != null)
            {
                foreach (var policy in policies)
                {
                    perms.SharedAccessPolicies.Add(policy);
                }
            }

            var status = await ContainerHandler.DoForAllContainersAsync(container.Name, 
                HttpStatusCode.OK, 
                async containerObj => await containerObj.SetPermissionsAsync(perms),
                true);
            
            HttpResponseMessage response = new HttpResponseMessage(status.StatusCode);
            await AddBasicContainerHeaders(response, container);
            return response;
        }

        private async Task<HttpResponseMessage> ValidatePreconditions(CloudBlobContainer container)
        {
            // TODO: Ensure that all preconditions are validated
            if (!await container.ExistsAsync())
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            IEnumerable<string> leaseID;
            if (Request.Headers.TryGetValues("x-ms-lease-id", out leaseID))
            {
                AccessCondition condition = new AccessCondition()
                {
                    LeaseId = leaseID.First()
                };
                //Try fetching the attributes to force validation of the leaseID
                await container.FetchAttributesAsync(condition, null, null);
            }
            // If we don't find any errors, just return null to indicate that everything is A-OK.
            return null;
        }

        private async Task<HttpResponseMessage> SetContainerMetadata(CloudBlobContainer container)
        {
            const string MetadataPrefix = "x-ms-meta-";
            HttpRequestBase request = RequestFromContext(HttpContextFactory.Current);
            container.Metadata.Clear();
            var metadata = Request.Headers.Where(header => header.Key.StartsWith(MetadataPrefix));
            foreach (var metadatum in metadata)
            {
                container.Metadata.Add(metadatum.Key.Substring(MetadataPrefix.Length), metadatum.Value.FirstOrDefault());
            }
            await container.SetMetadataAsync();
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            await AddBasicContainerHeaders(response, container);
            return response;
        }

        private async Task<HttpResponseMessage> SetContainerLease(CloudBlobContainer container)
        {
            IEnumerable<string> action;
            string leaseId = null;
            string proposedLeaseId = null;
            HttpResponseMessage response = new HttpResponseMessage();
            AccessCondition condition;
            string serverLeaseId;
            await AddBasicContainerHeaders(response, container);
            //If an action is not provided, it's not a valid call.
            if (!Request.Headers.TryGetValues("x-ms-lease-action", out action))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }
            if (Request.Headers.Contains("x-ms-lease-id"))
            {
                leaseId = Request.Headers.GetValues("x-ms-lease-id").FirstOrDefault();
            }
            if (Request.Headers.Contains("x-ms-proposed-lease-id"))
            {
                proposedLeaseId = Request.Headers.GetValues("x-ms-proposed-lease-id").FirstOrDefault();
            }
            
            switch (action.First().ToLower())
            {
                case "acquire":
                    int leaseDuration = Int32.Parse(Request.Headers.GetValues("x-ms-lease-duration").First());
                    TimeSpan? leaseDurationSpan;
                    if (leaseDuration == -1)
                    {
                        leaseDurationSpan = null;
                    }
                    else if (leaseDuration >= 15 && leaseDuration <= 60)
                    {
                        leaseDurationSpan = new TimeSpan(0, 0, leaseDuration);
                    }
                    else
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        return response;
                    }
                    serverLeaseId = await container.AcquireLeaseAsync(leaseDurationSpan, proposedLeaseId);
                    response = new DelegatedResponse(await ContainerHandler.DoForDataContainersAsync(container.Name, 
                        HttpStatusCode.Created, 
                        async containerObj => await containerObj.AcquireLeaseAsync(leaseDurationSpan, serverLeaseId),
                        true)).CreateResponse();
                    await AddBasicContainerHeaders(response, container);
                    response.Headers.Add("x-ms-lease-id", serverLeaseId);
                    return response;

                case "renew":
                    condition = new AccessCondition()
                    {
                        LeaseId = leaseId
                    };
                    response = new DelegatedResponse(await ContainerHandler.DoForAllContainersAsync(container.Name, 
                        HttpStatusCode.OK, 
                        async containerObj => await containerObj.RenewLeaseAsync(condition),
                        true)).CreateResponse();
                    await AddBasicContainerHeaders(response, container);
                    response.Headers.Add("x-ms-lease-id", leaseId);
                    return response;

                case "change":
                    condition = new AccessCondition()
                    {
                        LeaseId = leaseId
                    };
                    serverLeaseId = await container.ChangeLeaseAsync(proposedLeaseId, condition);
                    response = new DelegatedResponse(await ContainerHandler.DoForDataContainersAsync(container.Name, 
                        HttpStatusCode.OK, 
                        async containerObj => await containerObj.ChangeLeaseAsync(proposedLeaseId, condition),
                        true)).CreateResponse();
                    await AddBasicContainerHeaders(response, container);
                    response.Headers.Add("x-ms-lease-id", container.ChangeLease(proposedLeaseId, condition));
                    return response;

                case "release":
                    condition = new AccessCondition()
                    {
                        LeaseId = leaseId
                    };
                    response = new DelegatedResponse(await ContainerHandler.DoForAllContainersAsync(container.Name, 
                        HttpStatusCode.OK, 
                        async containerObj => await containerObj.ReleaseLeaseAsync(condition),
                        true)).CreateResponse();
                    await AddBasicContainerHeaders(response, container);
                    return response;

                case "break":
                    int breakDuration = 0;
                    if (Request.Headers.Contains("x-ms-lease-break-period"))
                    {
                        breakDuration = Int32.Parse(Request.Headers.GetValues("x-ms-lease-break-period").FirstOrDefault());
                    }
                    TimeSpan breakDurationSpan = new TimeSpan(0, 0, breakDuration);
                    TimeSpan remainingTime = await container.BreakLeaseAsync(breakDurationSpan);
                    response = new DelegatedResponse(await ContainerHandler.DoForDataContainersAsync(container.Name, 
                        HttpStatusCode.Accepted, 
                        async containerObj => await containerObj.BreakLeaseAsync(breakDurationSpan),
                        true)).CreateResponse();
                    await AddBasicContainerHeaders(response, container);
                    response.Headers.Add("x-ms-lease-time", remainingTime.Seconds.ToString());
                    return response;

                default:
                    //Not a recognized action
                    response.StatusCode = HttpStatusCode.BadRequest;
                    return response;
            }
        }

        private async Task<HttpResponseMessage> FormContainerAclResponse(CloudBlobContainer container)
        {
            BlobContainerPermissions permissions = await container.GetPermissionsAsync();
            HttpResponseMessage response = CreateResponse(permissions.SharedAccessPolicies);
            await AddBasicContainerHeaders(response, container);

            //Only add this header if some form of public access is permitted.
            if (permissions.PublicAccess != BlobContainerPublicAccessType.Off)
            {
                response.Headers.Add("x-ms-blob-public-access", permissions.PublicAccess.ToString().ToLower());
            }

            return response;
        }

        private async Task<HttpResponseMessage> FormContainerMetadataResponse(CloudBlobContainer container)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            await AddBasicContainerHeaders(response, container);

            foreach (KeyValuePair<string, string> pair in container.Metadata)
            {
                response.Headers.Add("x-ms-meta-" + pair.Key, pair.Value);
            }

            return response;
        }

        private string TryGetHeader(HttpHeaders headers, string headerName)
        {
            IEnumerable<string> values;
            if (headers.TryGetValues(headerName, out values))
            {
                return values.FirstOrDefault();
            }
            return String.Empty;
        }

        private async Task AddBasicContainerHeaders(HttpResponseMessage response, CloudBlobContainer container)
        {
            await container.FetchAttributesAsync();
            response.Headers.ETag = new EntityTagHeaderValue(container.Properties.ETag);
            if (container.Properties.LastModified.HasValue)
            {
                if (response.Content == null)
                {
                    response.Content = new StringContent("");
                }
                response.Content.Headers.LastModified = container.Properties.LastModified.Value.UtcDateTime;
            }
            //Right now we are just parroting back the values sent by the client. Might need to generate our
            //own values for these if none are provided.
            IEnumerable<string> headerValues;
            if (Request.Headers.TryGetValues("x-ms-client-request-id", out headerValues))
            {
                response.Headers.Add("x-ms-request-id", headerValues);
            }
            response.Headers.Add("x-ms-version", TryGetHeader(Request.Headers, "x-ms-version"));
            response.Headers.Date = DateTimeOffset.UtcNow;
        }

        private async Task<HttpResponseMessage> GetBlobList(string container)
        {
            return CreateResponse(await BlobListHandler.GetBlobListing(container, DashHttpRequestWrapper.Create(this.Request)));
        }

        static void SerializeBlobListing(XmlWriter writer, BlobListHandler.BlobListResults results)
        {
            var uri = new UriBuilder(results.ServiceEndpoint);
            writer.WriteStartElement("EnumerationResults");
            if (results.RequestVersion >= StorageServiceVersions.Version_2013_08_15)
            {
                writer.WriteAttributeString("ServiceEndpoint", results.ServiceEndpoint);
                writer.WriteAttributeString("ContainerName", results.ContainerName);
            }
            else
            {
                uri = uri.AddPathSegment(results.ContainerName);
                writer.WriteAttributeString("ContainerName", uri.Uri.ToString());
            }
            writer.WriteElementStringIfNotNull("Prefix", results.Prefix);
            writer.WriteElementStringIfNotNull("Marker", results.Marker);
            writer.WriteElementStringIfNotNull("MaxResults", results.IndicatedMaxResults);
            writer.WriteElementStringIfNotNull("Delimiter", results.Delimiter);
            writer.WriteStartElement("Blobs");
            IListBlobItem nextBlob = null;
            int blobCount = 0;
            foreach (var blob in results.Blobs)
            {
                if (++blobCount > results.MaxResults)
                {
                    nextBlob = blob;
                    break;
                }
                else if (blob is ICloudBlob)
                {
                    var realBlob = (ICloudBlob)blob;
                    writer.WriteStartElement("Blob");
                    writer.WriteElementString("Name", realBlob.Name);
                    if (results.RequestVersion < StorageServiceVersions.Version_2013_08_15)
                    {
                        writer.WriteElementString("Url", uri.AddPathSegment(realBlob.Name).Uri.ToString());
                    }
                    if (realBlob.IsSnapshot && results.IncludeDetails.IsFlagSet(BlobListingDetails.Snapshots))
                    {
                        writer.WriteElementString("Snapshot", realBlob.SnapshotTime, true);
                    }
                    writer.WriteStartElement("Properties");
                    writer.WriteElementString("Last-Modified", realBlob.Properties.LastModified);
                    writer.WriteElementString("Etag", realBlob.Properties.ETag);
                    writer.WriteElementString("Content-Length", realBlob.Properties.Length.ToString());
                    writer.WriteElementString("Content-Type", realBlob.Properties.ContentType);
                    writer.WriteElementString("Content-Encoding", realBlob.Properties.ContentEncoding);
                    writer.WriteElementString("Content-Language", realBlob.Properties.ContentLanguage);
                    writer.WriteElementString("Content-MD5", realBlob.Properties.ContentMD5);
                    writer.WriteElementString("Cache-Control", realBlob.Properties.CacheControl);
                    writer.WriteElementString("Content-Disposition", realBlob.Properties.ContentDisposition);
                    writer.WriteElementStringIfNotNull("x-ms-blob-sequence-number", realBlob.Properties.PageBlobSequenceNumber);
                    writer.WriteElementStringIfNotEnumValue("BlobType", realBlob.Properties.BlobType, BlobType.Unspecified, false);
                    if (results.RequestVersion >= StorageServiceVersions.Version_2012_02_12)
                    {
                        writer.WriteElementStringIfNotEnumValue("LeaseStatus", realBlob.Properties.LeaseStatus, LeaseStatus.Unspecified);
                        writer.WriteElementStringIfNotEnumValue("LeaseState", realBlob.Properties.LeaseState, LeaseState.Unspecified);
                        writer.WriteElementStringIfNotEnumValue("LeaseDuration", realBlob.Properties.LeaseDuration, LeaseDuration.Unspecified);
                    }
                    if (results.IncludeDetails.IsFlagSet(BlobListingDetails.Copy) && realBlob.CopyState != null)
                    {
                        writer.WriteElementStringIfNotNull("CopyId", realBlob.CopyState.CopyId);
                        writer.WriteElementStringIfNotEnumValue("CopyStatus", realBlob.CopyState.Status, CopyStatus.Invalid);
                        writer.WriteElementStringIfNotNull("CopySource", realBlob.CopyState.Source.ToString());
                        writer.WriteElementString("CopyProgress", String.Format("{0}/{1}", realBlob.CopyState.BytesCopied, realBlob.CopyState.TotalBytes));
                        writer.WriteElementStringIfNotNull("CopyCompletionTime", realBlob.CopyState.CompletionTime);
                        writer.WriteElementStringIfNotNull("CopyStatusDescription", realBlob.CopyState.StatusDescription);
                    }
                    writer.WriteEndElement();       // Properties
                    if (results.IncludeDetails.IsFlagSet(BlobListingDetails.Metadata))
                    {
                        writer.WriteStartElement("Metadata");
                        foreach (var metadataItem in realBlob.Metadata)
                        {
                            writer.WriteElementString(metadataItem.Key, metadataItem.Value);
                        }
                        writer.WriteEndElement();   // Metadata
                    }
                    writer.WriteEndElement();       // Blob
                }
                else if (blob is CloudBlobDirectory)
                {
                    writer.WriteStartElement("BlobPrefix");
                    writer.WriteElementString("Name", ((CloudBlobDirectory)blob).Prefix);
                    writer.WriteEndElement();
                }
                else
                {
                    System.Diagnostics.Debug.Assert(false, "Unexpected blob listing item");
                }
            }
            writer.WriteEndElement();           // Blobs
            if (nextBlob != null)
            {
                writer.WriteElementString("NextMarker", BlobListHandler.GetMarkerForBlob(nextBlob));
            }
            writer.WriteEndElement();           // EnumerationResults
        }

        static void SerializeAccessPolicies(XmlWriter writer, SharedAccessBlobPolicies policies)
        {
            writer.WriteStartElement("SignedIdentifiers");
            foreach (KeyValuePair<string, SharedAccessBlobPolicy> pair in policies)
            {
                writer.WriteStartElement("SignedIdentifier");
                writer.WriteElementString("Id", pair.Key);
                writer.WriteStartElement("AccessPolicy");
                if (pair.Value.SharedAccessStartTime != null)
                {
                    DateTimeOffset start = (DateTimeOffset)pair.Value.SharedAccessStartTime;
                    writer.WriteElementString("Start", start.ToString("s") + "Z");
                }
                else
                {
                    writer.WriteElementString("Start", "");
                }
                if (pair.Value.SharedAccessExpiryTime != null)
                {
                    DateTimeOffset expiry = (DateTimeOffset)pair.Value.SharedAccessExpiryTime;
                    writer.WriteElementString("Expiry", expiry.ToString("s") + "Z");
                }
                else
                {
                    writer.WriteElementString("Expiry", "");
                }
                var permissions = pair.Value.Permissions;
                string permString = "";
                if (permissions.HasFlag(SharedAccessBlobPermissions.Delete))
                {
                    permString += "d";
                }
                if (permissions.HasFlag(SharedAccessBlobPermissions.List))
                {
                    permString += "l";
                }
                if (permissions.HasFlag(SharedAccessBlobPermissions.Read))
                {
                    permString += "r";
                }
                if (permissions.HasFlag(SharedAccessBlobPermissions.Write))
                {
                    permString += "w";
                }
                writer.WriteElementString("Permission", permString);
                writer.WriteEndElement(); // AccessPolicy
                writer.WriteEndElement(); // SignedIdentifier
            }
            writer.WriteEndElement(); // SignedIdentifiers
        }

        static bool IsSharedAccessPolicyXML(XmlDictionaryReader reader)
        {
            return reader.IsStartElement("SignedIdentifiers");
        }

        class SharedAccessBlobPolicyWithId
        {
            public SharedAccessBlobPolicyWithId()
            {
                this.Policy = new SharedAccessBlobPolicy();
            }

            public string Id { get; set; }
            public SharedAccessBlobPolicy Policy { get; set; }
        }

        static readonly Dictionary<char, SharedAccessBlobPermissions> _permissionsMap = new Dictionary<char, SharedAccessBlobPermissions>
        {
            { 'r', SharedAccessBlobPermissions.Read },
            { 'w', SharedAccessBlobPermissions.Write },
            { 'd', SharedAccessBlobPermissions.Delete },
            { 'l', SharedAccessBlobPermissions.List },
        };

        static SharedAccessBlobPolicies DeserializeAccessPolicies(XmlReader reader)
        {
            var policies = new List<SharedAccessBlobPolicyWithId>();

            reader.MoveToContent();
            ObjectDeserializer.ReadCollection(reader, policies, 
                (policy, name, value) =>
                {
                    if (name == "id")
                    {
                        policy.Id = value;
                    }
                },
                new Dictionary<string, Action<XmlReader, SharedAccessBlobPolicyWithId>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "AccessPolicy", (r, policy) => ObjectDeserializer.ReadObject(r, policy.Policy, (p, name, value) =>
                        {
                            switch (name)
                            {
                                case "start":
                                    if (!String.IsNullOrWhiteSpace(value))
                                    {
                                        p.SharedAccessStartTime = DateTimeOffset.Parse(value);
                                    }
                                    break;

                                case "expiry":
                                    if (!String.IsNullOrWhiteSpace(value))
                                    {
                                        p.SharedAccessExpiryTime = DateTimeOffset.Parse(value);
                                    }
                                    break;

                                case "permission":
                                    p.Permissions = ObjectDeserializer.TranslateEnumFlags(value, _permissionsMap, (a, f) => a | f, "Permission");
                                    break;
                            }
                        })},
                });

            var retval = new SharedAccessBlobPolicies();
            foreach (var policy in policies)
            {
                retval.Add(policy.Id, policy.Policy);
            }
            return retval;
        }
    }
}
