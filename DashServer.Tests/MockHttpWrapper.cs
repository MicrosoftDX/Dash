//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
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

        public MockHttpRequestWrapper(string method, string uri, IEnumerable<Tuple<string, string>> headers)
        {
            this.HttpMethod = method;
            this.Url = new Uri(uri);
            this.Headers = new NameValueCollection();
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    this.Headers.Add(header.Item1, header.Item2);
                }
            }
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
