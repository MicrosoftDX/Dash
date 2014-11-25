//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class ResponseHeaders : RequestResponseItems
    {
        public static ResponseHeaders Create(NameValueCollection headers)
        {
            return new ResponseHeaders(headers.Keys
                .Cast<string>()
                .Select(headerName => new KeyValuePair<string, string>(headerName, headers[headerName])));
        }

        public static ResponseHeaders Create(ILookup<string, string> headers)
        {
            return new ResponseHeaders(headers
                .Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header)));
        }

        public ResponseHeaders(IEnumerable<KeyValuePair<string, string>> headers)
            : base(headers)
        {
        }

        private ResponseHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
            : base(headers)
        {
        }
    }
}