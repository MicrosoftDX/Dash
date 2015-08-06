//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Dash.Common.Update
{
    public class ApiUriBuilder : UriBuilder
    {
        // Well-known query params/headers
        public const string QueryParamRequestor = "requestor";
        public const string QueryParamCurrentVersion = "currentVersion";
        public const string QueryParamApiVersion = "api-version";

        IDictionary<string, string> _queryParameters;

        public ApiUriBuilder(string uri)
            : base(uri)
        {
            _queryParameters = ParseQueryParameters(base.Query);
            AddVersionParameter();
        }

        public ApiUriBuilder(string uri, IDictionary<string, string> queryParameters)
            : base(uri)
        {
            _queryParameters = queryParameters;
            AddVersionParameter();
        }

        public ApiUriBuilder(Uri uri)
            : base(uri)
        {
            _queryParameters = ParseQueryParameters(base.Query);
            AddVersionParameter();
        }

        public static bool TryCreate(string uri, out ApiUriBuilder uriBuilder)
        {
            try
            {
                uriBuilder = new ApiUriBuilder(uri);
                return true;
            }
            catch (UriFormatException)
            {
            }
            catch (ArgumentNullException)
            {
            }
            uriBuilder = null;
            return false;
        }

        public new string Query
        {
            get { return "?" + FormatQueryParameters(); }
            set
            {
                _queryParameters = ParseQueryParameters(value);
                base.Query = value;
            }
        }

        public new string ToString()
        {
            base.Query = FormatQueryParameters();
            return base.ToString();
        }

        public new Uri Uri
        {
            get
            {
                base.Query = FormatQueryParameters();
                return base.Uri;
            }
        }

        public IDictionary<string, string> QueryParameters
        {
            get { return _queryParameters; }
            set
            {
                _queryParameters = value;
                base.Query = FormatQueryParameters();
            }
        }

        IDictionary<string, string> ParseQueryParameters(string queryParameters)
        {
            if (queryParameters.Length > 0 && queryParameters[0] == '?')
            {
                queryParameters = queryParameters.Substring(1);
            }
            if (String.IsNullOrWhiteSpace(queryParameters))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            return queryParameters.Split('&')
                .Select(param => param.Split('='))
                .ToDictionary(param => param[0], param => param.Length > 1 ? param[1] : String.Empty, StringComparer.OrdinalIgnoreCase);
        }

        string FormatQueryParameters()
        {
            return String.Join("&", this._queryParameters
                .Where(param => !String.IsNullOrWhiteSpace(param.Value))
                .Select(param => String.Format("{0}={1}", param.Key, HttpUtility.UrlEncode(param.Value))));
        }

        void AddVersionParameter()
        {
            _queryParameters[QueryParamApiVersion] = Assembly.GetCallingAssembly().GetName().Version.SemanticVersionFormat();
        }
    }
}
