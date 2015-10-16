//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobControllerTests : DashTestBase
    {
        static DashTestContext _ctx;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ctx = InitializeConfigAndCreateTestBlobs(ctx, "datax2", new Dictionary<string, string>
                {
                    { "AccountName", "dashtest" },
                },
                new[] {
                    TestBlob.DefineBlob("fixed-test.txt"),
                    TestBlob.DefineBlob("test.txt"),
                    TestBlob.DefineBlob("test_in_different_data_account.txt"),
                });
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupTestBlobs(_ctx);
        }

        [TestMethod]
        public void GetBlobControllerTest()
        {
            var response = _ctx.Runner.ExecuteRequest(_ctx.GetBlobUri("fixed-test.txt"),
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(11, response.Content.Headers.ContentLength.Value);
        }

        [TestMethod]
        public void PutExistingBlobControllerTest()
        {
            string blobUri = _ctx.GetBlobUri("test.txt");

            var pipelineResponse = BlobPipelineTests.BlobRequest("GET", blobUri);
            string redirectHost = new Uri(pipelineResponse.Location).Host;

            var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
            content.Headers.Add("x-ms-version", "2013-08-15");
            content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
            content.Headers.Add("x-ms-blob-content-disposition", "attachment; filename=\"test.txt\"");
            content.Headers.Add("x-ms-blob-type", "BlockBlob");
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");

            var response = _ctx.Runner.ExecuteRequest(blobUri,
                "PUT",
                content,
                HttpStatusCode.Created);
            // Validate that the blob is still in the same account
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", blobUri);
            Assert.AreEqual(redirectHost.ToLower(), new Uri(pipelineResponse.Location).Host.ToLower());
        }

        [TestMethod]
        public void PutNonExistingBlobControllerTest()
        {
            PutAndValidateBlob(_ctx.GetUniqueBlobUri());
        }

        [TestMethod]
        public void EncodedBlobNameControllerTest()
        {
            PutAndValidateBlob(_ctx.GetBlobUri("workernode2.jokleinhbase.d6.internal.cloudapp.net,60020,1436223739284/workernode2.jokleinhbase.d6.internal.cloudapp.net%2C60020%2C1436223739284.1436223741878"));
            PutAndValidateBlob(_ctx.GetBlobUri("reserved-characters-blob-[]@!$&()*+,;='"));
        }

        void PutAndValidateBlob(string blobUri)
        {
            var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
            content.Headers.Add("x-ms-version", "2013-08-15");
            content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
            content.Headers.Add("x-ms-blob-content-disposition", "attachment; filename=\"fname.ext\"");
            content.Headers.Add("x-ms-blob-type", "BlockBlob");
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");

            var response = _ctx.Runner.ExecuteRequest(blobUri,
                "PUT",
                content,
                HttpStatusCode.Created);
            // Get it back & verify via the ETag
            _ctx.Runner.ExecuteRequestWithHeaders(blobUri,
                "GET",
                null,
                new[] {
                    Tuple.Create("If-Match", response.Headers.ETag.Tag),
                },
                expectedStatusCode: HttpStatusCode.OK);
            // Verify with an invalid ETag
            _ctx.Runner.ExecuteRequestWithHeaders(blobUri,
                "GET",
                null,
                new[] {
                    Tuple.Create("If-Match", "FredFlinstone"),
                },
                expectedStatusCode: HttpStatusCode.PreconditionFailed);
        }

        [TestMethod]
        public void BlobPropertiesAndMetadataControllerTest()
        {
            string blobUri = _ctx.GetUniqueBlobUri();
            string metadataUri = blobUri + "?comp=metadata";
            var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
            content.Headers.Add("x-ms-version", "2013-08-15");
            content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
            content.Headers.Add("x-ms-blob-content-disposition", "attachment; filename=\"fname.ext\"");
            content.Headers.Add("x-ms-blob-type", "BlockBlob");
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");
            var response = _ctx.Runner.ExecuteRequest(blobUri,
                "PUT",
                content,
                HttpStatusCode.Created);
            // Set Blob Properties
            content = new StringContent(String.Empty);
            content.Headers.Add("x-ms-blob-content-encoding", "application/csv");
            response = _ctx.Runner.ExecuteRequest(blobUri + "?comp=properties",
                "PUT",
                content,
                HttpStatusCode.OK);
            // Get Blob Properties            
            response = _ctx.Runner.ExecuteRequest(blobUri,
                "HEAD",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Headers.GetValues("x-ms-blob-type").First(), "BlockBlob");
            Assert.AreEqual(response.Content.Headers.GetValues("Content-Encoding").First(), "application/csv");

            // Set Blob Metadata
            content = new StringContent(String.Empty);
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");
            response = _ctx.Runner.ExecuteRequest(metadataUri,
                "PUT",
                content,
                HttpStatusCode.OK);
            // Get Blob Metadata
            response = _ctx.Runner.ExecuteRequest(metadataUri,
                "HEAD",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Headers.GetValues("x-ms-meta-m1").First(), "v1");
            response = _ctx.Runner.ExecuteRequest(metadataUri,
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Headers.GetValues("x-ms-meta-m2").First(), "v2");
        }

        [TestMethod]
        public void DeleteNonExistingBlobTest()
        {
            string blobUri = _ctx.GetUniqueBlobUri();
            _ctx.Runner.ExecuteRequest(blobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.NotFound);
        }

        static string GetBlockId()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }

        [TestMethod]
        public void PutBlobBlockControllerTest()
        {
            string blobUri = _ctx.GetUniqueBlobUri();
            string blockBlobUri = blobUri + "?comp=block&blockid=";
            string blockId1 = GetBlockId();
            string blockId2 = GetBlockId();
            var content = new StringContent("This is a block's worth of content", System.Text.Encoding.UTF8, "text/plain");
            var response = _ctx.Runner.ExecuteRequest(blockBlobUri + HttpUtility.UrlEncode(blockId1),
                "PUT",
                content,
                HttpStatusCode.Created);
            // 2nd block - now an existing blob
            content = new StringContent("This is the next block", System.Text.Encoding.UTF8, "text/plain");
            response = _ctx.Runner.ExecuteRequest(blockBlobUri + HttpUtility.UrlEncode(blockId2),
                "PUT",
                content,
                HttpStatusCode.Created);
            // Commit the blocks
            var blockList = new XDocument(
                new XElement("BlockList",
                    new XElement("Latest", blockId1),
                    new XElement("Latest", blockId2)
                )
            );
            response = _ctx.Runner.ExecuteRequest(blobUri + "?comp=blocklist",
                "PUT",
                blockList,
                HttpStatusCode.Created);

            // Read back the complete blob
            response = _ctx.Runner.ExecuteRequest(blobUri,
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Content.Headers.ContentLength.Value, 56);
            Assert.AreEqual(response.Content.ReadAsStringAsync().Result, "This is a block's worth of contentThis is the next block");
        }

        [TestMethod]
        public void CopyExistingBlobControllerTest()
        {
            string destBlobUri = _ctx.GetUniqueBlobUri();
            var response = _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", _ctx.GetBlobUri("fixed-test.txt", false)),
                },
                HttpStatusCode.Accepted);
            Assert.IsNotNull(response.Headers.GetValues("x-ms-copy-id"));
            Assert.AreEqual("success", response.Headers.GetValues("x-ms-copy-status").First());

            var pipelineResponse = BlobPipelineTests.BlobRequest("GET", _ctx.GetBlobUri("fixed-test.txt"));
            string redirectHost = new Uri(pipelineResponse.Location).Host;
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
            Assert.AreEqual(redirectHost, new Uri(pipelineResponse.Location).Host);

            string redirectUrl = new Uri(pipelineResponse.Location).GetLeftPart(UriPartial.Path);
            // Repeat the copy to verify the operation is idempotent
            response = _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", _ctx.GetBlobUri("fixed-test.txt", false)),
                },
                HttpStatusCode.Accepted);
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
            Assert.AreEqual(redirectUrl, new Uri(pipelineResponse.Location).GetLeftPart(UriPartial.Path));

            // Copy a different source to the same destination & verify that destination moves to the same account as the new source
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", _ctx.GetBlobUri("test_in_different_data_account.txt"));
            var newSourceAccount = new Uri(pipelineResponse.Location).Host;
            response = _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", _ctx.GetBlobUri("test_in_different_data_account.txt", false)),
                },
                HttpStatusCode.Accepted);
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
            Assert.AreEqual(newSourceAccount, new Uri(pipelineResponse.Location).Host);
        }

        [TestMethod]
        public void CopyNonExistingBlobControllerTest()
        {
            string destBlobUri = _ctx.GetUniqueBlobUri();
            _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", _ctx.GetBlobUri("foobar.txt", false)),
                },
                HttpStatusCode.NotFound);

            _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://foobar3939039393.com/test/foobar.txt"),
                },
                HttpStatusCode.InternalServerError);

            _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "/dashtest/test/foobar.txt"),
                },
                HttpStatusCode.BadRequest);

            _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "fredflinstone"),
                },
                HttpStatusCode.BadRequest);
        }

        [TestMethod]
        public void CopyNonStorageBlobControllerTest()
        {
            string destBlobUri = _ctx.GetUniqueBlobUri();
            var response = _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://www.service-gateway.net/gateway/v0.6/https/CloudProjectHttps.cspkg"),
                },
                HttpStatusCode.Accepted);
            Assert.IsNotNull(response.Headers.GetValues("x-ms-copy-id"));
            Assert.AreEqual("pending", response.Headers.GetValues("x-ms-copy-status").First());
            // Abort the copy
            _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri + "?comp=copy&copyid=" + response.Headers.GetValues("x-ms-copy-id").First(),
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-action", "abort"),
                },
                HttpStatusCode.NoContent);
        }

        [TestMethod]
        public void CopyBlobToExistingDestinationControllerTest()
        {
            string destBlobUri = String.Empty;
            var pipelineResponse = BlobPipelineTests.BlobRequest("GET", _ctx.GetBlobUri("test.txt"));
            string sourceHost = new Uri(pipelineResponse.Location).Host;
            while (true)
            {
                // Loop until we place a blob in a different storage account than our source
                destBlobUri = _ctx.GetUniqueBlobUri();
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                _ctx.Runner.ExecuteRequest(destBlobUri, "PUT", content);
                // See where the blob landed
                pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
                if (!String.Equals(new Uri(pipelineResponse.Location).Host, sourceHost, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            var response = _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", _ctx.GetBlobUri("test.txt", false)),
                },
                HttpStatusCode.Accepted);
            Assert.IsNotNull(response.Headers.GetValues("x-ms-copy-id"));
            Assert.AreEqual("success", response.Headers.GetValues("x-ms-copy-status").First());

            pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
            Assert.AreEqual(sourceHost, new Uri(pipelineResponse.Location).Host);
        }

        [TestMethod]
        public void CopyBlobOldVersionControllerTest()
        {
            string destBlobUri = _ctx.GetUniqueBlobUri();
            var response = _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2009-09-19"),
                    Tuple.Create("x-ms-copy-source", "/dashtest/test/test.txt"),
                },
                HttpStatusCode.Created);
            Assert.IsNotNull(response.Headers.GetValues("x-ms-copy-id"));
            Assert.AreEqual("success", response.Headers.GetValues("x-ms-copy-status").First());
            // Cleanup
            _ctx.Runner.ExecuteRequest(destBlobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);

            destBlobUri = _ctx.GetUniqueBlobUri();
            response = _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2009-09-19"),
                    Tuple.Create("x-ms-copy-source", "/dashtest/test.txt"),
                },
                HttpStatusCode.NotFound);

            destBlobUri = _ctx.GetUniqueBlobUri();
            response = _ctx.Runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2009-09-19"),
                    Tuple.Create("x-ms-copy-source", "/dashtest1/test/test.txt"),
                },
                HttpStatusCode.BadRequest);
        }
    }
}
