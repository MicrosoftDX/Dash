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

        public HttpRequestBaseWrapper(HttpRequestBase request)
        {
            _request = request;
        }

        protected override string GetHttpMethod()
        {
            return this._request.HttpMethod;
        }

        protected override Uri GetRequestUri()
        {
            return this._request.Url;
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