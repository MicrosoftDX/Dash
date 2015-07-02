﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
            xmlFormatter.SetSerializer<EnumerationResults>(new ObjectSerializer<EnumerationResults>(ContainerController.SerializeBlobListing));
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
            var formatter = GlobalConfiguration.Configuration.Formatters.XmlFormatter;
            var stream = await Request.Content.ReadAsStreamAsync();
            SharedAccessBlobPolicies policies = (SharedAccessBlobPolicies)await formatter.ReadFromStreamAsync(typeof(SharedAccessBlobPolicies), stream, null, null);
            string accessLevel = Request.Headers.GetValues("x-ms-blob-public-access").FirstOrDefault();
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
            foreach (var policy in policies)
            {
                perms.SharedAccessPolicies.Add(policy);
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
            response.Headers.Add("x-ms-version", Request.Headers.GetValues("x-ms-version"));
            response.Headers.Date = DateTimeOffset.UtcNow;
        }

        private async Task<HttpResponseMessage> GetBlobList(string container)
        {
            // Extract query parameters
            var queryParams = this.Request.GetQueryParameters();
            var prefix = queryParams.Value<string>("prefix");
            var delim = queryParams.Value<string>("delimiter");
            var marker = queryParams.Value<string>("marker");
            var indicatedMaxResults = queryParams.Value<string>("maxresults");
            var maxResults = queryParams.Value("maxresults", 5000);
            var includedDataSets = String.Join(",", queryParams.Values<string>("include"));

            var blobTasks = DashConfiguration.DataAccounts
                .Select(account => ChildBlobListAsync(account, container, prefix, delim, includedDataSets));
            var namespaceTask = ChildBlobListAsync(DashConfiguration.NamespaceAccount, container, prefix, delim, BlobListingDetails.Metadata.ToString());
            var blobs = await Task.WhenAll(blobTasks);
            var namespaceBlobs = await namespaceTask;
            var sortedBlobs = blobs
                .SelectMany(blobList => blobList)
                .OrderBy(blob => blob.Uri.AbsolutePath, StringComparer.Ordinal)  
                .SkipWhile(blob => !String.IsNullOrWhiteSpace(marker) && GetMarkerForBlob(blob) != marker);
            var sortedNamespace = namespaceBlobs
                .OrderBy(blob => blob.Uri.AbsolutePath, StringComparer.Ordinal)  
                .SkipWhile(blob => !String.IsNullOrWhiteSpace(marker) && GetMarkerForBlob(blob) != marker);
            var resultsList = sortedBlobs
                .Join(sortedNamespace,
                    blob => blob.Uri.AbsolutePath,
                    blob => blob.Uri.AbsolutePath,
                    (dataBlob, namespaceBlob) => Tuple.Create(dataBlob, namespaceBlob),
                    StringComparer.OrdinalIgnoreCase)
                .Where(blobPair => MatchPrimaryDataBlob(blobPair.Item2 as CloudBlockBlob, blobPair.Item1 as ICloudBlob))
                .Take(maxResults + 1)                  // Get an extra listing so that we can generate the nextMarker
                .Select(blobPair => blobPair.Item1);
            var blobResults = new EnumerationResults
            {
                RequestVersion = this.Request.GetHeaders().Value("x-ms-version", StorageServiceVersions.Version_2009_09_19),
                ServiceEndpoint = this.Request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped),
                ContainerName = container,
                MaxResults = maxResults,
                IndicatedMaxResults = indicatedMaxResults,
                Delimiter = delim,
                Marker = marker,
                Prefix = prefix,
                Blobs = resultsList,
                IncludeDetails = String.IsNullOrWhiteSpace(includedDataSets) ? BlobListingDetails.None : (BlobListingDetails)Enum.Parse(typeof(BlobListingDetails), includedDataSets, true),
            };
            return CreateResponse(blobResults);
        }

        private async Task<IEnumerable<IListBlobItem>> ChildBlobListAsync(CloudStorageAccount dataAccount, string container, string prefix, string delimiter, string includeFlags)
        {
            CloudBlobContainer containerObj = NamespaceHandler.GetContainerByName(dataAccount, container);
            if (!String.IsNullOrWhiteSpace(delimiter))
            {
                containerObj.ServiceClient.DefaultDelimiter = delimiter;
            }
            var results = new List<IEnumerable<IListBlobItem>>();
            BlobListingDetails listDetails;
            Enum.TryParse(includeFlags, true, out listDetails);
            string nextMarker = null;
            try
            {
                do
                {
                    var continuationToken = new BlobContinuationToken
                    {
                        NextMarker = nextMarker,
                    };
                    var blobResults = await containerObj.ListBlobsSegmentedAsync(prefix, String.IsNullOrWhiteSpace(delimiter), listDetails, null, continuationToken, null, null);
                    results.Add(blobResults.Results);
                    if (blobResults.ContinuationToken != null)
                    {
                        nextMarker = blobResults.ContinuationToken.NextMarker;
                    }
                    else
                    {
                        nextMarker = null;
                    }
                } while (!String.IsNullOrWhiteSpace(nextMarker));
            }
            catch (StorageException)
            {
                // Silently swallow the exception if we're missing the container for this account
                
            }

            return results
                .SelectMany(segmentResults => segmentResults);
        }

        static bool MatchPrimaryDataBlob(CloudBlockBlob namespaceEntry, ICloudBlob dataBlob)
        {
            // Handle listing of CloudBlobDirectory objects
            if (namespaceEntry == null || dataBlob == null)
            {
                return true;
            }
            // The blob listing included metadata for the namespace entry, so we don't need to refresh
            var namespaceBlob = new NamespaceBlob(namespaceEntry);
            if (namespaceBlob.IsReplicated)
            {
                return String.Equals(namespaceBlob.PrimaryAccountName, dataBlob.ServiceClient.Credentials.AccountName, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        class BlobComparer : IEqualityComparer<IListBlobItem>
        {
            public bool Equals(IListBlobItem lhs, IListBlobItem rhs)
            {
                if (lhs == null && rhs == null)
                {
                    return true;
                }
                else if (lhs == null)
                {
                    return false;
                }
                else if (!String.Equals(lhs.Uri.AbsolutePath, rhs.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                var lhsBlob = lhs as ICloudBlob;
                var rhsBlob = rhs as ICloudBlob;
                if (lhsBlob == null && rhsBlob == null)
                {
                    return true;
                }
                else if (lhsBlob == null)
                {
                    return false;
                }
                else if (lhsBlob.SnapshotTime != rhsBlob.SnapshotTime)
                {
                    return false;
                }
                return true;
            }

            public int GetHashCode(IListBlobItem obj)
            {
                var hash = obj.Uri.AbsolutePath.ToLowerInvariant().GetHashCode();
                if (obj is ICloudBlob)
                {
                    hash ^= ((ICloudBlob)obj).SnapshotTime.GetHashCode();
                }
                return hash;
            }
        }

        class EnumerationResults
        {
            public DateTimeOffset RequestVersion { get; set; }
            public string ServiceEndpoint { get; set; }
            public string ContainerName { get; set; }
            public string Prefix { get; set; }
            public string Marker { get; set; }
            public string IndicatedMaxResults { get; set; }
            public int MaxResults { get; set; }
            public string Delimiter { get; set; }
            public IEnumerable<IListBlobItem> Blobs { get; set; }
            public string NextMarker { get; set; }
            public BlobListingDetails IncludeDetails { get; set; }
        }

        static void SerializeBlobListing(XmlWriter writer, EnumerationResults results)
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
                        writer.WriteElementString("Snapshot", realBlob.SnapshotTime);
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
                        writer.WriteElementStringIfNotNull("CopyProgress", (realBlob.CopyState.BytesCopied / realBlob.CopyState.TotalBytes).ToString());
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
                writer.WriteElementString("NextMarker", GetMarkerForBlob(nextBlob));
            }
            writer.WriteEndElement();           // EnumerationResults
        }

        static string GetMarkerForBlob(IListBlobItem blob)
        {
            string markerValue;
            if (blob is ICloudBlob && ((ICloudBlob)blob).IsSnapshot)
            {
                markerValue = blob.Uri.AbsolutePath + "|" + ((ICloudBlob)blob).SnapshotTime.Value.ToString("o");
            }
            else 
            {
                markerValue = blob.Uri.AbsolutePath + "|";
            }
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(markerValue));
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

        static SharedAccessBlobPolicies DeserializeAccessPolicies(XmlReader reader)
        {
            SharedAccessBlobPolicies ret = new SharedAccessBlobPolicies();
            string signedIdentifier = "SignedIdentifier";
            string accessPolicy = "AccessPolicy";
            string wrapper = "SignedIdentifiers";
            reader.Read(); //Advance past xml declaration element
            reader.Read();
            while (!reader.EOF)
            {
                if (reader.Name == wrapper && !reader.IsStartElement())
                {
                    break; //Reached the end of the file
                }
                while (reader.Name != signedIdentifier && !reader.EOF)
                {
                    reader.Read(); //Advance to the next policy
                }
                if (reader.EOF)
                {
                    break;
                }
                string id = "";
                DateTimeOffset? start = new DateTimeOffset();
                DateTimeOffset? expiry = new DateTimeOffset();
                //Set permissions to None by default
                SharedAccessBlobPermissions permissionObj = 0;
                reader.Read(); //Go past start tag.
                // Keep reading until we reach the end of the identifier
                while (reader.Name != signedIdentifier)
                {
                    if (reader.Name == "Id")
                    {
                        reader.Read(); //Get to the value.
                        id = reader.Value;
                        reader.Read();
                    }
                    else if (reader.Name == accessPolicy)
                    {
                        reader.Read();
                        while (reader.Name != accessPolicy)
                        {
                            if (reader.Name == "Start")
                            {
                                reader.Read();
                                if (!string.IsNullOrWhiteSpace(reader.Value))
                                {
                                    start = DateTimeOffset.Parse(reader.Value);
                                }
                                else
                                {
                                    start = null;
                                }
                                
                                reader.Read();
                            }
                            else if (reader.Name == "Expiry")
                            {
                                reader.Read();
                                if (!string.IsNullOrWhiteSpace(reader.Value))
                                {
                                    expiry = DateTimeOffset.Parse(reader.Value);
                                }
                                else
                                {
                                    expiry = null;
                                }
                                reader.Read();
                            }
                            else if (reader.Name == "Permission")
                            {
                                reader.Read();
                                string permissions = reader.Value;

                                if (permissions.Contains('r'))
                                {
                                    permissionObj |= SharedAccessBlobPermissions.Read;
                                }
                                if (permissions.Contains('w'))
                                {
                                    permissionObj |= SharedAccessBlobPermissions.Write;
                                }
                                if (permissions.Contains('d'))
                                {
                                    permissionObj |= SharedAccessBlobPermissions.Delete;
                                }
                                if (permissions.Contains('l'))
                                {
                                    permissionObj |= SharedAccessBlobPermissions.List;
                                }

                                reader.Read();
                            }
                            reader.Read();
                        }
                    }
                    reader.Read();
                }
                SharedAccessBlobPolicy policy = new SharedAccessBlobPolicy()
                {
                    SharedAccessStartTime = start,
                    SharedAccessExpiryTime = expiry,
                    Permissions = permissionObj
                };

                ret.Add(new KeyValuePair<string, SharedAccessBlobPolicy>(id, policy));
                reader.Read();
            }

            return ret;
        }
    }
}
