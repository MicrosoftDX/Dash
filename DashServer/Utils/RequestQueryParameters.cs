//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class RequestQueryParameters
    {
        ILookup<string, string> _queryParams;

        public static RequestQueryParameters Create(HttpRequestMessage request)
        {
            return new RequestQueryParameters(request.GetQueryNameValuePairs());
        }

        protected RequestQueryParameters(IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            _queryParams = queryParams
                .ToLookup(queryParam => queryParam.Key, queryParam => queryParam.Value, StringComparer.OrdinalIgnoreCase);
        }

        public string this[string queryParamName]
        {
            get { return this.Value<string>(queryParamName); }
        }

        public T Value<T>(string queryParamName)
        {
            return Value(queryParamName, default(T));
        }

        public T Value<T>(string queryParamName, T defaultValue)
        {
            var values = Values<T>(queryParamName);
            if (values != null && values.Any())
            {
                return values.First();
            }
            return defaultValue;
        }

        public IEnumerable<T> Values<T>(string queryParamName)
        {
            var values = _queryParams[queryParamName];
            if (values != null)
            {
                return values
                    .Select(value =>
                    {
                        try
                        {
                            if (typeof(T).IsEnum)
                            {
                                return (T)Enum.Parse(typeof(T), value);
                            }
                            return (T)Convert.ChangeType(value, typeof(T));
                        }
                        catch
                        {
                        }
                        return default(T);
                    });
            }
            return Enumerable.Empty<T>();
        }
    }
}