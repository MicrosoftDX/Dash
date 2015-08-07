//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Web;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Server.Utils
{
    public class HttpRequestBaseWrapper : DashHttpRequestWrapper
    {
        HttpRequestBase _request;
        Uri _requestUri;
        IEnumerable<string> _pathSegments;
        IEnumerable<string> _originalSegments;

        public HttpRequestBaseWrapper(HttpRequestBase request, bool uriDecode)
        {
            _request = request;
            if (uriDecode)
            {
                _requestUri = new Uri(HttpUtility.UrlDecode(this._request.Url.ToString()));
                _pathSegments = PathUtils.GetPathSegments(_requestUri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped));
                _originalSegments = PathUtils.GetPathSegments(this._request.Url.AbsolutePath);
            }
            else
            {
                _requestUri = this._request.Url;
            }
        }

        protected override string GetHttpMethod()
        {
            return this._request.HttpMethod;
        }

        protected override Uri GetRequestUri()
        {
            return this._requestUri;
        }

        protected override RequestHeaders GetRequestHeaders()
        {
            return RequestHeaders.Create(this._request.Headers);
        }

        protected override RequestQueryParameters GetQueryParameters()
        {
            return RequestQueryParameters.Create(this._request.QueryString);
        }

        protected override IEnumerable<string> GetPathSegments()
        {
            if (_pathSegments != null)
            {
                return _pathSegments;
            }
            return base.GetPathSegments();
        }

        protected override IEnumerable<string> GetOriginalPathSegments()
        {
            if (_originalSegments != null)
            {
                return _originalSegments;
            }
            return base.GetOriginalPathSegments();
        }
    }
}