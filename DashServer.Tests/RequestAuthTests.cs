//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using Microsoft.Dash.Server.Handlers;
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
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutNumberOfAccounts", "1"},
                });
        }

        [TestMethod]
        public void SharedKeyRequestTest()
        {
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy", new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "adfb540e-1050-4c9b-a53a-be6cb71688d3"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 22:50:34 GMT"),
                Tuple.Create("Authorization", "SharedKey dashstorage1:rB2yPGBXn3CtquDlk03An34hgRHIibK/Xv+hjG3r0Us="),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
            }));
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

        static bool IsRequestAuthorized(string method, string uri, IEnumerable<Tuple<string, string>> headers = null)
        {
            return RequestAuthorization.IsRequestAuthorizedAsync(new MockHttpRequestWrapper(method, uri, headers), true).Result;
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

