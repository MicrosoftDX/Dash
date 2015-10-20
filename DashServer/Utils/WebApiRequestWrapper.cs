//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Server.Utils
{
    public class WebApiRequestWrapper : DashHttpRequestWrapper
    {
        HttpRequestMessage _request;

        public WebApiRequestWrapper(HttpRequestMessage request) :
            base(request.RequestUri)
        {
            _request = request;
        }

        protected override string GetHttpMethod()
        {
            return this._request.Method.Method;
        }

        protected override RequestHeaders GetRequestHeaders()
        {
            return RequestHeaders.Create(this._request);
        }

        protected override RequestQueryParameters GetQueryParameters()
        {
            return RequestQueryParameters.Create(this._request);
        }

        protected override RequestUriParts GetUriParts()
        {
            var segments = PathUtils.GetPathSegments(this.Url.AbsolutePath);
            IEnumerable<string> originalSegments;
            var originalUri = this.Headers.OriginalUri;
            if (!String.IsNullOrWhiteSpace(originalUri))
            {
                // The value from this header doesn't not include the controller name which is prepended by UrlRewrite
                originalSegments = PathUtils.GetPathSegments(originalUri);
            }
            else
            {
                originalSegments = segments.Skip(1);
            }
            return RequestUriParts.Create(segments.FirstOrDefault(),
                segments.Skip(1),
                originalSegments);
        }
    }
}