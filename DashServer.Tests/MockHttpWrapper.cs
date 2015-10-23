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
            this.OriginalPathSegments = this.Url.Segments
                .Select(segment => segment.Trim('/'))
                .Where(segment => !String.IsNullOrWhiteSpace(segment))
                .Skip(1)
                .ToArray();
            var segements = this.Url.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped)
                    .Trim('/')
                    .Split('/');
            this.Controller = segements.FirstOrDefault();
            this.PathSegments = segements
                .Skip(1)
                .ToArray();

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
        public string Controller { get; set; }
        public IEnumerable<string> PathSegments { get; private set; }
        public IEnumerable<string> OriginalPathSegments { get; private set; }
        public string AuthenticationScheme { get; set; }
        public byte[] AuthenticationKey { get; set; }
        public RequestQueryParameters QueryParameters 
        {
            get { return RequestQueryParameters.Create(this.Url.Query); }
        }
        public RequestUriParts UriParts 
        {
            get { return RequestUriParts.Create(this.Controller, this.PathSegments, this.OriginalPathSegments); }
        }

    }
}
