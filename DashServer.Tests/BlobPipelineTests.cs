//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Microsoft.Dash.Server.Utils;
using Microsoft.Dash.Server.Handlers;
using System.Net;
using System.Web;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobPipelineTests : PipelineTestBase
    {
        [TestInitialize]
        public void Init()
        {
            InitializeConfig(new Dictionary<string, string>()
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestdata2;AccountKey=OOXSVWWpImRf79sbiEtpIwFsggv7VAhdjtKdt7o0gOLr2krzVXwZ+cb/gJeMqZRlXHTniRN6vnKKjs1glijihA==" },
                    { "ScaleoutNumberOfAccounts", "2"},
                });
        }

        [TestMethod]
        public void GetBlobPipelineTest()
        {
            var result = BlobRequest("GET", "http://localhost/blob/test/test.txt");
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var location = new Uri(result.Location);
            Assert.AreEqual("http://dashtestdata1.blob.core.windows.net/test/test.txt", location.GetLeftPart(UriPartial.Path));
            var redirectQueryParams = HttpUtility.ParseQueryString(location.Query);
            Assert.AreEqual("2014-02-14", redirectQueryParams["sv"]);
            Assert.IsNotNull(redirectQueryParams["sig"]);
            Assert.IsNotNull(redirectQueryParams["se"]);
        }

        [TestMethod]
        public void GetNonExistingBlobPipelineTest()
        {
            var result = BlobRequest("GET", "http://localhost/blob/test/test1.txt");
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        [TestMethod]
        public void PutExistingBlobPipelineTest()
        {
            var result = BlobRequest("PUT", "http://localhost/blob/test/test.txt", new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-blob-content-disposition", "attachment; filename=\"test.txt\""),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-meta-m1", "v1"),
                Tuple.Create("x-ms-meta-m2", "v2"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var location = new Uri(result.Location);
            Assert.AreEqual("http://dashtestdata1.blob.core.windows.net/test/test.txt", location.GetLeftPart(UriPartial.Path));
        }

        [TestMethod]
        public void PutNonExistingBlobPipelineTest()
        {
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            var result = BlobRequest("PUT", blobUri, new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-blob-content-disposition", "attachment, filename=\"fname.ext\""),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-meta-m1", "v1"),
                Tuple.Create("x-ms-meta-m2", "v2"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var redirectLocation = new Uri(result.Location).GetLeftPart(UriPartial.Path);
            // Get it back & verify we get redirected to the same location
            result = BlobRequest("GET", blobUri);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));

            // Cleanup - TODO
        }

        [TestMethod]
        public void EncodedBlobNamePipelineTest()
        {
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString() + "/workernode2.jokleinhbase.d6.internal.cloudapp.net,60020,1436223739284/workernode2.jokleinhbase.d6.internal.cloudapp.net%2C60020%2C1436223739284.1436223741878";
            var result = BlobRequest("PUT", blobUri, new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-blob-content-disposition", "attachment, filename=\"fname.ext\""),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-meta-m1", "v1"),
                Tuple.Create("x-ms-meta-m2", "v2"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var redirectLocation = new Uri(result.Location).GetLeftPart(UriPartial.Path);
            // Get it back & verify we get redirected to the same location
            result = BlobRequest("GET", blobUri);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));

            // Cleanup - TODO
        }

        [TestMethod]
        public void BlobPropertiesAndMetadataPipelineTest()
        {
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            string metadataUri = blobUri + "?comp=metadata";
            var result = BlobRequest("PUT", blobUri);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var redirectLocation = new Uri(result.Location).GetLeftPart(UriPartial.Path);
            // Get Blob Properties            
            result = BlobRequest("HEAD", blobUri);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));
            // Set Blob Properties
            result = BlobRequest("PUT", blobUri + "?comp=properties", new[] {
                Tuple.Create("x-ms-blob-content-encoding", "application/csv"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));

            // Get Blob Metadata
            result = BlobRequest("HEAD", metadataUri);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));
            result = BlobRequest("GET", metadataUri);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));
            // Set Blob Metadata
            result = BlobRequest("PUT", metadataUri, new[] {
                Tuple.Create("x-ms-meta-m1", "v1"),
                Tuple.Create("x-ms-meta-m2", "v2"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));

            // Cleanup - TODO
        }

        static string GetBlockId()
        {
            return HttpUtility.UrlEncode(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        }

        [TestMethod]
        public void PutBlobBlockControllerTest()
        {
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            string blockBlobUri = blobUri + "?comp=block&blockid=";
            string blockId1 = GetBlockId();
            string blockId2 = GetBlockId();
            var result = BlobRequest("PUT", blockBlobUri + blockId1);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var redirectLocation = new Uri(result.Location).GetLeftPart(UriPartial.Path);
            // 2nd block - now an existing blob
            result = BlobRequest("PUT", blockBlobUri + blockId2);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));
            // Commit the blocks
            result = BlobRequest("PUT", blobUri + "?comp=blocklist");
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));

            // Cleanup - TODO
        }

        [TestMethod]
        public void PutExistingPageBlobPipelineTest()
        {
            var result = BlobRequest("PUT", "http://localhost/blob/test/pagetest.txt?comp=page", new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-range", "bytes=1024-2047"),
                Tuple.Create("Content-Length", "1024"),
                Tuple.Create("x-ms-page-write", "update"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var location = new Uri(result.Location);
            Assert.AreEqual("http://dashtestdata1.blob.core.windows.net/test/pagetest.txt", location.GetLeftPart(UriPartial.Path));
        }

        [TestMethod]
        public void PutNonExistingPageBlobPipelineTest()
        {
            var result = BlobRequest("PUT", "http://localhost/blob/test/non-existant-page-test.txt?comp=page", new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-range", "bytes=1024-2047"),
                Tuple.Create("Content-Length", "1024"),
                Tuple.Create("x-ms-page-write", "update"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            });
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        [TestMethod]
        public void GetPageBlobPipelineTest()
        {
            var result = BlobRequest("GET", "http://localhost/blob/test/pagetest.txt?comp=pagelist");
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var location = new Uri(result.Location);
            Assert.AreEqual("http://dashtestdata1.blob.core.windows.net/test/pagetest.txt", location.GetLeftPart(UriPartial.Path));
        }

        [TestMethod]
        public void GetNonExistingPageBlobPipelineTest()
        {
            var result = BlobRequest("GET", "http://localhost/blob/test/non-existant-page-test.txt?comp=pagelist");
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        [TestMethod]
        public void BlobPipelineForwardRequestsTest()
        {
            var result = BlobRequest("GET", "http://localhost/blob/test/test.txt", new Tuple<string, string>[] { });
            Assert.IsNull(result);

            result = BlobRequest("PUT", "http://localhost/blob/test/test.txt", new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-blob-content-disposition", "attachment; filename=\"test.txt\""),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-meta-m1", "v1"),
                Tuple.Create("x-ms-meta-m2", "v2"),
            });
            Assert.IsNull(result);

            result = BlobRequest("PUT", "http://localhost/blob/test/test.txt", new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-blob-content-disposition", "attachment; filename=\"test.txt\""),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-meta-m1", "v1"),
                Tuple.Create("x-ms-meta-m2", "v2"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
            });
            Assert.IsNull(result);
        }
    }
}
