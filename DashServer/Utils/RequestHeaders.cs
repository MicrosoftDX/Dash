﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Server.Utils
{
    public class RequestHeaders : RequestResponseItems
    {
        public static RequestHeaders Create(HttpRequestMessage request)
        {
            return new RequestHeaders(request.Headers
                .Concat(request.Content != null && request.Content.Headers != null ? 
                    request.Content.Headers : 
                    Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()));
        }

        public static RequestHeaders Create(HttpRequestBase request)
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

        public RequestHeaders(IEnumerable<KeyValuePair<string, string>> headers) 
            : base(headers)
        {
        }

        private RequestHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
            : base(headers)
        {
        }

        public string ClientRequestId
        {
            get { return this.Value<string>("x-ms-client-request-id", null); }
        }

        public DateTimeOffset RequestVersion
        {
            get { return this.Value("x-ms-version", StorageServiceVersions.Version_2009_09_19); }
        }

        public string CopySource
        {
            // We can't use any of the utility UrlDecode functions as they are too aggressive on reserved character
            // decoding (eg. '+' -> ' ').
            get { return PathUtils.PathDecode(this.Value<string>("x-ms-copy-source")); }
        }

        public string OriginalUri
        {
            get { return this.Value<string>("X-Original-URL", null); }
        }
    }
}