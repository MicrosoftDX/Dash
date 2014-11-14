//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dash.Server.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Tests
{
    [TestClass]
    public class SignatureValidationTests
    {
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
        public void SharedKeySignatureTest()
        {
            // Taken directly from the payload emitted by the .NET Storage FX
            var headers = new List<Tuple<string, string>>()
            {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("Content-Length", "423"),
                Tuple.Create("Content-MD5", "LHmMkMS8mTXh0bRP4e4Ptw=="),
                Tuple.Create("Content-MD5", "LHmMkMS8mTXh0bRP4e4Ptw=="),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-client-request-id", "82370d95-e820-4817-bd3f-6c266544ee8b"),
                Tuple.Create("x-ms-date", "Thu, 30 Oct 2014 17:31:22 GMT"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Expect", "100-continue"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
            };
            Assert.AreEqual("mDTyDIDy8yt7D81qAmYEYiXH1X/bmBHJF2+ZPn/r74k=",
                RequestAuthorization.SharedKeySignature(false, "PUT", "/test/test.txt", RequestHeaders.Create(headers.ToLookup(header => header.Item1, header => header.Item2)), RequestQueryParameters.Empty, ""),
                "Generated SharedKey signature should match expected one 1");

            // Taken directly from the payload emitted by the .NET Storage FX
            headers = new List<Tuple<string, string>>()
            {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("Content-Length", "423"),
                Tuple.Create("Content-MD5", "LHmMkMS8mTXh0bRP4e4Ptw=="),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-client-request-id", "TestRequest1"),
                Tuple.Create("x-ms-date", "Thu, 30 Oct 2014 19:48:43 GMT"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-test", "testvalue"),
                Tuple.Create("x-test", "differentvalue"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Expect", "100-continue"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
            };
            Assert.AreEqual("ktMmzuta7Wsqtzyd3TBbd3zW4miedtJPkWWlW9DCRb0=",
                RequestAuthorization.SharedKeySignature(false, "PUT", "/test/test.txt", RequestHeaders.Create(headers.ToLookup(header => header.Item1, header => header.Item2)), RequestQueryParameters.Empty, ""),
                "Generated SharedKey signature should match expected one 2");

            // Taken directly from the payload emitted by the .NET Storage FX
            headers = new List<Tuple<string, string>>()
            {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("Content-Length", "423"),
                Tuple.Create("Content-MD5", "LHmMkMS8mTXh0bRP4e4Ptw=="),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-client-request-id", "d0863cd4-ae43-4dd4-aa27-eff01d30b5cf"),
                Tuple.Create("x-ms-date", "Thu, 30 Oct 2014 20:15:46 GMT"),
                Tuple.Create("If-Modified-Since", "Thu, 30 Oct 2014 20:15:41 GMT"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Expect", "100-continue"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
            };
            Assert.AreEqual("21rAYhnoRQQu6MmxXj6W5Rx3YWI8OCjO50CNNniohAA=",
                RequestAuthorization.SharedKeySignature(false, "PUT", "/test/test.txt", RequestHeaders.Create(headers.ToLookup(header => header.Item1, header => header.Item2)), RequestQueryParameters.Empty, ""),
                "Generated SharedKey signature should match expected one 3");

            // Taken directly from the payload emitted by the .NET Storage FX
            headers = new List<Tuple<string, string>>()
            {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("Content-Type", "application/octet-stream"),
                Tuple.Create("Content-MD5", "LHmMkMS8mTXh0bRP4e4Ptw=="),
                Tuple.Create("Content-Language", "EN-US"),
                Tuple.Create("Content-Encoding", "gzip"),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("If-Match", "DummyETag"),
                Tuple.Create("x-ms-client-request-id", "93399f8e-6ce2-4020-81a4-4f1f53f427fd"),
                Tuple.Create("x-ms-date", "Thu, 30 Oct 2014 20:46:48 GMT"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Content-Length", "423"),
                Tuple.Create("Expect", "100-continue"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
            };
            Assert.AreEqual("ZBQYgC9dgzldVfH6t06qIYZujSz/3blWrinuYePTMv8=",
                RequestAuthorization.SharedKeySignature(false, "PUT", "/test/test.txt", RequestHeaders.Create(headers.ToLookup(header => header.Item1, header => header.Item2)), RequestQueryParameters.Empty, ""),
                "Generated SharedKey signature should match expected one 4");

            // Taken directly from the payload emitted by the .NET Storage FX
            headers = new List<Tuple<string, string>>()
            {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "9e51b5a3-2dff-4fb2-98f8-c7cfe05a223a"),
                Tuple.Create("x-ms-date", "Thu, 30 Oct 2014 20:55:13 GMT"),
                Tuple.Create("Content-Length", "0"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Expect", "100-continue"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
            };
            var queryParams = new List<Tuple<string, string>>()
            {
                Tuple.Create("restype", "container"),
                Tuple.Create("comp", "list"),
                Tuple.Create("prefix", "te"), 
                Tuple.Create("include", "snapshots"),
                Tuple.Create("INCLUDE", "uncommittedblobs,metadata,copy"),
            };
            Assert.AreEqual("C0mPsPAiMs+4qlDQXGXMXpRGUkQgyMLzOHBiCmoC4LE=",
                RequestAuthorization.SharedKeySignature(false, 
                    "GET", 
                    "/test", 
                    RequestHeaders.Create(headers.ToLookup(header => header.Item1, header => header.Item2)), 
                    RequestQueryParameters.Create(queryParams.ToLookup(queryParam => queryParam.Item1, queryParam => queryParam.Item2)),
                    ""),
                "Generated SharedKey signature should match expected one 5");
        }

        [TestMethod]
        public void SharedKeyLiteSignatureTest()
        {
            // Taken directly from the payload emitted by the .NET Storage FX
            var headers = new List<Tuple<string, string>>()
            {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "5bdcb4d9-bd21-44a6-a086-24d9fa9ec2b7"),
                Tuple.Create("x-ms-date", "Thu, 30 Oct 2014 21:32:29 GMT"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Expect", "100-continue"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
            };
            var queryParams = new List<Tuple<string, string>>()
            {
                Tuple.Create("restype", "container"),
                Tuple.Create("comp", "list"), 
                Tuple.Create("prefix", "te"), 
                Tuple.Create("include", "snapshots"), 
                Tuple.Create("INCLUDE", "uncommittedblobs,metadata,copy")
            };
            Assert.AreEqual("+C5fkZr8g80qF5vzPeLCnuaaA2pG8jTZRdmhKkjZt2g=",
                RequestAuthorization.SharedKeySignature(true, 
                    "GET", 
                    "/test",
                    RequestHeaders.Create(headers.ToLookup(header => header.Item1, header => header.Item2)),
                    RequestQueryParameters.Create(queryParams.ToLookup(queryParam => queryParam.Item1, queryParam => queryParam.Item2)),
                    ""),
                "Generated SharedKeyLite signature should match expected one 1");

            // Taken directly from the payload emitted by the .NET Storage FX
            headers = new List<Tuple<string, string>>()
            {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("Content-Type", "application/octet-stream"),
                Tuple.Create("Content-MD5", "LHmMkMS8mTXh0bRP4e4Ptw=="),
                Tuple.Create("Content-Language", "EN-US"),
                Tuple.Create("Content-Encoding", "gzip"),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("If-Match", "DummyETag"),
                Tuple.Create("x-ms-client-request-id", "f71ce734-540f-44a6-a68e-7acaa6730daa"),
                Tuple.Create("x-ms-date", "Thu, 30 Oct 2014 21:53:09 GMT"),
                Tuple.Create("Content-Length", "423"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Expect", "100-continue"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
            };
            Assert.AreEqual("0y2rS4dabAWCahy2pTzrr1liZ7uSkJkYv/C3aYAgvpg=",
                RequestAuthorization.SharedKeySignature(true, "PUT", "/test/test.txt", RequestHeaders.Create(headers.ToLookup(header => header.Item1, header => header.Item2)), RequestQueryParameters.Empty, ""),
                "Generated SharedKeyLite signature should match expected one 1");


        }
    }
}










