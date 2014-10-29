//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;

namespace Microsoft.Dash.Server.Handlers
{
    public static class RequestAuthorization
    {
        public static bool IsRequestAuthorized(HttpRequest request)
        {
            var authHeader = request.Headers["Authorization"];
            if (String.IsNullOrWhiteSpace(authHeader))
            {
                return false;
            }
            // TODO: Implement signature validation...
            var parts = authHeader.Split(' ', ':');
            if (parts.Length != 3)
            {
                return false;
            }
            else if (parts[0] != "SharedKey" && parts[1] != "SharedKeyLite")
            {
                return false;
            }
            var account = parts[1];
            var signature = parts[2];
            return true;
        }
    }
}