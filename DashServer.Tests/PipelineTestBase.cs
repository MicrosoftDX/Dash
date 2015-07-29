//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tests
{
    public class PipelineTestBase
    {
        public void InitializeConfig(IDictionary<string, string> config)
        {
            WebApiTestRunner.InitializeConfig(config);
        }

        public static HandlerResult BlobRequest(string method, string uri)
        {
            return BlobRequest(method, uri, new[] {
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
        }

        public static HandlerResult BlobRequest(string method, string uri, IEnumerable<Tuple<string, string>> headers)
        {
            WebApiTestRunner.SetupRequest(uri, method);
            return StorageOperationsHandler.HandlePrePipelineOperationAsync(
                new MockHttpRequestWrapper(method, uri, headers)).Result;
        }
    }
}
