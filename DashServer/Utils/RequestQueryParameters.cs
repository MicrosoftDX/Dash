//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class RequestQueryParameters : RequestResponseItems
    {
        public static RequestQueryParameters Create(HttpRequestMessage request)
        {
            return new RequestQueryParameters(request.GetQueryNameValuePairs());
        }

        public static RequestQueryParameters Create(HttpRequestBase request)
        {
            return Create(request.QueryString);
        }

        public static RequestQueryParameters Create(string uriQuery)
        {
            return Create(HttpUtility.ParseQueryString(uriQuery));
        }

        public static RequestQueryParameters Create(NameValueCollection queryParams)
        {
            return new RequestQueryParameters(queryParams.Keys
                .Cast<string>()
                .Select(queryParamName => new KeyValuePair<string, string>(queryParamName, queryParams[queryParamName])));
        }

        public static RequestQueryParameters Create(ILookup<string, string> queryParams)
        {
            return new RequestQueryParameters(queryParams
                .SelectMany(queryParam => queryParam
                    .Select(paramValue => new KeyValuePair<string, string>(queryParam.Key, paramValue))));
        }

        public static RequestQueryParameters Empty
        {
            get { return new RequestQueryParameters(Enumerable.Empty<KeyValuePair<string, string>>()); }
        }

        private RequestQueryParameters(IEnumerable<KeyValuePair<string, string>> queryParams) 
            : base(queryParams)
        {
        }

        public TimeSpan? Timeout
        {
            get { return TimeSpanFromSeconds("timeout"); }
        }
    }
}