﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Dash.Common.Diagnostics;

namespace Microsoft.Dash.Common.Utils
{
    public static class SharedKeySignature
    {
        public const string AlgorithmSharedKey      = "SharedKey";
        public const string AlgorithmSharedKeyLite  = "SharedKeyLite";

        public static readonly string AccountName   = DashConfiguration.AccountName;
        public static readonly byte[] AccountKey    = DashConfiguration.AccountKey;

        public static string GenerateSignature(Func<string> stringToSignFactory)
        {
            string stringToSign = stringToSignFactory();
            DashTrace.TraceInformation("Authentication signing string: {0}", stringToSign);

            var bytesToSign = Encoding.UTF8.GetBytes(stringToSign);
            using (var hmac = new HMACSHA256(SharedKeySignature.AccountKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(bytesToSign));
            }
        }

        public static string GetCanonicalizedHeaders(ILookup<string, string> headers)
        {
            return String.Join("\n", headers
                .Where(header => header.Key.StartsWith("x-ms-") &&
                                 header.Any(headerValue => !String.IsNullOrWhiteSpace(headerValue)))
                .OrderBy(header => header.Key, StringComparer.Create(new CultureInfo("en-US"), false))
                .Select(header => FormatCanonicalizedValues(header)));
        }

        public static string FormatCanonicalizedValues(IGrouping<string, string> headerOrParameter)
        {
            return headerOrParameter.Key.ToLowerInvariant() + ":" +
                String.Join(",", headerOrParameter
                    .Select(value => value.TrimStart().Replace("\r\n", String.Empty))
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        }

        public static string FormatSignatureHeader(string authScheme, string signature)
        {
            return String.Format("{0} {1}:{2}", authScheme, SharedKeySignature.AccountName, signature);
        }
    }
}
