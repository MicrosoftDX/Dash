//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobControllerTests
    {
        WebApiTestRunner _runner;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>()
                {
                    { "AccountName", "dashtest" },
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestdata2;AccountKey=OOXSVWWpImRf79sbiEtpIwFsggv7VAhdjtKdt7o0gOLr2krzVXwZ+cb/gJeMqZRlXHTniRN6vnKKjs1glijihA==" },
                    { "ScaleoutNumberOfAccounts", "2"},
                });
        }

        [TestMethod]
        public void GetBlobControllerTest()
        {
            var response = _runner.ExecuteRequest("http://localhost/blob/test/fixed-test.txt",
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Content.Headers.ContentLength.Value, 423);
        }

        [TestMethod]
        public void PutExistingBlobControllerTest()
        {
            var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
            content.Headers.Add("x-ms-version", "2013-08-15");
            content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
            content.Headers.Add("x-ms-blob-content-disposition", "attachment; filename=\"test.txt\"");
            content.Headers.Add("x-ms-blob-type", "BlockBlob");
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");

            var response = _runner.ExecuteRequest("http://localhost/blob/test/test.txt",
                "PUT",
                content,
                HttpStatusCode.Created);
            // Validate that the blob is still in the expected account
            new BlobPipelineTests().GetBlobPipelineTest();
        }

        [TestMethod]
        public void PutNonExistingBlobControllerTest()
        {
            var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
            content.Headers.Add("x-ms-version", "2013-08-15");
            content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
            content.Headers.Add("x-ms-blob-content-disposition", "attachment; filename=\"fname.ext\"");
            content.Headers.Add("x-ms-blob-type", "BlockBlob");
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");

            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            var response = _runner.ExecuteRequest(blobUri,
                "PUT",
                content,
                HttpStatusCode.Created);
            // Get it back & verify via the ETag
            _runner.ExecuteRequestWithHeaders(blobUri,
                "GET",
                null,
                new[] {
                    Tuple.Create("If-Match", response.Headers.ETag.Tag),
                },
                expectedStatusCode: HttpStatusCode.OK);
            // Verify with an invalid ETag
            _runner.ExecuteRequestWithHeaders(blobUri,
                "GET",
                null,
                new[] {
                    Tuple.Create("If-Match", "FredFlinstone"),
                },
                expectedStatusCode: HttpStatusCode.PreconditionFailed);

            // Cleanup
            _runner.ExecuteRequest(blobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void BlobPropertiesAndMetadataControllerTest()
        {
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            string metadataUri = blobUri + "?comp=metadata";
            var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
            content.Headers.Add("x-ms-version", "2013-08-15");
            content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
            content.Headers.Add("x-ms-blob-content-disposition", "attachment; filename=\"fname.ext\"");
            content.Headers.Add("x-ms-blob-type", "BlockBlob");
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");
            var response = _runner.ExecuteRequest(blobUri,
                "PUT",
                content,
                HttpStatusCode.Created);
            // Set Blob Properties
            content = new StringContent(String.Empty);
            content.Headers.Add("x-ms-blob-content-encoding", "application/csv");
            response = _runner.ExecuteRequest(blobUri + "?comp=properties",
                "PUT",
                content,
                HttpStatusCode.OK);
            // Get Blob Properties            
            response = _runner.ExecuteRequest(blobUri,
                "HEAD",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Headers.GetValues("x-ms-blob-type").First(), "BlockBlob");
            Assert.AreEqual(response.Content.Headers.GetValues("Content-Encoding").First(), "application/csv");

            // Set Blob Metadata
            content = new StringContent(String.Empty);
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");
            response = _runner.ExecuteRequest(metadataUri,
                "PUT",
                content,
                HttpStatusCode.OK);
            // Get Blob Metadata
            response = _runner.ExecuteRequest(metadataUri,
                "HEAD",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Headers.GetValues("x-ms-meta-m1").First(), "v1");
            response = _runner.ExecuteRequest(metadataUri,
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Headers.GetValues("x-ms-meta-m2").First(), "v2");

            // Cleanup
            _runner.ExecuteRequest(blobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void DeleteNonExistingBlobTest()
        {
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            _runner.ExecuteRequest(blobUri,
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
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            string blockBlobUri = blobUri + "?comp=block&blockid=";
            string blockId1 = GetBlockId();
            string blockId2 = GetBlockId();
            var content = new StringContent("This is a block's worth of content", System.Text.Encoding.UTF8, "text/plain");
            var response = _runner.ExecuteRequest(blockBlobUri + HttpUtility.UrlEncode(blockId1),
                "PUT",
                content,
                HttpStatusCode.Created);
            // 2nd block - now an existing blob
            content = new StringContent("This is the next block", System.Text.Encoding.UTF8, "text/plain");
            response = _runner.ExecuteRequest(blockBlobUri + HttpUtility.UrlEncode(blockId2),
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
            response = _runner.ExecuteRequest(blobUri + "?comp=blocklist",
                "PUT",
                blockList,
                HttpStatusCode.Created);

            // Read back the complete blob
            response = _runner.ExecuteRequest(blobUri,
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Content.Headers.ContentLength.Value, 56);
            Assert.AreEqual(response.Content.ReadAsStringAsync().Result, "This is a block's worth of contentThis is the next block");
            // Cleanup
            _runner.ExecuteRequest(blobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void CopyExistingBlobControllerTest()
        {
            string destBlobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            var response = _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://localhost/test/fixed-test.txt"),
                },
                HttpStatusCode.Accepted);
            Assert.IsNotNull(response.Headers.GetValues("x-ms-copy-id"));
            Assert.AreEqual("success", response.Headers.GetValues("x-ms-copy-status").First());

            var pipelineResponse = BlobPipelineTests.BlobRequest("GET", "http://localhost/blob/test/fixed-test.txt");
            string redirectHost = new Uri(pipelineResponse.Location).Host;
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
            Assert.AreEqual(redirectHost, new Uri(pipelineResponse.Location).Host);

            string redirectUrl = new Uri(pipelineResponse.Location).GetLeftPart(UriPartial.Path);
            // Repeat the copy to verify the operation is idempotent
            response = _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://localhost/test/fixed-test.txt"),
                },
                HttpStatusCode.Accepted);
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
            Assert.AreEqual(redirectUrl, new Uri(pipelineResponse.Location).GetLeftPart(UriPartial.Path));

            // Copy a different source to the same destination & verify that destination moves to the same account as the new source
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", "http://localhost/blob/test/test_in_different_data_account.txt");
            var newSourceAccount = new Uri(pipelineResponse.Location).Host;
            response = _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://localhost/test/test_in_different_data_account.txt"),
                },
                HttpStatusCode.Accepted);
            pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
            Assert.AreEqual(newSourceAccount, new Uri(pipelineResponse.Location).Host);

            // Cleanup
            _runner.ExecuteRequest(destBlobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void CopyNonExistingBlobControllerTest()
        {
            string destBlobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://localhost/test/foobar.txt"),
                },
                HttpStatusCode.NotFound);

            _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://foobar3939039393.com/test/foobar.txt"),
                },
                HttpStatusCode.InternalServerError);

            _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "/dashtest/test/foobar.txt"),
                },
                HttpStatusCode.BadRequest);

            _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "fredflinstone"),
                },
                HttpStatusCode.BadRequest);

            // Cleanup
            _runner.ExecuteRequest(destBlobUri, "DELETE");
        }

        [TestMethod]
        public void CopyNonStorageBlobControllerTest()
        {
            string destBlobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            var response = _runner.ExecuteRequestWithHeaders(destBlobUri,
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
            _runner.ExecuteRequestWithHeaders(destBlobUri + "?comp=copy&copyid=" + response.Headers.GetValues("x-ms-copy-id").First(),
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-action", "abort"),
                },
                HttpStatusCode.NoContent);
            // Cleanup
            _runner.ExecuteRequest(destBlobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void CopyBlobToExistingDestinationControllerTest()
        {
            string destBlobUri = String.Empty;
            var pipelineResponse = BlobPipelineTests.BlobRequest("GET", "http://localhost/blob/test/test.txt");
            string sourceHost = new Uri(pipelineResponse.Location).Host;
            while (true)
            {
                // Loop until we place a blob in a different storage account than our source
                destBlobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                _runner.ExecuteRequest(destBlobUri, "PUT", content);
                // See where the blob landed
                pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
                if (!String.Equals(new Uri(pipelineResponse.Location).Host, sourceHost, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                // Cleanup
                _runner.ExecuteRequest(destBlobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
            }
            var response = _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://localhost/test/test.txt"),
                },
                HttpStatusCode.Accepted);
            Assert.IsNotNull(response.Headers.GetValues("x-ms-copy-id"));
            Assert.AreEqual("success", response.Headers.GetValues("x-ms-copy-status").First());

            pipelineResponse = BlobPipelineTests.BlobRequest("GET", destBlobUri);
            Assert.AreEqual(sourceHost, new Uri(pipelineResponse.Location).Host);

            // Cleanup
            _runner.ExecuteRequest(destBlobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void CopyBlobOldVersionControllerTest()
        {
            string destBlobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            var response = _runner.ExecuteRequestWithHeaders(destBlobUri,
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
            _runner.ExecuteRequest(destBlobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);

            destBlobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            response = _runner.ExecuteRequestWithHeaders(destBlobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2009-09-19"),
                    Tuple.Create("x-ms-copy-source", "/dashtest/test.txt"),
                },
                HttpStatusCode.NotFound);

            destBlobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            response = _runner.ExecuteRequestWithHeaders(destBlobUri,
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
