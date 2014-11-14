//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class RequestHeaders : RequestItems
    {
        public static RequestHeaders Create(HttpRequestMessage request)
        {
            return new RequestHeaders(request.Headers);
        }

        public static RequestHeaders Create(HttpRequest request)
        {
            return Create(request.Headers);
        }

        public static RequestHeaders Create(NameValueCollection headers)
        {
            return new RequestHeaders(headers.Keys
                .Cast<string>()
                .Select(headerName => new KeyValuePair<string, string>(headerName, headers[headerName])));
        }

        public static RequestHeaders Create(ILookup<string, string> headers)
        {
            return new RequestHeaders(headers
                .Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header)));
        }

        private RequestHeaders(IEnumerable<KeyValuePair<string, string>> headers) 
            : base(headers)
        {
        }

        private RequestHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
            : base(headers)
        {
        }
    }
}