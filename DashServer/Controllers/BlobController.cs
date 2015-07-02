//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Controllers
{
    [RoutePrefix("Blob")]
    public class BlobController : CommonController
    {
        /// Get Blob - http://msdn.microsoft.com/en-us/library/azure/dd179440.aspx
        [HttpGet]
        public async Task<HttpResponseMessage> GetBlob(string container, string blob, string snapshot = null)
        {
            return await BasicBlobHandler(container, blob, null, StorageOperationTypes.GetBlob);
        }

        [HttpPut]
        public async Task<HttpResponseMessage> PutBlob(string container, string blob)
        {
            var requestWrapper = DashHttpRequestWrapper.Create(this.Request);
            var operation = StorageOperations.GetBlobOperation(requestWrapper);
            switch (operation)
            {
                case StorageOperationTypes.CopyBlob:
                    /// Copy Blob - https://msdn.microsoft.com/en-us/library/azure/dd894037.aspx
                    return ProcessResultResponse(await BlobHandler.CopyBlobAsync(requestWrapper, container, blob, requestWrapper.Headers.Value<string>("x-ms-copy-source")));

                case StorageOperationTypes.PutBlob:
                    /// Put Blob - http://msdn.microsoft.com/en-us/library/azure/dd179451.aspx
                    return await PutBlobHandler(container, blob, requestWrapper, operation);

                default:
                    System.Diagnostics.Debug.Assert(false);
                    return this.CreateResponse(HttpStatusCode.BadRequest, requestWrapper.Headers);
            }
        }

        /// Delete Blob - http://msdn.microsoft.com/en-us/library/azure/dd179413.aspx
        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteBlob(string container, string blob, string snapshot = null)
        {
            var requestWrapper = DashHttpRequestWrapper.Create(this.Request);
            var headers = requestWrapper.Headers;
            var queryParams = requestWrapper.QueryParameters;
            return await DoHandlerAsync(String.Format("BlobController.DeleteBlob: {0}/{1}", container, blob),
                async () => await NamespaceHandler.PerformNamespaceOperation(container, blob, async (namespaceBlob) =>
                {
                    // We only need to delete the actual blob. We are leaving the namespace entry alone as a sort of cache.
                    if (!(await namespaceBlob.ExistsAsync()) || namespaceBlob.IsMarkedForDeletion)
                    {
                        return this.CreateResponse(HttpStatusCode.NotFound, headers);
                    }
                    // Delete the real data blob by forwarding the request onto the data account
                    var forwardedResponse = await ForwardRequestHandler(namespaceBlob, StorageOperationTypes.DeleteBlob);
                    if (!forwardedResponse.IsSuccessStatusCode)
                    {
                        return forwardedResponse;
                    }
                    // See if we need to delete any replicas
                    if (BlobReplicationHandler.ShouldReplicateBlob(headers, namespaceBlob)) 
                    {
                        await BlobReplicationHandler.EnqueueBlobReplication(namespaceBlob, true, false);
                    }
                    // Mark the namespace blob for deletion
                    await namespaceBlob.MarkForDeletionAsync();
                    return this.CreateResponse(HttpStatusCode.Accepted, headers);
                }));
        }

        /// Get Blob Properties - http://msdn.microsoft.com/en-us/library/azure/dd179394.aspx
        [HttpHead]
        public async Task<HttpResponseMessage> GetBlobProperties(string container, string blob, string snapshot = null)
        {
            return await BasicBlobHandler(container, blob, null, StorageOperationTypes.GetBlobProperties);
        }

        /// Get Blob operations with the 'comp' query parameter
        [AcceptVerbs("GET", "HEAD")]
        public async Task<HttpResponseMessage> GetBlobComp(string container, string blob, string comp, string snapshot = null)
        {
            var requestWrapper = DashHttpRequestWrapper.Create(this.Request);
            var operation = StorageOperations.GetBlobOperation(requestWrapper);
            switch (operation)
            {
                case StorageOperationTypes.GetBlobMetadata:
                case StorageOperationTypes.GetBlockList:
                case StorageOperationTypes.GetPageRanges:
                    return await BasicBlobHandler(container, blob, requestWrapper, operation);

                default:
                    return this.CreateResponse(HttpStatusCode.BadRequest, requestWrapper.Headers);
            }
        }

        /// PUT Blob operations with the 'comp' query parameter
        [HttpPut]
        public async Task<HttpResponseMessage> PutBlobComp(string container, string blob, string comp)
        {
            var requestWrapper = DashHttpRequestWrapper.Create(this.Request);
            var operation = StorageOperations.GetBlobOperation(requestWrapper);
            switch (operation)
            {
                case StorageOperationTypes.SetBlobProperties:
                case StorageOperationTypes.SetBlobMetadata:
                case StorageOperationTypes.LeaseBlob:
                case StorageOperationTypes.SnapshotBlob:
                case StorageOperationTypes.PutPage:
                    return await BasicBlobHandler(container, blob, requestWrapper, operation);

                case StorageOperationTypes.PutBlock:
                case StorageOperationTypes.PutBlockList:
                    return await PutBlobHandler(container, blob, requestWrapper, operation);

                case StorageOperationTypes.AbortCopyBlob:
                    /// Abort Copy Blob - http://msdn.microsoft.com/en-us/library/azure/jj159098.aspx
                    return ProcessResultResponse(await BlobHandler.AbortCopyBlobAsync(
                        requestWrapper, container, blob, requestWrapper.QueryParameters.Value<string>("copyid")));

                default:
                    return this.CreateResponse(HttpStatusCode.BadRequest, requestWrapper.Headers);
            }
        }

        /// <summary>
        /// Generic function to forward blob request. Target blob must already exist.
        /// </summary>
        private async Task<HttpResponseMessage> BasicBlobHandler(string container, string blob, IHttpRequestWrapper requestWrapper, StorageOperationTypes operation)
        {
            return await DoHandlerAsync(String.Format("BlobController.BasicBlobHandler: {0} {1}/{2}", operation, container, blob),
                async () =>
                {
                    var namespaceBlob = await NamespaceHandler.FetchNamespaceBlobAsync(container, blob);
                    if (!await namespaceBlob.ExistsAsync())
                    {
                        return this.CreateResponse(HttpStatusCode.NotFound, (RequestHeaders)null);
                    }
                    if (BlobReplicationHandler.DoesOperationTriggerReplication(operation) &&
                        BlobReplicationHandler.ShouldReplicateBlob(requestWrapper.Headers, namespaceBlob))
                    {
                        await BlobReplicationHandler.EnqueueBlobReplication(namespaceBlob, false);
                    }
                    return await ForwardRequestHandler(namespaceBlob, operation);
                });
        }

        private async Task<HttpResponseMessage> PutBlobHandler(string container, string blob, IHttpRequestWrapper requestWrapper, StorageOperationTypes operation)
        {
            return await DoHandlerAsync(String.Format("BlobController.PutBlobHandler: {0} {1}/{2}", operation, container, blob),
                async () =>
                {
                    var namespaceBlob = await NamespaceHandler.CreateNamespaceBlobAsync(container, blob);
                    if (BlobReplicationHandler.DoesOperationTriggerReplication(operation) &&
                        BlobReplicationHandler.ShouldReplicateBlob(requestWrapper.Headers, namespaceBlob))
                    {
                        await BlobReplicationHandler.EnqueueBlobReplication(namespaceBlob, false);
                    }
                    return await ForwardRequestHandler(namespaceBlob, operation);
                });
        }

        static readonly HashSet<string> _noCopyHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Authorization",
            "Host",
            "x-original-url",
            "Expect",
        };
        private async Task<HttpResponseMessage> ForwardRequestHandler(NamespaceBlob namespaceBlob, StorageOperationTypes operation)
        {
            // Clone the inbound request
            var sourceRequest = this.Request;
            var clonedRequest = new HttpRequestMessage(sourceRequest.Method,
                ControllerOperations.GetRedirectUri(sourceRequest.RequestUri,
                    sourceRequest.Method.Method,
                    DashConfiguration.GetDataAccountByAccountName(namespaceBlob.SelectDataAccount),
                    namespaceBlob.Container,
                    namespaceBlob.BlobName, 
                    false));
            clonedRequest.Version = sourceRequest.Version;
            foreach (var property in sourceRequest.Properties)
            {
                clonedRequest.Properties.Add(property);
            }
            foreach (var header in sourceRequest.Headers)
            {
                if (!_noCopyHeaders.Contains(header.Key))
                {
                    clonedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            // Depending on the operation, we have to do some fixup to unwind HttpRequestMessage a bit - we also have to fixup some responses
            switch (operation)
            {
                case StorageOperationTypes.GetBlob:
                case StorageOperationTypes.GetBlobMetadata:
                case StorageOperationTypes.GetBlobProperties:
                case StorageOperationTypes.GetBlockList:
                case StorageOperationTypes.GetPageRanges:
                case StorageOperationTypes.LeaseBlob:
                case StorageOperationTypes.SetBlobMetadata:
                case StorageOperationTypes.SetBlobProperties:
                case StorageOperationTypes.SnapshotBlob:
                    // Push any headers that are assigned to Content onto the request itself as these operations do not have any body
                    if (sourceRequest.Content != null)
                    {
                        foreach (var header in sourceRequest.Content.Headers)
                        {
                            clonedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                    break;

                default:
                    clonedRequest.Content = sourceRequest.Content;
                    break;
            }
            var client = new HttpClient();
            var response = await client.SendAsync(clonedRequest, HttpCompletionOption.ResponseHeadersRead);
            // Fixup response for HEAD requests
            switch (operation)
            {
                case StorageOperationTypes.GetBlobProperties:
                    var content = response.Content;
                    if (response.IsSuccessStatusCode && content != null)
                    {
                        string mediaType = null;
                        string dummyContent = String.Empty;
                        if (content.Headers.ContentType != null)
                        {
                            mediaType = content.Headers.ContentType.MediaType;
                        }
                        // For some reason, a HEAD request requires some content otherwise the Content-Length is set to 0
                        dummyContent = "A";
                        response.Content = new StringContent(dummyContent, null, mediaType);
                        foreach (var header in content.Headers)
                        {
                            response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                        response.Content.Headers.ContentLength = content.Headers.ContentLength;
                        content.Dispose();
                    }
                    break;
            }
            return response;
        }
    }
}
