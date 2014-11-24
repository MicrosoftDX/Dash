//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    /// <summary>
    /// Test mock-out interface
    /// </summary>
    public interface IHttpRequestWrapper
    {
        RequestUriParts UriParts { get; }
        RequestHeaders Headers { get; }
        RequestQueryParameters QueryParameters { get; }
        Uri Url { get; }
        string HttpMethod { get; }
    }

    /// <summary>
    /// Implementation for real request
    /// </summary>
    public class DashHttpRequestWrapper : IHttpRequestWrapper
    {
        HttpRequest _request;

        public DashHttpRequestWrapper(HttpRequest request)
        {
            _request = request;
        }

        public RequestUriParts UriParts
        {
            get { return GetCachedObject<RequestUriParts>("Dash_RequestUriParts", () => RequestUriParts.Create(this._request.Url)); }
        }

        public RequestHeaders Headers
        {
            get { return GetCachedObject<RequestHeaders>("Dash_RequestHeaders", () => RequestHeaders.Create(this._request.Headers)); }
        }

        public RequestQueryParameters QueryParameters
        {
            get { return GetCachedObject<RequestQueryParameters>("Dash_QueryParameters", () => RequestQueryParameters.Create(this._request.QueryString)); }
        }

        public Uri Url
        {
            get { return _request.Url; }
        }

        public string HttpMethod
        {
            get { return _request.HttpMethod; }
        }

        T GetCachedObject<T>(string key, Func<T> creator)
        {
            // We're reasonably thread safe here because we're affinitized to a single request, so we omit locking
            var ctx = HttpContextFactory.Current;
            if (ctx.Items.Contains(key))
            {
                return (T)ctx.Items[key];
            }
            T newObject = creator();
            ctx.Items[key] = newObject;
            return newObject;
        }
    }
}