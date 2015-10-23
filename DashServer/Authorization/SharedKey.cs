//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Authorization
{
    public static class SharedKey
    {
        public static bool IsRequestType(RequestHeaders headers)
        {
            return headers.Contains("Authorization");
        }

        public static bool IsAuthorized(IHttpRequestWrapper request, RequestHeaders headers, RequestQueryParameters queryParams, bool ignoreRequestAge)
        {
            // Quick request age check
            var authHeader = headers.Value<string>("Authorization");
            DateTimeOffset requestVersion = headers.RequestVersion;
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
            else if (parts[0] != SharedKeySignature.AlgorithmSharedKey && parts[0] != SharedKeySignature.AlgorithmSharedKeyLite)
            {
                return false;
            }
            var account = parts[1];
            var signature = parts[2];

            if (!String.Equals(account, SharedKeySignature.AccountName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            // We have to deal with multiple encodings (the spec is a bit ambiguous on what an 'encoded' path actually is).
            // Only run the validation if the encodings result in different strings
            var requestUriParts = request.UriParts;
            var pathsToCheck = new List<string>() { requestUriParts.OriginalUriPath };
            var unencodedPath = requestUriParts.PublicUriPath;
            if (unencodedPath != pathsToCheck[0])
            {
                pathsToCheck.Add(unencodedPath);
            }
            var alternateEncodingPaths = AlternateEncodeString(pathsToCheck[0]);
            if (alternateEncodingPaths != null)
            {
                pathsToCheck.AddRange(alternateEncodingPaths);
            }
            // For some verbs we can't tell if the Content-Length header was specified as 0 or that IIS/UrlRewrite/ASP.NET has constructed
            // the header value for us. The difference is significant to the signature as content length is included for SharedKey.
            // The ambiguity has been resolved in version 2015-02-21
            bool fullKeyAlgorithm = parts[0] == SharedKeySignature.AlgorithmSharedKey;
            bool runBlankContentLengthComparison = false;
            string method = request.HttpMethod.ToUpper();
            var contentLength = headers.Value("Content-Length", String.Empty);
            if (fullKeyAlgorithm)
            {
                int length;
                if (!int.TryParse(contentLength, out length) || length <= 0)
                {
                    if (requestVersion >= StorageServiceVersions.Version_2015_02_21)
                    {
                        // This version made it explicit what to do with 0 content-length
                        contentLength = String.Empty;
                        runBlankContentLengthComparison = false;
                    }
                    else
                    {
                        // Preserve a Content-Length: 0 header for PUT methods
                        runBlankContentLengthComparison = !method.Equals(WebRequestMethods.Http.Put, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            var stringsToCheck = pathsToCheck.SelectMany(uriPath => new[] {
                    runBlankContentLengthComparison ? Tuple.Create(uriPath, String.Empty) : null,
                    Tuple.Create(uriPath, contentLength),
                })
                .Where(check => check != null)
                .Select(pathWithContentLength => GetStringToSign(!fullKeyAlgorithm, method, pathWithContentLength.Item1, headers, queryParams, dateHeader, pathWithContentLength.Item2))
                .SelectMany(stringToSign => new[] { Tuple.Create(true, stringToSign), Tuple.Create(false, stringToSign) });
            var evaluationResult = stringsToCheck
                .FirstOrDefault(validatationCheck => VerifyRequestAuthorization(signature, validatationCheck.Item1, validatationCheck.Item2));
            if (evaluationResult != null)
            {
                // Remember the Auth Scheme & Key for when we have to sign the response
                request.AuthenticationScheme = parts[0];
                request.AuthenticationKey = evaluationResult.Item1 ? SharedKeySignature.PrimaryAccountKey : SharedKeySignature.SecondaryAccountKey;
                return true;
            }
            DashTrace.TraceWarning("Failed to authenticate SharedKey request: {0}:{1}:{2}:{3}:{4}", parts[0], account, method, request.Url, signature);

            return false;
        }

        public static string GenerateSignature(bool usePrimaryKey, bool liteAlgorithm, string method, string uriPath,
            RequestHeaders headers, RequestQueryParameters queryParams, string requestDate, string contentLength)
        {
            return SharedKeySignature.GenerateSignature(
                GetStringToSign(liteAlgorithm, method, uriPath, headers, queryParams, requestDate, contentLength),
                usePrimaryKey);
        }

        private static bool VerifyRequestAuthorization(string signature, bool usePrimaryKey, string stringToSign)
        {
            if (!SharedKeySignature.HasKey(usePrimaryKey))
            {
                return false;
            }
            else if (signature == SharedKeySignature.GenerateSignature(stringToSign, usePrimaryKey))
            {
                return true;
            }
            return false;
        }

        static IEnumerable<string> AlternateEncodeString(string source)
        {
            // Some clients (eg. Azure Management Studio) are very aggressive in encoding the resource path which becomes part of the string
            // which they sign. We have to generate alternate encoded strings (in both upper & lower case hex encoding).
            for (int pos = 0; pos < source.Length; pos++)
            {
                if (Uri.IsHexEncoding(source, pos))
                {
                    // Move the index over the hex-encoded character
                    Uri.HexUnescape(source, ref pos);
                    pos--;
                }
                else if (IsAlternateEncodingCharacter(source[pos]))
                {
                    return AlternateEncodeStringTranslate(source, pos);
                }

            }
            return null;
        }

        static string[] AlternateEncodeStringTranslate(string source, int pos)
        {
            // Allocate enough capacity here to handle worst-case of all remaining characters needing hex encoding
            // without StringBuilding needing to reallocate its buffer
            string sourceSubstring = source.Substring(0, pos);
            var upperCaseEncoded = new StringBuilder(sourceSubstring, pos + (source.Length - pos) * 3);
            var lowerCaseEncoded = new StringBuilder(sourceSubstring, upperCaseEncoded.Capacity);
            for (; pos < source.Length; pos++)
            {
                char ch = source[pos];
                if (IsAlternateEncodingCharacter(ch))
                {
                    string encoded = Uri.HexEscape(ch);
                    upperCaseEncoded.Append(encoded);
                    lowerCaseEncoded.Append(encoded.ToLowerInvariant());
                }
                else
                {
                    upperCaseEncoded.Append(ch);
                    lowerCaseEncoded.Append(ch);
                }
            }
            return new[] {
                upperCaseEncoded.ToString(),
                lowerCaseEncoded.ToString()
            };
        }

        // This is the set of sub-delims reserved characters defined in RFC3986 (minus the ' character for unknown reasons)
        const string _alternateEncodingMarks = "[]@!$&()*+,;=";
        static bool IsAlternateEncodingCharacter(char ch)
        {
            return _alternateEncodingMarks.IndexOf(ch) != -1;
        }

        static string GetStringToSign(bool liteAlgorithm, string method, string uriPath,
            RequestHeaders headers, RequestQueryParameters queryParams, string requestDate, string contentLength)
        {
            // Signature scheme is described at: http://msdn.microsoft.com/en-us/library/azure/dd179428.aspx
            // and the SDK implementation is at: https://github.com/Azure/azure-storage-net/tree/master/Lib/ClassLibraryCommon/Auth/Protocol
            if (liteAlgorithm)
            {
                return String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}",
                                    method,
                                    headers.Value("Content-MD5", String.Empty),
                                    headers.Value("Content-Type", String.Empty),
                                    requestDate,
                                    SharedKeySignature.GetCanonicalizedHeaders(headers),
                                    GetCanonicalizedResource(liteAlgorithm, uriPath, queryParams, SharedKeySignature.AccountName));
            }
            else
            {
                return String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n{6}\n{7}\n{8}\n{9}\n{10}\n{11}\n{12}\n{13}",
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
                                    SharedKeySignature.GetCanonicalizedHeaders(headers),
                                    GetCanonicalizedResource(liteAlgorithm, uriPath, queryParams, SharedKeySignature.AccountName));
            }
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
                        .Select(queryParam => SharedKeySignature.FormatCanonicalizedValues(queryParam)));
                }
                return commonPrefix;
            }
        }
    }
}