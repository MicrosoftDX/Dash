//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Server.Utils
{
    public class HttpRequestBaseWrapper : DashHttpRequestWrapper
    {
        HttpRequestBase _request;

        public HttpRequestBaseWrapper(HttpRequestBase request) :
            base(request.Url)
        {
            _request = request;
        }

        protected override string GetHttpMethod()
        {
            return this._request.HttpMethod;
        }

        protected override RequestHeaders GetRequestHeaders()
        {
            return RequestHeaders.Create(this._request.Headers);
        }

        protected override RequestQueryParameters GetQueryParameters()
        {
            return RequestQueryParameters.Create(this._request.QueryString);
        }

        protected override RequestUriParts GetUriParts()
        {
            // The Original path does not include the controller segment which is injected by UrlRewrite
            string originalPath = this.Headers.OriginalUri;
            if (String.IsNullOrWhiteSpace(originalPath))
            {
                originalPath = this._request.RawUrl;
            }
            return RequestUriParts.Create(PathUtils.GetPathSegments(this.Url.AbsolutePath).FirstOrDefault(),
                PathUtils.GetPathSegments(this._request.RawUrl),
                PathUtils.GetPathSegments(originalPath));
        }
    }
}