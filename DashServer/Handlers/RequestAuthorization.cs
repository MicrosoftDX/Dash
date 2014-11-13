//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Handlers
{
    public static class RequestAuthorization
    {
        static readonly string AccountName  = DashConfiguration.AccountName;
        static readonly byte[] AccountKey   = DashConfiguration.AccountKey;

        const string AlgorithmSharedKey     = "SharedKey";
        const string AlgorithmSharedKeyLite = "SharedKeyLite";

        public static bool IsRequestAuthorized(IHttpRequestWrapper request, bool ignoreRequestAge = false)
        {
            var headers = RequestHeaders.Create(request.Headers);
            var queryParams = RequestQueryParameters.Create(request.Url.Query);
            // See if this is an anonymous request
            var authHeader = headers.Value<string>("Authorization");
            if (String.IsNullOrWhiteSpace(authHeader))
            {
                return IsAnonymousAccessAllowed(request);
            }
            // Quick request age check
            string requestDateHeader = headers.Value<string>("x-ms-date");
            string dateHeader = String.Empty;
            if (String.IsNullOrWhiteSpace(requestDateHeader))
            {
                requestDateHeader = headers.Value<string>("Date");
                dateHeader = requestDateHeader;
            }
            if (String.IsNullOrWhiteSpace(requestDateHeader))
            {
                // One of the date headers is mandatory
                return false;
            }
            if (!ignoreRequestAge)
            {
                DateTime requestDate;
                if (!DateTime.TryParse(requestDateHeader, out requestDate))
                {
                    return false;
                }
                else if (requestDate < DateTime.Now.AddMinutes(-15))
                {
                    return false;
                }
            }
            var parts = authHeader.Split(' ', ':');
            if (parts.Length != 3)
            {
                return false;
            }
            else if (parts[0] != AlgorithmSharedKey && parts[0] != AlgorithmSharedKeyLite)
            {
                return false;
            }
            var account = parts[1];
            var signature = parts[2];

            // Pull out the MVC controller part of the path
            var requestUriParts = RequestUriParts.Create(request.Url);
            string uriPath = requestUriParts.PublicUriPath;
            var uriUnencodedPath = uriPath.Replace(":", "%3A").Replace("@", "%40");
            bool runUnencodedComparison = uriPath != uriUnencodedPath;

            // Both encoding schemes are valid for the signature
            return String.Equals(account, AccountName, StringComparison.OrdinalIgnoreCase) &&
                (signature == SharedKeySignature(parts[0] == AlgorithmSharedKeyLite, request.HttpMethod.ToUpper(), uriPath, headers, queryParams, dateHeader) ||
                 (runUnencodedComparison && signature == SharedKeySignature(parts[0] == AlgorithmSharedKeyLite, request.HttpMethod.ToUpper(), uriUnencodedPath, headers, queryParams, dateHeader)));
        }

        public static string SharedKeySignature(bool liteAlgorithm, string method, string uriPath, RequestHeaders headers, RequestQueryParameters queryParams, string requestDate)
        {
            // Signature scheme is described at: http://msdn.microsoft.com/en-us/library/azure/dd179428.aspx
            // and the SDK implementation is at: https://github.com/Azure/azure-storage-net/tree/master/Lib/ClassLibraryCommon/Auth/Protocol
            string stringToSign = "";

            if (liteAlgorithm)
            {
                stringToSign = String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}",
                                                    method,
                                                    headers.Value("Content-MD5", String.Empty),
                                                    headers.Value("Content-Type", String.Empty),
                                                    requestDate,
                                                    GetCanonicalizedHeaders(headers),
                                                    GetCanonicalizedResource(liteAlgorithm, uriPath, queryParams, AccountName));
            }
            else
            {
                var contentLength = headers.Value("Content-Length", "0");
                int length;
                if (!int.TryParse(contentLength, out length) || length <= 0)
                {
                    contentLength = String.Empty;
                }
                stringToSign = String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n{6}\n{7}\n{8}\n{9}\n{10}\n{11}\n{12}\n{13}",
                                                    method,
                                                    headers.Value("Content-Encoding", String.Empty),
                                                    headers.Value("Content-Language", String.Empty),
                                                    contentLength,
                                                    headers.Value("Content-MD5", String.Empty),
                                                    headers.Value("Content-Type", String.Empty),
                                                    requestDate,
                                                    headers.Value("If-Modified-Since", String.Empty),
                                                    headers.Value("If-Match", String.Empty),
                                                    headers.Value("If-None-Match", String.Empty),
                                                    headers.Value("If-Unmodified-Since", String.Empty),
                                                    headers.Value("Range", String.Empty),
                                                    GetCanonicalizedHeaders(headers),
                                                    GetCanonicalizedResource(liteAlgorithm, uriPath, queryParams, AccountName));
            }

            var bytesToSign = Encoding.UTF8.GetBytes(stringToSign);
            using (var hmac = new HMACSHA256(AccountKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(bytesToSign));
            }
        }

        static string GetCanonicalizedHeaders(RequestHeaders headers)
        {
            return String.Join("\n", headers
                .Where(header => header.Key.StartsWith("x-ms-") && 
                                 header.Any(headerValue => !String.IsNullOrWhiteSpace(headerValue)))
                .OrderBy(header => header.Key, StringComparer.Create(new CultureInfo("en-US"), false))
                .Select(header => FormatCanonicalizedValues(header)));
        }

        static string GetCanonicalizedResource(bool liteAlgorithm, string uriPath, RequestQueryParameters queryParams, string accountName)
        {
            string commonPrefix = "/" + accountName + uriPath;
            if (liteAlgorithm)
            {
                var compParam = queryParams.Value<string>("comp");
                return commonPrefix + (!String.IsNullOrWhiteSpace(compParam) ? "?comp=" + compParam : "");
            }
            else
            {
                if (queryParams.Any())
                {
                    return commonPrefix + "\n" + String.Join("\n", queryParams
                        .OrderBy(queryParam => queryParam.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(queryParam => FormatCanonicalizedValues(queryParam)));
                }
                return commonPrefix;
            }
        }

        static string FormatCanonicalizedValues(IGrouping<string, string> headerOrParameter)
        {
            return headerOrParameter.Key.ToLowerInvariant() + ":" +
                String.Join(",", headerOrParameter
                    .Select(value => value.TrimStart().Replace("\r\n", String.Empty))
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        }

        static bool IsAnonymousAccessAllowed(IHttpRequestWrapper request)
        {
            bool retval = false;
            var requestUriParts = RequestUriParts.Create(request.Url);
            if (requestUriParts.IsAccountRequest)
            {
                // No anonymous access to account operations
                retval = false;
            }
            else if (requestUriParts.IsContainerRequest)
            {
                // TODO: Check if the container permits anonymous operations for this type
            }
            else if (requestUriParts.IsBlobRequest)
            {
                // TODO: Check if the containing container permits anonymous operations for this type
            }
            return retval;
        }

        /// <summary>
        /// Implementation for real request
        /// </summary>
        public class HttpRequestWrapper : IHttpRequestWrapper
        {
            HttpRequest _request;

            public HttpRequestWrapper(HttpRequest request)
            {
                _request = request;
            }

            public NameValueCollection Headers
            {
                get { return _request.Headers; }
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

    /// <summary>
    /// Test mock-out interface
    /// </summary>
    public interface IHttpRequestWrapper
    {
        NameValueCollection Headers { get; }
        Uri Url { get; }
        string HttpMethod { get; }
    }
}