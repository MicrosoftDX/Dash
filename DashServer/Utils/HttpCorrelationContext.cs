//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Web;
using Microsoft.Dash.Common.Diagnostics;

namespace Microsoft.Dash.Server.Utils
{
    // Reference counter/RAII wrapper for assigning a per-request correlation id.
    // NOTE: This class does not have any concurrency protection and is intended to only be called from sites
    // that do not have any contention conditions.
    public class HttpCorrelationContext
    {
        const string CorrelationContextCacheKey = "Dash_CorrelationContext";

        public static void Set(HttpRequest request)
        {
            // Set the correlation id - this can come from the request's x-ms-client-request-id header or we gen up something unique
            var headers = DashHttpRequestWrapper.Create(request).Headers;
            string clientId = headers.ClientRequestId;
            Guid correlationId;
            if (String.IsNullOrWhiteSpace(clientId) || !Guid.TryParse(clientId, out correlationId))
            {
                correlationId = Guid.NewGuid();
            }
            HttpContextFactory.Current.Items[CorrelationContextCacheKey] = new CorrelationContext(correlationId);
        }

        public static void Reset()
        {
            // Release request correlation context
            var httpCtx = HttpContextFactory.Current;
            if (httpCtx.Items.Contains(CorrelationContextCacheKey))
            {
                var ctx = httpCtx.Items[CorrelationContextCacheKey] as CorrelationContext;
                if (ctx != null)
                {
                    ctx.Dispose();
                    httpCtx.Items.Remove(CorrelationContextCacheKey);
                }
            }
        }
    }
}