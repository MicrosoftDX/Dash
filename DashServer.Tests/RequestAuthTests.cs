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
        public class MockHttpRequestWrapper : IHttpRequestWrapper
        {
            public NameValueCollection Headers { get; set; }
            public Uri Url { get; set; }
            public string HttpMethod { get; set; }
        }


        [TestInitialize]
        public void Init()
        {
            TestUtils.InitializeConfig(new Dictionary<string, string>()
                {
                    { "AccountName", "dashstorage1" },
                    { "AccountKey", "8jqRVtXUWiEthgIhR+dFwrB8gh3lFuquvJQ1v4eabObIj7okI1cZIuzY8zZHmEdpcC0f+XlUkbFwAhjTfyrLIg==" },
                });
        }

        [TestMethod]
        public void SharedKeyRequestTest()
        {
            var request = new MockHttpRequestWrapper
            {
                HttpMethod = "GET",
                Url = new Uri("http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy"),
                Headers = new NameValueCollection(),
            };
            request.Headers.Add("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)");
            request.Headers.Add("x-ms-version", "2014-02-14");
            request.Headers.Add("x-ms-client-request-id", "adfb540e-1050-4c9b-a53a-be6cb71688d3");
            request.Headers.Add("x-ms-date", "Fri, 31 Oct 2014 22:50:34 GMT");
            request.Headers.Add("Authorization", "SharedKey dashstorage1:rB2yPGBXn3CtquDlk03An34hgRHIibK/Xv+hjG3r0Us=");
            request.Headers.Add("Host", "dashstorage1.blob.core.windows.net");
            request.Headers.Add("Connection", "Keep-Alive");

            Assert.IsTrue(RequestAuthorization.IsRequestAuthorized(request, true));
        }

        [TestMethod]
        public void SharedKeyLiteRequestTest()
        {
            var request = new MockHttpRequestWrapper
            {
                HttpMethod = "GET",
                Url = new Uri("http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy"),
                Headers = new NameValueCollection(),
            };
            request.Headers.Add("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)");
            request.Headers.Add("x-ms-version", "2014-02-14");
            request.Headers.Add("x-ms-client-request-id", "bbe0b567-f26e-405c-93e6-341ab6f6bb0e");
            request.Headers.Add("x-ms-date", "Fri, 31 Oct 2014 23:41:08 GMT");
            request.Headers.Add("Authorization", "SharedKeyLite dashstorage1:QG+WCbb0PMit6NZkOiCFNJIzDiz3jlv4oiOj8V6uQB8=");
            request.Headers.Add("Host", "dashstorage1.blob.core.windows.net");
            request.Headers.Add("Connection", "Keep-Alive");

            Assert.IsTrue(RequestAuthorization.IsRequestAuthorized(request, true));
        }
    }
}

