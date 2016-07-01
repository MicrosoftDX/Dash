//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Authorization;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Handlers
{
    public static class RequestAuthorization
    {
        public static async Task<bool> IsRequestAuthorizedAsync(IHttpRequestWrapper request, bool ignoreRequestAge = false)
        {
            // OPTIONS pre-flight is always valid
            if (request.HttpMethod == HttpMethod.Options.ToString())
            {
                return true;
            }
            var headers = request.Headers;
            var queryParams = request.QueryParameters;
            // See what type of auth scheme is applied to this request
            bool sharedKeyRequest = SharedKey.IsRequestType(headers);
            bool sasRequest = SharedAccessSignature.IsRequestType(queryParams);
            if (sharedKeyRequest && sasRequest)
            {
                // Can't be a SAS & SharedKey Authorization together
                return false;
            }
            else if (sasRequest)
            {
                return await SharedAccessSignature.IsAuthorizedAsync(request, headers, queryParams, ignoreRequestAge);
            }
            else if (sharedKeyRequest)
            {
                return SharedKey.IsAuthorized(request, headers, queryParams, ignoreRequestAge);
            }
            // Anonymous
            return await Anonymous.IsAuthorizedAsync(request);
        }
    }
}