//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Handlers
{
    public static class StorageRequestHelpers
    {
        const string MetadataPrefix = "x-ms-meta-";

        public static void AddStandardResponseHeaders(this HttpResponseMessage response, RequestHeaders requestHeaders)
        {
            //Right now we are just parroting back the values sent by the client. Might need to generate our
            //own values for these if none are provided.
            string clientId = requestHeaders.Value<string>("x-ms-client-request-id");
            if (!String.IsNullOrWhiteSpace(clientId))
            {
                response.Headers.Add("x-ms-request-id", clientId);
            }
            response.Headers.Add("x-ms-version", requestHeaders.Value("x-ms-version", StorageServiceVersions.Version_2014_02_14.ToVersionString()));
            response.Headers.Date = DateTimeOffset.UtcNow;
        }

        public static bool ReadRequestMetadata(this RequestHeaders requestHeaders, IDictionary<string, string> metadata)
        {
            bool updated = false;
            foreach (var metadatum in requestHeaders.Where(header => header.Key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                string key = metadatum.Key.Substring(MetadataPrefix.Length);
                string value = metadatum.First();
                metadata.Add(new KeyValuePair<string, string>(key, value));
                updated = true;
            }
            return updated;
        }

        public static void AddMetadataResponseHeaders(this HttpResponseMessage response, IDictionary<string, string> metadata)
        {
            foreach (var metadataEntry in metadata)
            {
                response.Headers.Add(MetadataPrefix + metadataEntry.Key, metadataEntry.Value);
            }
        }

        public static bool SetAttributeFromRequest<T>(this RequestResponseItems requestItems, string attributeName, Action<T> setAttribute) where T : IConvertible
        {
            if (requestItems.Contains(attributeName))
            {
                setAttribute(requestItems.Value<T>(attributeName));
                return true;
            }
            return false;
        }

        public static bool SetAttributeFromRequestWithAltName<T>(this RequestResponseItems requestItems, string attributeName1, string attributeName2, Action<T> setAttribute) where T : IConvertible
        {
            if (!requestItems.SetAttributeFromRequest(attributeName1, setAttribute))
            {
                return requestItems.SetAttributeFromRequest(attributeName2, setAttribute);
            }
            return true;
        }

        public static AccessCondition CreateAccessCondition(this RequestHeaders requestHeaders, bool includeSequenceConditions = false)
        {
            var retval = AccessCondition.GenerateEmptyCondition();
            requestHeaders.SetAttributeFromRequest("If-Modified-Since", (DateTime modifiedDate) => retval.IfModifiedSinceTime = modifiedDate);
            requestHeaders.SetAttributeFromRequest("If-Unmodified-Since", (DateTime modifiedDate) => retval.IfNotModifiedSinceTime = modifiedDate);
            requestHeaders.SetAttributeFromRequest("If-Match", (string eTag) => retval.IfMatchETag = eTag);
            requestHeaders.SetAttributeFromRequest("If-None-Match", (string eTag) => retval.IfNoneMatchETag = eTag);
            requestHeaders.SetAttributeFromRequest("x-ms-lease-id", (string leaseId) => retval.LeaseId = leaseId);
            if (includeSequenceConditions)
            {
                requestHeaders.SetAttributeFromRequest("x-ms-if-sequence-number-le", (long seqNo) => retval.IfSequenceNumberLessThanOrEqual = seqNo);
                requestHeaders.SetAttributeFromRequest("x-ms-if-sequence-number-lt", (long seqNo) => retval.IfSequenceNumberLessThan = seqNo);
                requestHeaders.SetAttributeFromRequest("x-ms-if-sequence-number-eq", (long seqNo) => retval.IfSequenceNumberEqual = seqNo);
            }
            return retval;
        }

        public static BlobRequestOptions CreateRequestOptions(this RequestQueryParameters queryParams)
        {
            return new BlobRequestOptions
            {
                ServerTimeout = queryParams.Timeout,
            };
        }

        public static OperationContext CreateOperationContext(this RequestHeaders requestHeaders)
        {
            return new OperationContext
            {
                ClientRequestID = requestHeaders.Value<string>("x-ms-client-request-id", null),
            };
        }
    }
}
