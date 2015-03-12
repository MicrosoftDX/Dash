//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Common.Diagnostics;

namespace Microsoft.Dash.Server.Handlers
{
    public static class RequestAuthorization
    {
        static readonly string AccountName  = DashConfiguration.AccountName;
        static readonly byte[] AccountKey   = DashConfiguration.AccountKey;

        const string AlgorithmSharedKey     = "SharedKey";
        const string AlgorithmSharedKeyLite = "SharedKeyLite";

        public static async Task<bool> IsRequestAuthorizedAsync(IHttpRequestWrapper request, bool ignoreRequestAge = false)
        {
            var headers = request.Headers;
            var queryParams = request.QueryParameters;
            // See if this is an anonymous request
            var authHeader = headers.Value<string>("Authorization");
            if (String.IsNullOrWhiteSpace(authHeader))
            {
                return await IsAnonymousAccessAllowedAsync(request);
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
            var requestUriParts = request.UriParts;
            string uriPath = requestUriParts.OriginalUriPath;
            var uriUnencodedPath = requestUriParts.PublicUriPath;
            bool runUnencodedComparison = uriPath != uriUnencodedPath;

            // For some verbs we can't tell if the Content-Length header was specified as 0 or that IIS/UrlRewrite/ASP.NET has constructed
            // the header value for us. The difference is significant to the signature as content length is included for SharedKey
            bool fullKeyAlgorithm = parts[0] == AlgorithmSharedKey;
            bool runBlankContentLengthComparison = false;
            string method = request.HttpMethod.ToUpper();
            var contentLength = headers.Value("Content-Length", "");
            if (fullKeyAlgorithm)
            {
                int length;
                if (!int.TryParse(contentLength, out length) || length <= 0)
                {
                    // Preserve a Content-Length: 0 header for PUT methods
                    runBlankContentLengthComparison = !method.Equals(WebRequestMethods.Http.Put, StringComparison.OrdinalIgnoreCase);
                }
            }
            // Both encoding schemes are valid for the signature
            // Evaluations are ordered by most likely match to least likely match to reduce # of hash calculations
            if (!String.Equals(account, AccountName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (runBlankContentLengthComparison &&
                signature == SharedKeySignature(!fullKeyAlgorithm, method, uriPath, headers, queryParams, dateHeader, String.Empty))
            {
                return true;
            }
            else if (signature == SharedKeySignature(!fullKeyAlgorithm, method, uriPath, headers, queryParams, dateHeader, contentLength))
            {
                return true;
            }
            else if (runBlankContentLengthComparison &&
                runUnencodedComparison &&
                signature == SharedKeySignature(!fullKeyAlgorithm, method, uriUnencodedPath, headers, queryParams, dateHeader, String.Empty))
            {
                return true;
            }
            else if (runUnencodedComparison && 
                signature == SharedKeySignature(!fullKeyAlgorithm, method, uriUnencodedPath, headers, queryParams, dateHeader, contentLength))
            {
                return true;
            }
            DashTrace.TraceWarning("Failed to authenticate request: {0}:{1}:{2}:{3}", account, method, uriPath, signature);

            return false;
        }

        public static string SharedKeySignature(bool liteAlgorithm, string method, string uriPath, RequestHeaders headers, RequestQueryParameters queryParams, string requestDate, string contentLength)
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
            DashTrace.TraceInformation("Authentication signing string: {0}", stringToSign);

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

        static async Task<bool> IsAnonymousAccessAllowedAsync(IHttpRequestWrapper request)
        {
            bool retval = false;
            var requestUriParts = request.UriParts;
            var requestOperation = StorageOperations.GetBlobOperation(request.HttpMethod, requestUriParts, request.QueryParameters, request.Headers);
            if (request.HttpMethod == HttpMethod.Options.ToString())
            {
                return true;
            }
            switch (requestOperation)
            {
                case StorageOperationTypes.GetContainerProperties:
                case StorageOperationTypes.GetContainerMetadata:
                case StorageOperationTypes.ListBlobs:
                    retval = await GetContainerPublicAccessAsync(requestUriParts.Container) == BlobContainerPublicAccessType.Container;
                    break;

                case StorageOperationTypes.GetBlob:
                case StorageOperationTypes.GetBlobProperties:
                case StorageOperationTypes.GetBlobMetadata:
                case StorageOperationTypes.GetBlockList:
                    retval = await GetContainerPublicAccessAsync(requestUriParts.Container) != BlobContainerPublicAccessType.Off;
                    break;
            }
            return retval;
        }

        static async Task<BlobContainerPublicAccessType> GetContainerPublicAccessAsync(string container)
        {
            // TODO: Plug this potential DoS vector - spurious anonymous requests could drown us here...
            var containerObject = ControllerOperations.GetContainerByName(DashConfiguration.NamespaceAccount, container);
            var permissions = await containerObject.GetPermissionsAsync();
            return permissions.PublicAccess;
        }

    }
}