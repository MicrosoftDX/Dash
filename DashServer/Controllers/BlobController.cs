//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.Dash.Common.Utils;

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
            var requestWrapper = DashHttpRequestWrapper.Create(this.Request);
            if (requestWrapper.Headers.Contains("x-ms-copy-source"))
            {
                return ProcessHandlerResult(await BlobHandler.CopyBlobAsync(requestWrapper, container, blob, requestWrapper.Headers.Value<string>("x-ms-copy-source")));
            }
            else
            {
                return await PutBlobHandler(container, blob);
            }
        }

        /// Delete Blob - http://msdn.microsoft.com/en-us/library/azure/dd179413.aspx
        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteBlob(string container, string blob, string snapshot = null)
        {
            return await DoHandlerAsync(String.Format("BlobController.DeleteBlob: {0}/{1}", container, blob),
                async () => await ControllerOperations.PerformNamespaceOperation(container, blob, async (namespaceBlob) =>
                {
                    //We only need to delete the actual blob. We are leaving the namespace entry alone as a sort of cache.
                    if (!(await namespaceBlob.ExistsAsync()) || namespaceBlob.IsMarkedForDeletion)
                    {
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                    }
                    // Delete the real data blob
                    var dataBlob = ControllerOperations.GetBlobByName(DashConfiguration.GetDataAccountByAccountName(namespaceBlob.AccountName), container, blob);
                    await dataBlob.DeleteIfExistsAsync();
                    // Mark the namespace blob for deletion
                    await namespaceBlob.MarkForDeletionAsync();

                    return new HttpResponseMessage(HttpStatusCode.Accepted);
                }));
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

                case "pagelist":
                    return await GetBlobPageRanges(container, blob, snapshot);

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

                case "copy":
                    return await AbortCopyBlob(container, blob);

                case "page":
                    return await PutBlobPage(container, blob);

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

        /// Abort Copy Blob - http://msdn.microsoft.com/en-us/library/azure/jj159098.aspx
        private async Task<IHttpActionResult> AbortCopyBlob(string container, string blob)
        {
            var requestWrapper = DashHttpRequestWrapper.Create(this.Request);
            return ProcessHandlerResult(await BlobHandler.AbortCopyBlobAsync(requestWrapper, container, blob, requestWrapper.QueryParameters.Value<string>("copyid")));
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

        /// Get Page Ranges - https://msdn.microsoft.com/en-us/library/azure/ee691973.aspx 
        private async Task<IHttpActionResult> GetBlobPageRanges(string container, string blob, string snapshot)
        {
            return await BasicBlobHandler(container, blob);
        }

        /// Put Page - https://msdn.microsoft.com/en-us/library/azure/ee691975.aspx 
        private async Task<IHttpActionResult> PutBlobPage(string container, string blob)
        {
            return await BasicBlobHandler(container, blob);
        }

        /// <summary>
        /// Generic function to redirect a put request for properties of a blob
        /// </summary>
        private async Task<IHttpActionResult> BasicBlobHandler(string container, string blob)
        {
            var result = await BlobHandler.BasicBlobAsync(container, blob);
            return ProcessHandlerResult(result);
        }

        private async Task<IHttpActionResult> PutBlobHandler(string container, string blob)
        {
            var result = await BlobHandler.PutBlobAsync(container, blob);
            return ProcessHandlerResult(result);
        }
    }
}
