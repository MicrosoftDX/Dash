//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net;

namespace Microsoft.Dash.Common.Utils
{
    public class SimpleHttpResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string ReasonPhrase { get; set; }
    }
}
