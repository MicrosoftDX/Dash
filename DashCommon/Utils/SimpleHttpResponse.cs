//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net;

namespace Microsoft.Dash.Common.Utils
{
    public class SimpleHttpResponse
    {
        public SimpleHttpResponse()
        {
            this.StatusCode = HttpStatusCode.Unused;
        }

        public SimpleHttpResponse(SimpleHttpResponse src)
        {
            this.StatusCode = src.StatusCode;
            this.ReasonPhrase = src.ReasonPhrase;
        }

        public HttpStatusCode StatusCode { get; set; }
        public string ReasonPhrase { get; set; }
    }
}
