//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Tests
{
    [TestClass]
    public class RequestAuthTests
    {
        [TestInitialize]
        public void Init()
        {
            WebApiTestRunner.InitializeConfig(new Dictionary<string, string>()
                {
                    { "AccountName", "dashstorage1" },
                    { "AccountKey", "8jqRVtXUWiEthgIhR+dFwrB8gh3lFuquvJQ1v4eabObIj7okI1cZIuzY8zZHmEdpcC0f+XlUkbFwAhjTfyrLIg==" },
                    { "SecondaryAccountKey", "Klari9ZbVdFQ35aULCfqqehCsd136amhusMHWynTpz2Pg+GyQMJw3GH177hvEQbaZ2oeRYk3jw0mIaV3ehNIRg==" },
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutNumberOfAccounts", "1"},
                });
        }

        [TestMethod]
        public void SharedKeyRequestTest()
        {
            var headers = new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "adfb540e-1050-4c9b-a53a-be6cb71688d3"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 22:50:34 GMT"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Content-Length", "0"),
            };
            var request = new MockHttpRequestWrapper("GET",
                "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy",
                headers.Concat(new[] { Tuple.Create("Authorization", "SharedKey dashstorage1:rB2yPGBXn3CtquDlk03An34hgRHIibK/Xv+hjG3r0Us=") }));
            Assert.IsTrue(IsRequestAuthorized(request));
            Assert.IsTrue(request.AuthenticationKey.SequenceEqual(Convert.FromBase64String("8jqRVtXUWiEthgIhR+dFwrB8gh3lFuquvJQ1v4eabObIj7okI1cZIuzY8zZHmEdpcC0f+XlUkbFwAhjTfyrLIg==")));
            Assert.AreEqual(request.AuthenticationScheme, "SharedKey");
            // Encoded URI
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test%20encoded",
                headers,
                "SharedKey dashstorage1:CVlJHA7FdyqJ75pPkufGavOsXEtzcr5RAWd3R5fEolA="));
            // For encoded uris, some clients generate the signature on the unencoded path (in violation of the documented spec), but 
            // storage supports it, so we have to as well.
            Assert.IsTrue(IsRequestAuthorized("PUT", 
                "http://localhost/container/test%20encoded", 
                headers,
                "SharedKey dashstorage1:w6D7S11x58ueIvRKEWZGe1MRVvMkQFO+18wPlfm6f+A="));
            // Alternate encoding - some punctuation characters are encoded as well
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test%20encoded(1)",
                headers,
                "SharedKey dashstorage1:RkJ/SQDzKdpAWVI1fJV+kK715YJ0gigRTnrQdQnx+TU="));
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test%20encoded%281%29",
                headers,
                "SharedKey dashstorage1:RkJ/SQDzKdpAWVI1fJV+kK715YJ0gigRTnrQdQnx+TU="));
            // Secondary key
            request = new MockHttpRequestWrapper("GET",
                "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy",
                headers.Concat(new[] { Tuple.Create("Authorization", "SharedKey dashstorage1:zmKvQUdxMH2OJWQygKOa950BULRSw6Dpue2zpE6TAAA=") }));
            Assert.IsTrue(IsRequestAuthorized(request));
            Assert.IsTrue(request.AuthenticationKey.SequenceEqual(Convert.FromBase64String("Klari9ZbVdFQ35aULCfqqehCsd136amhusMHWynTpz2Pg+GyQMJw3GH177hvEQbaZ2oeRYk3jw0mIaV3ehNIRg==")));
            // Invalid key
            Assert.IsFalse(IsRequestAuthorized("GET",
                "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy",
                headers,
                "SharedKey dashstorage1:ymKvQUdxMH2OJWQygKOa950BULRSw6Dpue2zpE6TAAA="));
        }

        [TestMethod]
        public void SharedKeyLiteRequestTest()
        {
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy", new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "bbe0b567-f26e-405c-93e6-341ab6f6bb0e"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 23:41:08 GMT"),
                Tuple.Create("Authorization", "SharedKeyLite dashstorage1:QG+WCbb0PMit6NZkOiCFNJIzDiz3jlv4oiOj8V6uQB8="),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
            }));
            // Secondary key
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy", new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "bbe0b567-f26e-405c-93e6-341ab6f6bb0e"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 23:41:08 GMT"),
                Tuple.Create("Authorization", "SharedKeyLite dashstorage1:uKfAsKUjr3KqNb8jeKVtNuNT7whE+caeAkuGsYWehD8="),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
            }));
            // Invalid key
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy", new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "bbe0b567-f26e-405c-93e6-341ab6f6bb0e"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 23:41:08 GMT"),
                Tuple.Create("Authorization", "SharedKeyLite dashstorage1:ymKvQUdxMH2OJWQygKOa950BULRSw6Dpue2zpE6TAAA="),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
            }));
        }

        [TestMethod]
        public void RedirectionSignatureTest()
        {
            var request = new MockHttpRequestWrapper("GET", "http://localhost/container/test%20encoded", null)
            {
                AuthenticationScheme = "SharedKey",
                AuthenticationKey = DashConfiguration.AccountKey,
            };
            var result = HandlerResult.Redirect(request,
                "http://dataaccount.blob.core.windows.net/container/test%20encoded");
            result.Headers = new ResponseHeaders(new[] {
                new KeyValuePair<string, string>("x-ms-date", "Wed, 01 Apr 2015 01:26:43 GMT"),
            });
            Assert.AreEqual(result.SignedLocation, "SharedKey dashstorage1:iU0kJCrvLR7rdIS/HXO0T04gTu09enDo25/3WtrjESI=");
            // Secondary key
            request = new MockHttpRequestWrapper("GET", "http://localhost/container/test%20encoded", null)
            {
                AuthenticationScheme = "SharedKeyLite",
                AuthenticationKey = DashConfiguration.SecondaryAccountKey,
            };
            result = HandlerResult.Redirect(request,
                "http://dataaccount.blob.core.windows.net/container/test%20encoded");
            result.Headers = new ResponseHeaders(new[] {
                new KeyValuePair<string, string>("x-ms-date", "Wed, 01 Apr 2015 01:26:43 GMT"),
            });
            Assert.AreEqual(result.SignedLocation, "SharedKeyLite dashstorage1:o3XAz28naFjcSxCbqoZL394S/zLY2+nYk7v8KbdnlSI=");
        }

        [Flags]
        enum ContainerAccess
        {
            None = 0x00,
            Private = 0x01,
            Container = 0x02,
            Blob = 0x04,
        }

        [TestMethod]
        public void AnonymousAccessTest()
        {
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/account?comp=list"));
            Assert.IsFalse(IsRequestAuthorized("PUT", "http://localhost/account?restype=service&comp=properties"));
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/account?restype=service&comp=properties"));
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/account?restype=service&comp=stats"));
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "", "restype=container");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "GET", "", "restype=container");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "HEAD", "", "restype=container");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "GET", "", "restype=container&comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "HEAD", "", "restype=container&comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "", "restype=container&comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "GET", "", "restype=container&comp=acl");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "HEAD", "", "restype=container&comp=acl");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "", "restype=container&comp=acl");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "", "restype=container&comp=lease");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "DELETE", "", "restype=container");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "GET", "", "restype=container&comp=list");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "GET",  "test.txt", "");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "HEAD", "test.txt", "");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=properties");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "GET", "test.txt", "comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "HEAD", "test.txt", "comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "DELETE", "test.txt", "");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=lease");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "", new[] { Tuple.Create("x-ms-copy-source", "http://localhost/blob/myblob/myblob2") });
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=copy");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=block");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=blocklist");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "GET", "test.txt", "comp=blocklist");
        }

        static bool IsRequestAuthorized(string method, string uri, IEnumerable<Tuple<string, string>> headers, string authString)
        {
            return IsRequestAuthorized(method, uri, headers.Concat(new[] { Tuple.Create("Authorization", authString) }));
        }

        static bool IsRequestAuthorized(string method, string uri, IEnumerable<Tuple<string, string>> headers = null)
        {
            return IsRequestAuthorized(new MockHttpRequestWrapper(method, uri, headers));
        }

        static bool IsRequestAuthorized(IHttpRequestWrapper request)
        {
            return RequestAuthorization.IsRequestAuthorizedAsync(request, true).Result;
        }

        static void EvaluateAnonymousContainerAccess(ContainerAccess expectedAccess, string method, string blob, string uriSuffix, IEnumerable<Tuple<string, string>> headers = null)
        {
            Assert.AreEqual(expectedAccess.HasFlag(ContainerAccess.Private), IsRequestAuthorized(method, FormatContainerAccessUri(ContainerAccess.Private, blob, uriSuffix), headers));
            Assert.AreEqual(expectedAccess.HasFlag(ContainerAccess.Container), IsRequestAuthorized(method, FormatContainerAccessUri(ContainerAccess.Container, blob, uriSuffix), headers));
            Assert.AreEqual(expectedAccess.HasFlag(ContainerAccess.Blob), IsRequestAuthorized(method, FormatContainerAccessUri(ContainerAccess.Blob, blob, uriSuffix), headers));
        }

        static string FormatContainerAccessUri(ContainerAccess accessTest, string blob, string uriSuffix)
        {
            return String.Format("http://localhost/{0}/{1}/{2}?{3}",
                String.IsNullOrEmpty(blob) ? "container" : "blob",
                accessTest == ContainerAccess.Private ? "test" : (accessTest == ContainerAccess.Container ? "anonymouscontainertest" : "anonymousblobtest"),
                blob,
                uriSuffix);
        }

    }
}

