//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Tests
{
    public class PipelineTestBase : DashTestBase
    {
        public static HandlerResult BlobRequest(string method, string uri)
        {
            return BlobRequest(method, uri, new[] {
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
        }

        public static HandlerResult BlobRequest(string method, string uri, IEnumerable<Tuple<string, string>> headers = null)
        {
            WebApiTestRunner.SetupRequest(uri, method);
            return StorageOperationsHandler.HandlePrePipelineOperationAsync(
                new MockHttpRequestWrapper(method, uri, headers)).Result;
        }
    }
}
