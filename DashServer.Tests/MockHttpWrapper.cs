//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using Microsoft.Dash.Server.Utils;
using System.Linq;

namespace Microsoft.Tests
{
    class MockHttpRequestWrapper : IHttpRequestWrapper
    {
        public MockHttpRequestWrapper()
        {
            this.Headers = new RequestHeaders(Enumerable.Empty<KeyValuePair<string, string>>());
        }

        public MockHttpRequestWrapper(string method, string uri, IEnumerable<Tuple<string, string>> headers)
        {
            this.HttpMethod = method;
            this.Url = new Uri(uri);
            if (headers != null)
            {
                this.Headers = new RequestHeaders(headers
                    .Select(header => new KeyValuePair<string, string>(header.Item1, header.Item2)));
            }
            else
            {
                this.Headers = new RequestHeaders(Enumerable.Empty<KeyValuePair<string, string>>());
            }
        }

        public RequestHeaders Headers { get; set; }
        public Uri Url { get; set; }
        public string HttpMethod { get; set; }
        public RequestQueryParameters QueryParameters 
        {
            get { return RequestQueryParameters.Create(this.Url.Query); }
        }
        public RequestUriParts UriParts 
        {
            get { return RequestUriParts.Create(this.Url); }
        }

    }
}
