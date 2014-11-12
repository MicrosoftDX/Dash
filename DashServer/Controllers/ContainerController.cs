//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Xml;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Controllers
{
    //[RoutePrefix("Container")]
    public class ContainerController : CommonController
    {

        static ContainerController()
        {
            var xmlFormatter = GlobalConfiguration.Configuration.Formatters.XmlFormatter;
            xmlFormatter.WriterSettings.OmitXmlDeclaration = false;
            xmlFormatter.SetSerializer<EnumerationResults>(new ObjectSerializer<EnumerationResults>(ContainerController.SerializeBlobListing));
        }

        /// Put Container - http://msdn.microsoft.com/en-us/library/azure/dd179468.aspx
        [HttpPut]
        public async Task<HttpResponseMessage> CreateContainer(string containerName)
        {
            await DoForContainerInAllAccounts(containerName, async (container) => await container.CreateIfNotExistsAsync());
            return new HttpResponseMessage(HttpStatusCode.Created);
        }

        // Put Container operations, with 'comp' parameter'
        [HttpPut]
        public async Task<IHttpActionResult> PutContainerData(string containerName, string comp)
        {
            BlobContainerPermissions perms = null;
            var queryParams = this.Request.GetQueryParameters();
            switch (comp.ToLowerInvariant())
            {
                case "acl":
                    var signedIdentifiers = await this.Request.Content.ReadAsAsync<List<SignedIdentifier>>();
                    var publicAccessParam = queryParams["x-ms-blob-public-access"];
                    perms = new BlobContainerPermissions
                    {
                        PublicAccess = publicAccessParam == null || publicAccessParam.Length == 0 ? 
                            BlobContainerPublicAccessType.Off : 
                            (BlobContainerPublicAccessType)Enum.Parse(typeof(BlobContainerPublicAccessType), publicAccessParam),
                    };
                    foreach (var signedIdentifier in signedIdentifiers)
                    {
                        perms.SharedAccessPolicies.Add(signedIdentifier.Id, new SharedAccessBlobPolicy
                        {
                            SharedAccessStartTime = signedIdentifier.AccessPolicy.Start,
                            SharedAccessExpiryTime = signedIdentifier.AccessPolicy.Expiry,
                            Permissions = SharedAccessBlobPolicy.PermissionsFromString(signedIdentifier.AccessPolicy.Permission),
                        });
                    }
                    break;
            }

            await DoForContainerInAllAccounts(containerName, async (container) => {
                switch (comp.ToLowerInvariant())
                {
                    case "metadata":
                        // TODO: Copy metadata to all containers
                        break;

                    case "acl":
                        await container.SetPermissionsAsync(perms);
                        break;
                }
            });
            return Ok();
        }

        class SignedIdentifier
        {
            public string Id { get; set; }
            public AccessPolicyType AccessPolicy { get; set; }
        }

        class AccessPolicyType
        {
            public DateTimeOffset Start { get; set; }
            public DateTimeOffset Expiry { get; set; }
            public string Permission { get; set; }
        }

        /// Delete Container - http://msdn.microsoft.com/en-us/library/azure/dd179408.aspx
        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteContainer(string containerName)
        {
            await DoForContainerInAllAccounts(containerName, async (container) => await container.DeleteIfExistsAsync());
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        [AcceptVerbs("GET", "HEAD")]
        //Get Container operations, with optional 'comp' parameter
        public async Task<HttpResponseMessage> GetContainerData(string container, string comp = null)
        {
            CloudBlobContainer containerObj = GetContainerByName(DashConfiguration.NamespaceAccount, container);
            if (!await containerObj.ExistsAsync())
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            string compvar = comp == null ? "" : comp;
            switch (compvar.ToLower())
            {
                case "list":
                    return await GetBlobList(container);

                case "metadata":
                    break;

                case "acl":
                    break;

                default:
                    //Uri forwardUri = GetForwardingUri(RequestFromContext(HttpContext.Current), masterAccount.Credentials.AccountName, masterAccount.Credentials.ExportBase64EncodedKey(), container);
                    await containerObj.FetchAttributesAsync();
                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Headers.Add("ETag", containerObj.Properties.ETag);
                    //response.Headers.Add("Last-Modified", "foo");

                    return response;
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private async Task DoForContainerInAllAccounts(string container, Func<CloudBlobContainer, Task> action)
        {
            await action(GetContainerByName(DashConfiguration.NamespaceAccount, container));
            foreach (var account in DashConfiguration.DataAccounts)
            {
                await action(GetContainerByName(account, container));
            }
        }

        private async Task<HttpResponseMessage> GetBlobList(string container)
        {
            // Extract query parameters
            var queryParams = this.Request.GetQueryParameters();
            var prefix = queryParams["prefix"];
            var delim = queryParams["delimiter"];
            var marker = queryParams["marker"];
            var indicatedMaxResults = queryParams["maxresults"];
            var maxResults = queryParams.Value("maxresults", 5000);
            var includedDataSets = String.Join(",", queryParams.Values<string>("include"));

            var fetchBlobsTasks = DashConfiguration.DataAccounts
                .Where(account => account != null)
                .Select(async account => await ChildBlobList(account, container, prefix, String.IsNullOrWhiteSpace(delim), includedDataSets));
            var blobs = await Task.WhenAll(fetchBlobsTasks);
            var sortedBlobs = blobs
                .SelectMany(blobList => blobList)
                .OrderBy(blob => blob.Uri.AbsolutePath, StringComparer.Ordinal)                           
                .SkipWhile(blob => !String.IsNullOrWhiteSpace(marker) && GetMarkerForBlob(blob) != marker)
                .Take(maxResults + 1);                  // Get an extra listing so that we can generate the nextMarker
            var blobResults = new EnumerationResults
            {
                ServiceEndpoint = this.Request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped),
                ContainerName = container,
                MaxResults = maxResults,
                IndicatedMaxResults = indicatedMaxResults,
                Delimiter = delim,
                Marker = marker,
                Prefix = prefix,
                Blobs = sortedBlobs,
                IncludeDetails = String.IsNullOrWhiteSpace(includedDataSets) ? BlobListingDetails.None : (BlobListingDetails)Enum.Parse(typeof(BlobListingDetails), includedDataSets, true),
            };
            return CreateResponse(blobResults);
        }

        private async Task<IEnumerable<IListBlobItem>> ChildBlobList(CloudStorageAccount account, string container, string prefix, bool useFlatListing, string includeFlags)
        {
            CloudBlobContainer containerObj = GetContainerByName(account, container);
            var results = new List<IEnumerable<IListBlobItem>>();
            BlobListingDetails listDetails;
            Enum.TryParse(includeFlags, true, out listDetails);
            string nextMarker = null;
            do
            {
                var continuationToken = new BlobContinuationToken
                {
                    NextMarker = nextMarker,
                };
                var blobResults = await containerObj.ListBlobsSegmentedAsync(prefix, useFlatListing, listDetails, null, continuationToken, null, null);
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

            return results
                .SelectMany(segmentResults => segmentResults);
        }

        class EnumerationResults
        {
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
            writer.WriteStartElement("EnumerationResults");
            writer.WriteAttributeString("ServiceEndpoint", results.ServiceEndpoint);
            writer.WriteAttributeString("ContainerName", results.ContainerName);
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
                }
                else if (blob is ICloudBlob)
                {
                    var realBlob = (ICloudBlob)blob;
                    writer.WriteStartElement("Blob");
                    writer.WriteElementString("Name", realBlob.Name);
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
                    writer.WriteElementString("Cache-Disposition", realBlob.Properties.ContentDisposition);
                    writer.WriteElementStringIfNotNull("x-ms-blob-sequence-number", realBlob.Properties.PageBlobSequenceNumber);
                    writer.WriteElementStringIfNotEnumValue("BlobType", realBlob.Properties.BlobType, BlobType.Unspecified, false);
                    writer.WriteElementStringIfNotEnumValue("LeaseStatus", realBlob.Properties.LeaseStatus, LeaseStatus.Unspecified);
                    writer.WriteElementStringIfNotEnumValue("LeaseState", realBlob.Properties.LeaseState, LeaseState.Unspecified);
                    writer.WriteElementStringIfNotEnumValue("LeaseDuration", realBlob.Properties.LeaseDuration, LeaseDuration.Unspecified);
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
    }
}
