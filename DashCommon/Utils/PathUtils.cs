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
            if (_IsUrlSafeChar == null || String.IsNullOrWhiteSpace(path))
            {
                return path;
            }
            // Don't use the standard encoding helpers here as they universally encode a space with '+' which seems
            // to screw with storage (it ends up creating blobs with + in the name).
            bool encoding = false;
            char[] encodedRetval = null;
            int encodedLength = 0;
            for (int index = 0; index < path.Length; index++)
            {
                char ch = path[index];
                if (ch != StandardPathDelimiterChar && !(bool)_IsUrlSafeChar.Invoke(null, new object[] { ch }))
                {
                    if (!encoding)
                    {
                        encodedRetval = new char[index + (path.Length - index) * 3];
                        if (index > 0)
                        {
                            path.CopyTo(0, encodedRetval, 0, index);
                        }
                        encoding = true;
                        encodedLength = index;
                    }
                    encodedRetval[encodedLength++] = '%';
                    encodedRetval[encodedLength++] = IntToHex((ch >> 4) & 15);
                    encodedRetval[encodedLength++] = IntToHex(ch & 15);
                }
                else if (encoding)
                {
                    encodedRetval[encodedLength++] = ch;
                }
            }
            if (!encoding)
            {
                return path;
            }
            return new string(encodedRetval, 0, encodedLength);
        }

        static char IntToHex(int n)
        {
            if (n <= 9)
            {
                return (char)(n + 0x30);
            }
            return (char)((n - 10) + 0x41);
        }
    }
}
