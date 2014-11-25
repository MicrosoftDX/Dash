//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
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
    public abstract class DashHttpRequestWrapper : IHttpRequestWrapper
    {
        protected DashHttpRequestWrapper()
        {
        }

        public static IHttpRequestWrapper Create(HttpRequest request)
        {
            return new HttpRequestBaseWrapper(new HttpRequestWrapper(request));
        }

        public static IHttpRequestWrapper Create(HttpRequestMessage request)
        {
            return new WebApiRequestWrapper(request);
        }

        public RequestUriParts UriParts
        {
            get { return GetCachedObject<RequestUriParts>("Dash_RequestUriParts", () => RequestUriParts.Create(GetRequestUri())); }
        }

        public RequestHeaders Headers
        {
            get { return GetCachedObject<RequestHeaders>("Dash_RequestHeaders", () => GetRequestHeaders()); }
        }

        public RequestQueryParameters QueryParameters
        {
            get { return GetCachedObject<RequestQueryParameters>("Dash_QueryParameters", () => GetQueryParameters()); }
        }

        protected abstract string GetHttpMethod();
        protected abstract Uri GetRequestUri();
        protected abstract RequestHeaders GetRequestHeaders();
        protected abstract RequestQueryParameters GetQueryParameters();

        public Uri Url
        {
            get { return GetRequestUri(); }
        }

        public string HttpMethod
        {
            get { return GetHttpMethod(); }
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