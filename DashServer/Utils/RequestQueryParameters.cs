//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class RequestQueryParameters : RequestItems
    {
        public static RequestQueryParameters Create(HttpRequestMessage request)
        {
            return new RequestQueryParameters(request.GetQueryNameValuePairs());
        }

        public static RequestQueryParameters Create(HttpRequest request)
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

        private RequestQueryParameters(IEnumerable<KeyValuePair<string, string>> queryParams) 
            : base(queryParams)
        {
        }
    }
}