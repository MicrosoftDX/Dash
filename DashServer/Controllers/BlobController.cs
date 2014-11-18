//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Dash.Server.Handlers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Blob")]
    public class BlobController : CommonController
    {
        /// Get Blob - http://msdn.microsoft.com/en-us/library/azure/dd179440.aspx
        [HttpGet]
        public async Task<IHttpActionResult> GetBlob(string container, string blob, string snapshot = null)
        {
            return await BasicBlobHandler(container, blob);
        }

        /// Put Blob - http://msdn.microsoft.com/en-us/library/azure/dd179451.aspx
        [HttpPut]
        public async Task<IHttpActionResult> PutBlob(string container, string blob)
        {
            return await PutBlobHandler(container, blob);
        }

        /// Delete Blob - http://msdn.microsoft.com/en-us/library/azure/dd179413.aspx
        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteBlob(string container, string blob, string snapshot = null)
        {
            //We only need to delete the actual blob. We are leaving the namespace entry alone as a sort of cache.
            var namespaceBlob = new NamespaceBlob(ControllerOperations.GetBlobByName(DashConfiguration.NamespaceAccount, container, blob));
            if (!await namespaceBlob.ExistsAsync())
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            // Delete the real data blob
            var dataBlob = ControllerOperations.GetBlobByName(DashConfiguration.GetDataAccountByAccountName(namespaceBlob.AccountName), container, blob);
            await dataBlob.DeleteAsync();
            // Mark the namespace blob for deletion
            await namespaceBlob.MarkForDeletionAsync();

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        /// Get Blob Properties - http://msdn.microsoft.com/en-us/library/azure/dd179394.aspx
        [HttpHead]
        public async Task<IHttpActionResult> GetBlobProperties(string container, string blob, string snapshot = null)
        {
            return await BasicBlobHandler(container, blob);
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
            return await BasicBlobHandler(container, blob);
        }

        /// Get Blob Metadata - http://msdn.microsoft.com/en-us/library/azure/dd179350.aspx
        private async Task<IHttpActionResult> GetBlobMetadata(string container, string blob, string snapshot)
        {
            return await BasicBlobHandler(container, blob);
        }

        /// Set Blob Metadata - http://msdn.microsoft.com/en-us/library/azure/dd179414.aspx
        private async Task<IHttpActionResult> SetBlobMetadata(string container, string blob)
        {
            return await BasicBlobHandler(container, blob);
        }

        /// Lease Blob - http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
        private async Task<IHttpActionResult> LeaseBlob(string container, string blob)
        {
            return await BasicBlobHandler(container, blob);
        }

        /// Snapshot Blob - http://msdn.microsoft.com/en-us/library/azure/ee691971.aspx
        private async Task<IHttpActionResult> SnapshotBlob(string container, string blob)
        {
            // This will need to be some variation of copy. May need to replicate snapshotting logic in case
            // original server is out of space.
            return await BasicBlobHandler(container, blob);
        }

        /// Get Block List - http://msdn.microsoft.com/en-us/library/azure/dd179400.aspx
        private async Task<IHttpActionResult> GetBlobBlockList(string container, string blob, string snapshot)
        {
            return await BasicBlobHandler(container, blob);
        }

        /// Put Block - http://msdn.microsoft.com/en-us/library/azure/dd135726.aspx
        private async Task<IHttpActionResult> PutBlobBlock(string container, string blob)
        {
            return await PutBlobHandler(container, blob);
        }

        /// Put Block List - http://msdn.microsoft.com/en-us/library/azure/dd179467.aspx
        private async Task<IHttpActionResult> PutBlobBlockList(string container, string blob)
        {
            return await PutBlobHandler(container, blob);
        }

        /// <summary>
        /// Generic function to redirect a put request for properties of a blob
        /// </summary>
        private async Task<IHttpActionResult> BasicBlobHandler(string container, string blob)
        {
            var namespaceBlob = await ControllerOperations.FetchNamespaceBlobAsync(container, blob);
            if (!await namespaceBlob.ExistsAsync())
            {
                return NotFound();
            }
            HttpRequestBase request = RequestFromContext(HttpContextFactory.Current);
            return Redirect(ControllerOperations.GetRedirectUri(request,
                DashConfiguration.GetDataAccountByAccountName(namespaceBlob.AccountName),
                namespaceBlob.Container,
                namespaceBlob.BlobName));
        }

        private async Task<IHttpActionResult> PutBlobHandler(string container, string blob)
        {
            HttpRequestBase request = RequestFromContext(HttpContextFactory.Current);
            var namespaceBlob = await ControllerOperations.CreateNamespaceBlobAsync(request, container, blob);
            //redirection code
            Uri redirect = ControllerOperations.GetRedirectUri(request, DashConfiguration.GetDataAccountByAccountName(namespaceBlob.AccountName), container, blob);
            return Redirect(redirect);
        }

    }
}
