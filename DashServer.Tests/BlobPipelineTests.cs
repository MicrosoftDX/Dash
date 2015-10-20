//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using Microsoft.Dash.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobPipelineTests : PipelineTestBase
    {
        static DashTestContext _ctx;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ctx = InitializeConfigAndCreateTestBlobs(ctx, "datax2", new Dictionary<string, string>(), 
                new[] {
                    TestBlob.DefineBlob("test.txt"),
                    TestBlob.DefineBlob("pagetest.txt", blobType: BlobType.PageBlob),
                });
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupTestBlobs(_ctx);
        }

        [TestMethod]
        public void GetBlobPipelineTest()
        {
            var result = BlobRequest("GET", _ctx.GetBlobUri("test.txt"));
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var location = new Uri(result.Location);
            var account = location.Host.Split('.')[0];
            Assert.IsTrue(DashConfiguration.DataAccounts
                .Any(dataAccount => String.Equals(dataAccount.Credentials.AccountName, account, StringComparison.OrdinalIgnoreCase)));
            var redirectQueryParams = HttpUtility.ParseQueryString(location.Query);
            Assert.AreEqual("2014-02-14", redirectQueryParams["sv"]);
            Assert.IsNotNull(redirectQueryParams["sig"]);
            Assert.IsNotNull(redirectQueryParams["se"]);
            Assert.IsTrue(DateTimeOffset.Parse(redirectQueryParams["st"]) < DateTimeOffset.UtcNow);
            Assert.IsTrue(DateTimeOffset.Parse(redirectQueryParams["se"]) > DateTimeOffset.UtcNow);
        }

        [TestMethod]
        public void GetNonExistingBlobPipelineTest()
        {
            var result = BlobRequest("GET", _ctx.GetBlobUri("fredflinstone.txt"));
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        [TestMethod]
        public void PutExistingBlobPipelineTest()
        {
            var result = BlobRequest("PUT", _ctx.GetBlobUri("test.txt"), new[] {
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
            var account = location.Host.Split('.')[0];
            Assert.IsTrue(DashConfiguration.DataAccounts
                .Any(dataAccount => String.Equals(dataAccount.Credentials.AccountName, account, StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void PutNonExistingBlobPipelineTest()
        {
            string blobUri = _ctx.GetBlobUri(Guid.NewGuid().ToString());
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
        }

        [TestMethod]
        public void SpecialBlobNamePipelineTest()
        {
            var createHeaders = new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-blob-content-disposition", "attachment, filename=\"fname.ext\""),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-meta-m1", "v1"),
                Tuple.Create("x-ms-meta-m2", "v2"),
                Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                Tuple.Create("Expect", "100-Continue")
            };
            string blobUri = _ctx.GetBlobUri(Guid.NewGuid().ToString()) + "/workernode2.jokleinhbase.d6.internal.cloudapp.net,60020,1436223739284/workernode2.jokleinhbase.d6.internal.cloudapp.net%2C60020%2C1436223739284.1436223741878";
            var result = BlobRequest("PUT", blobUri, createHeaders);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var redirectLocation = new Uri(result.Location).GetLeftPart(UriPartial.Path);
            // Get it back & verify we get redirected to the same location
            result = BlobRequest("GET", blobUri);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));

            // Blob name with single character names
            string blobName = Guid.NewGuid().ToString() + "/1";
            blobUri = _ctx.GetBlobUri(blobName);
            result = BlobRequest("PUT", blobUri, createHeaders);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            string createdPath = new Uri(result.Location).AbsolutePath;
            redirectLocation = new Uri(result.Location).GetLeftPart(UriPartial.Path);
            Assert.AreEqual("/" + _ctx.ContainerName + "/" + blobName, createdPath);
            result = BlobRequest("GET", blobUri);
            Assert.AreEqual(redirectLocation, new Uri(result.Location).GetLeftPart(UriPartial.Path));
        }

        [TestMethod]
        public void BlobPropertiesAndMetadataPipelineTest()
        {
            string blobUri = _ctx.GetBlobUri(Guid.NewGuid().ToString());
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
        }

        static string GetBlockId()
        {
            return HttpUtility.UrlEncode(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        }

        [TestMethod]
        public void PutBlobBlockControllerTest()
        {
            string blobUri = _ctx.GetBlobUri(Guid.NewGuid().ToString());
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
        }

        [TestMethod]
        public void PutExistingPageBlobPipelineTest()
        {
            var result = BlobRequest("PUT", _ctx.GetBlobUri("pagetest.txt") + "?comp=page", new[] {
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
            var account = location.Host.Split('.')[0];
            Assert.IsTrue(DashConfiguration.DataAccounts
                .Any(dataAccount => String.Equals(dataAccount.Credentials.AccountName, account, StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void PutNonExistingPageBlobPipelineTest()
        {
            var result = BlobRequest("PUT", _ctx.GetBlobUri("non-existant-page-test.txt") + "?comp=page", new[] {
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
            var result = BlobRequest("GET", _ctx.GetBlobUri("pagetest.txt") + "?comp=pagelist");
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
            var location = new Uri(result.Location);
            var account = location.Host.Split('.')[0];
            Assert.IsTrue(DashConfiguration.DataAccounts
                .Any(dataAccount => String.Equals(dataAccount.Credentials.AccountName, account, StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void GetNonExistingPageBlobPipelineTest()
        {
            var result = BlobRequest("GET", _ctx.GetBlobUri("non-existant-page-test.txt") + "?comp=pagelist");
            Assert.IsNotNull(result);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
        }

        [TestMethod]
        public void BlobPipelineForwardRequestsTest()
        {
            var result = BlobRequest("GET", _ctx.GetBlobUri("test.txt"), new Tuple<string, string>[] { });
            Assert.IsNull(result);

            result = BlobRequest("PUT", _ctx.GetBlobUri("test.txt"), new[] {
                Tuple.Create("x-ms-version", "2013-08-15"),
                Tuple.Create("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT"),
                Tuple.Create("x-ms-blob-content-disposition", "attachment; filename=\"test.txt\""),
                Tuple.Create("x-ms-blob-type", "BlockBlob"),
                Tuple.Create("x-ms-meta-m1", "v1"),
                Tuple.Create("x-ms-meta-m2", "v2"),
            });
            Assert.IsNull(result);

            result = BlobRequest("PUT", _ctx.GetBlobUri("test.txt"), new[] {
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
