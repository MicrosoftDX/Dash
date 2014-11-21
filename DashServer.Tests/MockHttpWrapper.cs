//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Specialized;
using System.Web;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Tests
{
    class MockHttpRequestWrapper : IHttpRequestWrapper
    {
        public MockHttpRequestWrapper()
        {
            this.Headers = new NameValueCollection();
        }

        public NameValueCollection Headers { get; set; }
        public Uri Url { get; set; }
        public string HttpMethod { get; set; }
        public NameValueCollection QueryParameters 
        {
            get { return HttpUtility.ParseQueryString(this.Url.Query); }
        }
    }
}
