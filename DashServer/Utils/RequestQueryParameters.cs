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

        public static RequestQueryParameters Create(string uriQuery, bool decodeValues = true)
        {
            return Create(ParseQueryString(uriQuery, decodeValues));
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

        public void Append(string queryString, bool decodeValues = true)
        {
            var appendQueryParams = ParseQueryString(queryString, decodeValues);
            foreach (string paramName in appendQueryParams.Keys)
            {
                this.Append(paramName, appendQueryParams[paramName]);
            }
        }

        public void Remove(string paramName)
        {
            _items.Remove(paramName);
        }

        public override string ToString()
        {
            return String.Join("&", _items
                .SelectMany(queryParams =>
                    queryParams.Value.Select(paramValue => queryParams.Key + '=' + paramValue)));
        }

        static NameValueCollection ParseQueryString(string queryString, bool urlDecode)
        {
            NameValueCollection retval = new NameValueCollection();
            int length = queryString != null ? queryString.Length : 0;
            int startCharacter = !String.IsNullOrEmpty(queryString) && queryString[0] == '?' ? 1 : 0;
            for (int index = startCharacter; index < length; index++)
            {
                int startIndex = index;
                int valueDelimIndex = -1;
                while (index < length)
                {
                    char ch = queryString[index];
                    if (ch == '=')
                    {
                        if (valueDelimIndex < 0)
                        {
                            valueDelimIndex = index;
                        }
                    }
                    else if (ch == '&')
                    {
                        break;
                    }
                    index++;
                }
                string paramName = null, paramValue = null;
                if (valueDelimIndex >= 0)
                {
                    paramName = queryString.Substring(startIndex, valueDelimIndex - startIndex);
                    paramValue = queryString.Substring(valueDelimIndex + 1, (index - valueDelimIndex) - 1);
                }
                else
                {
                    paramValue = queryString.Substring(startIndex, index - startIndex);
                }
                if (urlDecode)
                {
                    retval.Add(HttpUtility.UrlDecode(paramName), HttpUtility.UrlDecode(paramValue));
                }
                else
                {
                    retval.Add(paramName, paramValue);
                }
                // Preserve compat with HttpUtility.ParseQueryString - append empty param if there's a delimiter on the end of the string.
                if ((index == (length - 1)) && (queryString[index] == '&'))
                {
                    retval.Add(null, string.Empty);
                }
            }
            return retval;
        }
    }
}