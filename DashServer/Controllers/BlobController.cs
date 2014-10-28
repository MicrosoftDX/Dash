//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Blob")]
    public class BlobController : CommonController
    {
        /// Get Blob - http://msdn.microsoft.com/en-us/library/azure/dd179440.aspx
        [HttpGet]
        public async Task<IHttpActionResult> GetBlob(string container, string blob, string snapshot = null)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            String accountName = "";
            String accountKey = "";
            Uri blobUri;
            HttpResponseMessage response;

            CloudBlockBlob namespaceBlob = GetBlobByName(masterAccount, container, blob);

            //Get blob metadata
            namespaceBlob.FetchAttributes();

            blobUri = new Uri(namespaceBlob.Metadata["link"]);
            accountName = namespaceBlob.Metadata["accountname"];
            accountKey = namespaceBlob.Metadata["accountkey"];

            response = new HttpResponseMessage();
            Uri redirect = GetRedirectUri(blobUri, accountName, accountKey, container, Request);

            return Redirect(redirect);
        }

        /// Put Blob - http://msdn.microsoft.com/en-us/library/azure/dd179451.aspx
        [HttpPut]
        public async Task<IHttpActionResult> PutBlob(string container, string blob)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            String accountName = "";
            String accountKey = "";
            Uri blobUri;
            String containerName = "";

            CreateNamespaceBlob(Request, masterAccount);

            ReadMetaData(masterAccount, container, blob, out blobUri, out accountName, out accountKey, out containerName);

            //redirection code
            Uri redirect = GetRedirectUri(blobUri, accountName, accountKey, container, Request);
            return Redirect(redirect);
        }

        /// Delete Blob - http://msdn.microsoft.com/en-us/library/azure/dd179413.aspx
        [HttpDelete]
        public async Task<IHttpActionResult> DeleteBlob(string container, string blob, string snapshot = null)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            String accountName = "";
            String accountKey = "";
            Uri blobUri;
            String containerName = "";

            // Set Namespace Blob for deletion
            //create a namespace blob with hardcoded metadata
            CloudBlockBlob namespaceBlob = GetBlobByName(masterAccount, container, blob);

            if (!namespaceBlob.Exists())
            {
                throw new FileNotFoundException("Namespace blob not found");
            }

            namespaceBlob.FetchAttributes();
            namespaceBlob.Metadata["todelete"] = "true";
            namespaceBlob.SetMetadata();

            ReadMetaData(masterAccount, container, blob, out blobUri, out accountName, out accountKey, out containerName);
            Uri redirect = GetRedirectUri(blobUri, accountName, accountKey, containerName, Request);

            CloudBlockBlob blobObj = GetBlobByName(masterAccount, container, blob);

            blobObj.Delete();

            return Redirect(redirect);
        }

        /// Get Blob Properties - http://msdn.microsoft.com/en-us/library/azure/dd179394.aspx
        [HttpHead]
        public async Task<IHttpActionResult> GetBlobProperties(string container, string blob, string snapshot = null)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();

            String accountName = "";
            String accountKey = "";
            Uri blobUri;
            String containerName = "";

            //reading metadata from namespace blob
            ReadMetaData(masterAccount, container, blob, out blobUri, out accountName, out accountKey, out containerName);
            Uri redirect = GetRedirectUri(blobUri, accountName, accountKey, containerName, Request);

            return Redirect(redirect);
        }

        /// Get Blob operations with the 'comp' query parameter
        [AcceptVerbs("GET", "HEAD")]
        public async Task<IHttpActionResult> GetBlobComp(string container, string blob, string comp, string snapshot = null)
        {
            switch (comp.ToLower())
            {
                case "metadata":
                    return await GetBlobMetadata(container, blob, snapshot);

                case "blocklist":
                    return await GetBlobBlockList(container, blob, snapshot);

                default:
                    return BadRequest();
            }
        }

        /// PUT Blob operations with the 'comp' query parameter
        [HttpPut]
        public async Task<IHttpActionResult> PutBlobComp(string container, string blob, string comp)
        {
            switch (comp.ToLower())
            {
                case "properties":
                    return await SetBlobProperties(container, blob);

                case "metadata":
                    return await SetBlobMetadata(container, blob);

                case "lease":
                    return await LeaseBlob(container, blob);

                case "snapshot":
                    return await SnapshotBlob(container, blob);

                case "block":
                    return await PutBlobBlock(container, blob);

                case "blocklist":
                    return await PutBlobBlockList(container, blob);

                default:
                    return BadRequest();
            }
        }

        /// Set Blob Properties - http://msdn.microsoft.com/en-us/library/azure/ee691966.aspx
        private async Task<IHttpActionResult> SetBlobProperties(string container, string blob)
        {
            await Task.Delay(10);
            return Ok();
        }

        /// Get Blob Metadata - http://msdn.microsoft.com/en-us/library/azure/dd179350.aspx
        private async Task<IHttpActionResult> GetBlobMetadata(string container, string blob, string snapshot)
        {
            await Task.Delay(10);
            return Ok();
        }

        /// Set Blob Metadata - http://msdn.microsoft.com/en-us/library/azure/dd179414.aspx
        private async Task<IHttpActionResult> SetBlobMetadata(string container, string blob)
        {
            await Task.Delay(10);
            return Ok();
        }

        /// Lease Blob - http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
        private async Task<IHttpActionResult> LeaseBlob(string container, string blob)
        {
            await Task.Delay(10);
            return Ok();
        }

        /// Snapshot Blob - http://msdn.microsoft.com/en-us/library/azure/ee691971.aspx
        private async Task<IHttpActionResult> SnapshotBlob(string container, string blob)
        {
            await Task.Delay(10);
            return Ok();
        }

        /// Get Block List - http://msdn.microsoft.com/en-us/library/azure/dd179400.aspx
        private async Task<IHttpActionResult> GetBlobBlockList(string container, string blob, string snapshot)
        {
            await Task.Delay(10);
            return Ok();
        }

        /// Put Block - http://msdn.microsoft.com/en-us/library/azure/dd135726.aspx
        private async Task<IHttpActionResult> PutBlobBlock(string container, string blob)
        {
            await Task.Delay(10);
            return Ok();
        }

        /// Put Block List - http://msdn.microsoft.com/en-us/library/azure/dd179467.aspx
        private async Task<IHttpActionResult> PutBlobBlockList(string container, string blob)
        {
            await Task.Delay(10);
            return Ok();
        }
    }
}
