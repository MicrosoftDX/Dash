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

        public static IEnumerable<string> GetPathSegments(Uri uri)
        {
            // Manually parse the uri - there's no methods on the Uri class which give us the path section of an absolute or relative uri based
            // on the OriginalString (which is what we want the segments for as we don't want 'automagic' decoding).
            string path = uri.OriginalString;
            if (uri.IsAbsoluteUri)
            {
                // Given that the UriParser has stated that this is an absolute uri, we can carry forward some assumptions (ie. the path will start at the 3rd backslash)
                int startPos = 0, seenSlashes = 3;
                for (int index = 0; seenSlashes > 0 && index < path.Length; index++)
                {
                    if (path[index] == StandardPathDelimiterChar)
                    {
                        seenSlashes--;
                        startPos = index;
                    }
                }
                if (seenSlashes == 0)
                {
                    path = path.Substring(startPos);
                }
                else
                {
                    path = String.Empty;
                }
            }
            return GetPathSegments(path);
        }

        public static IEnumerable<string> GetPathSegments(string path)
        {
            int startIdx, currentIdx;
            for (startIdx = 0, currentIdx = 0; currentIdx < path.Length; currentIdx++)
            {
                char ch = path[currentIdx];
                if (ch == StandardPathDelimiterChar)
                {
                    if (currentIdx > startIdx)
                    {
                        yield return path.Substring(startIdx, currentIdx - startIdx);
                    }
                    startIdx = currentIdx + 1;
                }
                else if (ch == '?')
                {
                    break;
                }
            }
            if (currentIdx > startIdx)
            {
                yield return path.Substring(startIdx, currentIdx - startIdx);
            }
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

        public static string PathDecode(string path)
        {
            // Again, the utility functions converting '+' -> ' ' is invalid for storage. Mitigate by pre-processing the string.
            // This is a little inefficient as we are processing the string twice, but it is the most thorough
            // implementation as it deals with the full unicode character set
            return WebUtility.UrlDecode(path.Replace("+", "%2B"));
        }

        static char IntToHex(int n)
        {
            if (n <= 9)
            {
                return (char)(n + (int)'0');
            }
            return (char)(n - 10 + (int)'A');
        }
    }
}
