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
    public interface IHttpRequestWrapper
    {
        NameValueCollection Headers { get; }
        Uri Url { get; }
        string HttpMethod { get; }
    }

    public static class RequestAuthorization
    {
        static readonly string AccountName  = DashConfiguration.AccountName;
        static readonly byte[] AccountKey   = DashConfiguration.AccountKey;

        const string AlgorithmSharedKey     = "SharedKey";
        const string AlgorithmSharedKeyLite = "SharedKeyLite";

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

        public static bool IsRequestAuthorized(IHttpRequestWrapper request, bool ignoreRequestAge = false)
        {
            var headers = request.Headers.Keys
                .Cast<string>()
                .SelectMany(headerName => 
                    request.Headers.GetValues(headerName)
                        .Select(headerValue => Tuple.Create(headerName, headerValue)))
                .ToLookup(header => header.Item1, header => header.Item2, StringComparer.OrdinalIgnoreCase);
            // Quick request age check
            string requestDateHeader = headers.First("x-ms-date");
            string dateHeader = String.Empty;
            if (String.IsNullOrWhiteSpace(requestDateHeader))
            {
                requestDateHeader = headers.First("Date");
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
            // Now the auth header with signature
            var authHeader = headers.First("Authorization");
            if (String.IsNullOrWhiteSpace(authHeader))
            {
                return false;
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
            var uriPath = "/" + String.Join("/",
                request.Url.Segments
                    .Select(segment => segment.Trim('/'))
                    .Where(segment => !String.IsNullOrWhiteSpace(segment))
                    .Skip(1));
            var uriUnencodedPath = uriPath.Replace(":", "%3A").Replace("@", "%40");
            bool runUnencodedComparison = uriPath != uriUnencodedPath;

            // Both encoding schemes are valid for the signature
            return String.Equals(account, AccountName, StringComparison.OrdinalIgnoreCase) &&
                (signature == SharedKeySignature(parts[0] == AlgorithmSharedKeyLite, request.HttpMethod.ToUpper(), uriPath, headers, request.Url.Query, dateHeader) ||
                 (runUnencodedComparison && signature == SharedKeySignature(parts[0] == AlgorithmSharedKeyLite, request.HttpMethod.ToUpper(), uriUnencodedPath, headers, request.Url.Query, dateHeader)));
        }

        public static string SharedKeySignature(bool liteAlgorithm, string method, string uriPath, ILookup<string, string> headers, string uriQuery, string requestDate)
        {
            // Signature scheme is described at: http://msdn.microsoft.com/en-us/library/azure/dd179428.aspx
            // and the SDK implementation is at: https://github.com/Azure/azure-storage-net/tree/master/Lib/ClassLibraryCommon/Auth/Protocol
            string stringToSign = "";

            if (liteAlgorithm)
            {
                stringToSign = String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}",
                                                    method,
                                                    headers.FirstOrDefault("Content-MD5", String.Empty),
                                                    headers.FirstOrDefault("Content-Type", String.Empty),
                                                    requestDate,
                                                    GetCanonicalizedHeaders(headers),
                                                    GetCanonicalizedResource(liteAlgorithm, uriPath, uriQuery, AccountName));
            }
            else
            {
                var contentLength = headers.FirstOrDefault("Content-Length", "0");
                int length;
                if (!int.TryParse(contentLength, out length) || length <= 0)
                {
                    contentLength = String.Empty;
                }
                stringToSign = String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n{6}\n{7}\n{8}\n{9}\n{10}\n{11}\n{12}\n{13}",
                                                    method,
                                                    headers.FirstOrDefault("Content-Encoding", String.Empty),
                                                    headers.FirstOrDefault("Content-Language", String.Empty),
                                                    contentLength,
                                                    headers.FirstOrDefault("Content-MD5", String.Empty),
                                                    headers.FirstOrDefault("Content-Type", String.Empty),
                                                    requestDate,
                                                    headers.FirstOrDefault("If-Modified-Since", String.Empty),
                                                    headers.FirstOrDefault("If-Match", String.Empty),
                                                    headers.FirstOrDefault("If-None-Match", String.Empty),
                                                    headers.FirstOrDefault("If-Unmodified-Since", String.Empty),
                                                    headers.FirstOrDefault("Range", String.Empty),
                                                    GetCanonicalizedHeaders(headers),
                                                    GetCanonicalizedResource(liteAlgorithm, uriPath, uriQuery, AccountName));
            }

            var bytesToSign = Encoding.UTF8.GetBytes(stringToSign);
            using (var hmac = new HMACSHA256(AccountKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(bytesToSign));
            }
        }

        static string GetCanonicalizedHeaders(ILookup<string, string> headers)
        {
            return String.Join("\n", headers
                .Where(header => header.Key.StartsWith("x-ms-") && 
                                 header.Any(headerValue => !String.IsNullOrWhiteSpace(headerValue)))
                .OrderBy(header => header.Key, StringComparer.Create(new CultureInfo("en-US"), false))
                .Select(header => FormatCanonicalizedValues(header)));
        }

        static string GetCanonicalizedResource(bool liteAlgorithm, string uriPath, string uriQuery, string accountName)
        {
            var queryParams = HttpUtility.ParseQueryString(uriQuery);
            var queryParamsLookup = queryParams
                .AllKeys
                .SelectMany(queryParam => queryParams.GetValues(queryParam)
                    .Select(paramValue => Tuple.Create(queryParam, paramValue)))
                .ToLookup(queryParam => queryParam.Item1, queryParam => queryParam.Item2, StringComparer.InvariantCultureIgnoreCase);
            string commonPrefix = "/" + accountName + uriPath;
            if (liteAlgorithm)
            {
                var compParam = queryParamsLookup.First("comp");
                return commonPrefix + (!String.IsNullOrWhiteSpace(compParam) ? "?comp=" + compParam : "");
            }
            else
            {
                if (queryParamsLookup.Any())
                {
                    return commonPrefix + "\n" + String.Join("\n", queryParamsLookup
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

        static T First<K,T>(this ILookup<K,T> lookup, K key)
        {
            return lookup.FirstOrDefault(key, default(T));
        }

        static T FirstOrDefault<K, T>(this ILookup<K, T> lookup, K key, T defaultValue)
        {
            var values = lookup[key];
            if (values != null)
            {
                return values.FirstOrDefault();
            }
            return defaultValue;
        }
    }
}