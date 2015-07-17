//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Microsoft.Dash.Common.Utils
{
    public static class PathUtils
    {
        // TODO: Possibly move this to config in future - however, still need to handle per request
        const string StandardPathDelimiter              = "/";
        static readonly char StandardPathDelimiterChar  = StandardPathDelimiter[0];

        public static string CombineContainerAndBlob(string container, string blobName, bool leadingDelimiter = false)
        {
            blobName = blobName.TrimStart(StandardPathDelimiterChar);
            return (leadingDelimiter ? StandardPathDelimiter : String.Empty) + 
                container +
                (String.IsNullOrWhiteSpace(blobName) ? String.Empty : StandardPathDelimiter + blobName);
        }

        public static string CombinePathSegments(IEnumerable<string> segments)
        {
            return String.Join(StandardPathDelimiter, segments);
        }

        public static string[] GetPathSegments(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return new string[0];
            }
            return path.Split(new[] { StandardPathDelimiterChar }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string AddPathSegment(string path, string pathToAdd)
        {
            var segments = PathUtils.GetPathSegments(path)
                .ToList();
            segments.Add(pathToAdd);
            return CombinePathSegments(segments);
        }

        static readonly MethodInfo _IsUrlSafeChar = typeof(WebUtility)
            .GetMethod("IsUrlSafeChar", BindingFlags.Static | BindingFlags.NonPublic);

        public static string PathEncode(string path)
        {
            // Pulling apart the path, encoding each segment & re-assembling it is a potentially expensive operation.
            // It's worth doing the pre-check, even if it involves scanning the string twice
            if (String.IsNullOrWhiteSpace(path))
            {
                return path;
            }
            if (_IsUrlSafeChar != null &&
                !path.Any(ch => ch != StandardPathDelimiterChar && !(bool)_IsUrlSafeChar.Invoke(null, new object[] { ch })))
            {
                return path;
            }
            var segments = GetPathSegments(path)
                .Select(segment => WebUtility.UrlEncode(segment))
                .ToArray();
            return String.Join(StandardPathDelimiter, segments);
        }
    }
}
