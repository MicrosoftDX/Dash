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
        NameValueCollection Headers { get; }
        NameValueCollection QueryParameters { get; }
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

        public NameValueCollection Headers
        {
            get { return _request.Headers; }
        }

        public NameValueCollection QueryParameters
        {
            get { return _request.QueryString; }
        }

        public Uri Url
        {
            get { return _request.Url; }
        }

        public string HttpMethod
        {
            get { return _request.HttpMethod; }
        }
    }
}