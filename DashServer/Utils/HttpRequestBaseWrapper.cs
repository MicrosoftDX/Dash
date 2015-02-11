//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class HttpRequestBaseWrapper : DashHttpRequestWrapper
    {
        HttpRequestBase _request;
        Uri _requestUri;

        public HttpRequestBaseWrapper(HttpRequestBase request, bool uriDecode)
        {
            _request = request;
            if (uriDecode)
            {
                _requestUri = new Uri(HttpUtility.UrlDecode(this._request.Url.ToString()));
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
    }
}