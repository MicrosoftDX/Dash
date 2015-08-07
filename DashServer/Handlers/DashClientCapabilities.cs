//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Handlers
{
    [Flags]
    public enum DashClientCapabilities
    {
        None = 0x00,
        FollowRedirects = 0x01,
        NoPayloadToDash = 0x02,
        FullSupport = FollowRedirects | NoPayloadToDash,
    }

    public static class DashClientDetector
    {
        public static DashClientCapabilities DetectClient(IHttpRequestWrapper requestWrapper)
        {
            DashClientCapabilities retval = DashClientCapabilities.None;
            string agent = requestWrapper.Headers.Value("User-Agent", String.Empty).ToLower();
            bool expect100 = requestWrapper.Headers.Contains("Expect");
            if (expect100)
            {
                // Expect: 100-Continue trumps everything
                retval = DashClientCapabilities.FullSupport;
            }
            else if (agent.Contains("dash"))
            {
                // Modified client
                retval = DashClientCapabilities.FullSupport;
            }
            else if (agent.StartsWith("wa-storage/2.0.6") || 
                agent.Contains(".net") ||
                agent.Contains("windowspowershell"))
            {
                // .NET clients can handle redirects seamlessly (they omit the Authorization header as they follow the redirect)
                retval = DashClientCapabilities.FollowRedirects;
            }
            return retval;
        }
    }
}