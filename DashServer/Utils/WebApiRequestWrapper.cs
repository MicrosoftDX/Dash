//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class WebApiRequestWrapper : DashHttpRequestWrapper
    {
        HttpRequestMessage _request;

        public WebApiRequestWrapper(HttpRequestMessage request)
        {
            _request = request;
        }

        protected override string GetHttpMethod()
        {
            return this._request.Method.Method;
        }

        protected override Uri GetRequestUri()
        {
            return this._request.RequestUri;
        }

        protected override RequestHeaders GetRequestHeaders()
        {
            return RequestHeaders.Create(this._request);
        }

        protected override RequestQueryParameters GetQueryParameters()
        {
            return RequestQueryParameters.Create(this._request);
        }
    }
}