//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Controllers
{
    //[RoutePrefix("Container")]
    public class ContainerController : CommonController
    {

        /// Put Container - http://msdn.microsoft.com/en-us/library/azure/dd179468.aspx
        [HttpPut]
        public async Task<HttpResponseMessage> CreateContainer(string container)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            //Create Master Container
            CloudBlobContainer masterContainer = GetContainerByName(masterAccount, container);
            await masterContainer.CreateIfNotExistsAsync();

            //if the creation of a namespace container is successful we create all containers in other partition storage accounts
            int numOfAccounts = NumOfAccounts();
            for (int currAccount = 0; currAccount < numOfAccounts; currAccount++)
            {
                CreateChildContainer(currAccount, masterAccount, container);
            }

            return new HttpResponseMessage(HttpStatusCode.Created);
        }

        // Put Container operations, with 'comp' parameter'
        [HttpPut]
        public async Task<IHttpActionResult> PutContainerData(string container, string comp)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            Uri forwardUri = GetForwardingUri(RequestFromContext(HttpContext.Current), masterAccount.Credentials.AccountName, masterAccount.Credentials.ExportBase64EncodedKey(), container);
            HttpClient client = new HttpClient();
            HttpRequestBase request = RequestFromContext(HttpContext.Current);
            HttpRequestMessage request2 = new HttpRequestMessage(HttpMethod.Put, forwardUri);
            request2.Content = Request.Content;
            return Ok();
            //foreach (var header in Request.Headers)
            //{
            //    request2.Headers.Add(header.Name, header.Value);
            //}

            //await client.SendAsync(request2);
        }

        /// Delete Container - http://msdn.microsoft.com/en-us/library/azure/dd179408.aspx
        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteContainer(string container)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            //Delete Master Container
            CloudBlobContainer masterContainer = GetContainerByName(masterAccount, container);
            await masterContainer.DeleteAsync();

            //if the deletion of a namespace container is successfull we delete all containers in other partition storage accounts
            int numOfAccounts = NumOfAccounts();

            //going through all storage accounts to create same container in all of them
            for (int currAccount = 0; currAccount < numOfAccounts; currAccount++)
            {
                DeleteChildContainer(masterAccount, currAccount, container);
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        [AcceptVerbs("GET", "HEAD")]
        //Get Container operations, with optional 'comp' parameter
        public async Task<HttpResponseMessage> GetContainerData(string container, string comp=null)
        {
            string compvar = comp == null ? "" : comp;
            switch (compvar.ToLower())
            {
                case "list":
                    return await GetBlobList(container);
                default:
                    CloudStorageAccount masterAccount = GetMasterAccount();
                    Uri forwardUri = GetForwardingUri(RequestFromContext(HttpContext.Current), masterAccount.Credentials.AccountName, masterAccount.Credentials.ExportBase64EncodedKey(), container);
                    CloudBlobContainer containerObj = GetContainerByName(masterAccount, container);
                    await containerObj.FetchAttributesAsync();
                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Headers.Add("ETag", containerObj.Properties.ETag);
                    //response.Headers.Add("Last-Modified", "foo");

                    return response;
            }
            
        }

        private async Task<HttpResponseMessage> GetBlobList(string container)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();
            int numOfAccounts = NumOfAccounts();
            List<string> blobs = new List<string>();
            for (int currAccount = 0; currAccount < numOfAccounts; currAccount++)
            {
                blobs = blobs.Concat(await ChildBlobList(masterAccount, currAccount, container)).ToList();
            }
            string xmlresponse = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
            xmlresponse += "<EnumerationResults ServiceEndpoint=\"\" ContainerName=\"" + container + "\">";
            //TODO: Need to figure out the appropriate values for ServiceEndpoint, Prefix, Marker, MaxResults, and Delimiter
            xmlresponse += xmlEntry("Prefix", "");
            xmlresponse += xmlEntry("Marker", "");
            xmlresponse += xmlEntry("MaxResults", "");
            xmlresponse += xmlEntry("Delimiter", "");
            xmlresponse += xmlEntry("Blobs", String.Join("", blobs.ToArray()));
            xmlresponse += "</EnumerationResults>";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(xmlresponse);
            //TODO: Add headers for Content-Type, x-ms-request-id, x-ms-version, and Date
            return response;
        }

        private Dictionary<string, string> BlobProperties(CloudBlockBlob item)
        {
            Dictionary<string, string> blobDict = new Dictionary<string, string>();
            blobDict.Add("Last-Modified", item.Properties.LastModified.ToString());
            blobDict.Add("Etag", item.Properties.ETag);
            // Missing - how to get Content Length?
            blobDict.Add("Content-Type", item.Properties.ContentType);
            blobDict.Add("Content-Encoding", item.Properties.ContentEncoding);
            blobDict.Add("Content-Language", item.Properties.ContentLanguage);
            blobDict.Add("Content-MD5", item.Properties.ContentMD5);
            blobDict.Add("Cache-Control", item.Properties.CacheControl);
            blobDict.Add("x-ms-blob-sequence-number", item.Properties.PageBlobSequenceNumber.ToString());
            blobDict.Add("LeaseStatus", item.Properties.LeaseStatus.ToString());
            blobDict.Add("LeaseState", item.Properties.LeaseState.ToString());
            blobDict.Add("LeaseDuration", item.Properties.LeaseDuration.ToString());
            if (item.CopyState != null)
            {
                blobDict.Add("CopyId", item.CopyState.CopyId);
                blobDict.Add("CopyStatus", item.CopyState.Status.ToString());
                blobDict.Add("CopySource", item.CopyState.Source.ToString());
                blobDict.Add("CopyProgress", item.CopyState.BytesCopied.ToString());
                blobDict.Add("CopyCompletionTime", item.CopyState.CompletionTime.ToString());
                blobDict.Add("CopyStatusDescription", item.CopyState.StatusDescription);
            }
            
            return blobDict;
        }

        private async Task<string> GetBlobXml(CloudBlockBlob item)
        {
            await item.FetchAttributesAsync();
            Dictionary<string, string> propDict = BlobProperties(item);
            string xml = "<Blob>";
            xml += xmlEntry("Name", item.Name);
            xml += xmlEntry("Snapshot", item.SnapshotTime.ToString());
            xml += "<Properties>";
            foreach(KeyValuePair<string, string> pair in propDict) {
                xml += xmlEntry(pair.Key, pair.Value);
            }
            xml += "</Properties>";
            xml += "<Metadata>";
            foreach (KeyValuePair<string, string> pair in item.Metadata)
            {
                xml += xmlEntry(pair.Key, pair.Value);
            }
            xml += "</Metadata>";
            xml += "</Blob>";
            return xml;
        }

        private string xmlEntry(string key, string value)
        {
            return "<" + key + ">" + value + "</" + key + ">";
        }

        private void CreateChildContainer(int currAccount, CloudStorageAccount masterAccount, string container)
        {
            string accountName = "";
            string accountKey = "";

            base.readAccountData(masterAccount, currAccount, out accountName, out accountKey);

            //get container reference
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            var blobContainer = GetContainerByName(account, container);

            blobContainer.CreateIfNotExists();
        }

        private void DeleteChildContainer(CloudStorageAccount masterAccount, int currAccount, string container)
        {
            string accountName = "";
            string accountKey = "";

            base.readAccountData(masterAccount, currAccount, out accountName, out accountKey);

            //get container reference
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            var blobContainer = GetContainerByName(account, container);

            blobContainer.DeleteIfExists();
        }

        private async Task<List<string>> ChildBlobList(CloudStorageAccount masterAccount, int currAccount, string container)
        {
            string accountName = "";
            string accountKey = "";

            base.readAccountData(masterAccount, currAccount, out accountName, out accountKey);
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            CloudBlobContainer containerObj = GetContainerByName(account, container);
            List<string> blobs = new List<string>();
            IEnumerable<IListBlobItem> blobObjs = containerObj.ListBlobs();
            foreach (CloudBlockBlob item in blobObjs)
            {
                string blobXml = await GetBlobXml(item);
                blobs.Add(blobXml);
            }
            return blobs;
        }
    }
}
