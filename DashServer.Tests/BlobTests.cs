//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Dash.Server.Controllers;
using Microsoft.Dash.Server.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using System.Threading;
using Microsoft.Dash.Server;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobTests
    {
        WebApiTestRunner _runner;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>()
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=http;AccountName=dashstorage0;AccountKey=uCNvIdXcltACBiDUMyO0BflZpKmjseplqOlzE62tx87qnkwpUMBV/GQhrscW9lmdZVT0x8DilYqUoHMNBlVIGg==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=http;AccountName=dashstorage1;AccountKey=8jqRVtXUWiEthgIhR+dFwrB8gh3lFuquvJQ1v4eabObIj7okI1cZIuzY8zZHmEdpcC0f+XlUkbFwAhjTfyrLIg==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=http;AccountName=dashstorage2;AccountKey=YI0BDhckKp+6uBsu4OAAeVvUyOuMvimqo9BSz197lR14x9vWE+tuwqOr0U1asNWpkdZs4z8wcnu9pZNYDqdRPA==" },
                    { "ScaleoutNumberOfAccounts", "2"},
                });
        }

        [TestMethod]
        public void GetBlobTest()
        {
            var response = _runner.ExecuteRequest("http://localhost/blob/test/test.txt", 
                "GET",
                expectedStatusCode: HttpStatusCode.Redirect);
            Assert.IsNotNull(response.Headers.Location);
            Assert.AreEqual("http://dashstorage1.blob.core.windows.net/test/test.txt", response.Headers.Location.GetLeftPart(UriPartial.Path));
            var redirectQueryParams = HttpUtility.ParseQueryString(response.Headers.Location.Query);
            Assert.AreEqual("2014-02-14", redirectQueryParams["sv"]);
            Assert.IsNotNull(redirectQueryParams["sig"]);
            Assert.IsNotNull(redirectQueryParams["se"]);
        }

        [TestMethod]
        public void PutExistingBlobTest()
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
                HttpStatusCode.Redirect);
            Assert.IsNotNull(response.Headers.Location);
            Assert.AreEqual("http://dashstorage1.blob.core.windows.net/test/test.txt", response.Headers.Location.GetLeftPart(UriPartial.Path));
        }

        [TestMethod]
        public void PutNonExistingBlobTest()
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
                HttpStatusCode.Redirect);
            Assert.IsNotNull(response.Headers.Location);
            string redirectLocation = response.Headers.Location.GetLeftPart(UriPartial.Path);
            // Get it back & verify we get redirected to the same location
            response = _runner.ExecuteRequest(blobUri,
                "GET",
                expectedStatusCode: HttpStatusCode.Redirect);
            Assert.AreEqual(redirectLocation, response.Headers.Location.GetLeftPart(UriPartial.Path));

            // Cleanup
            _runner.ExecuteRequest(blobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void BlobPropertiesAndMetadataTest()
        {
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            string metadataUri = blobUri + "?comp=metadata";
            var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
            var response = _runner.ExecuteRequest(blobUri,
                "PUT",
                content,
                HttpStatusCode.Redirect);
            Assert.IsNotNull(response.Headers.Location);
            string redirectLocation = response.Headers.Location.GetLeftPart(UriPartial.Path);
            // Get Blob Properties            
            response = _runner.ExecuteRequest(blobUri,
                "HEAD",
                expectedStatusCode: HttpStatusCode.Redirect);
            Assert.AreEqual(redirectLocation, response.Headers.Location.GetLeftPart(UriPartial.Path));
            // Set Blob Properties
            content = new StringContent(String.Empty);
            content.Headers.Add("x-ms-blob-content-encoding", "application/csv");
            response = _runner.ExecuteRequest(blobUri + "?comp=properties",
                "PUT",
                content,
                HttpStatusCode.Redirect);
            Assert.AreEqual(redirectLocation, response.Headers.Location.GetLeftPart(UriPartial.Path));

            // Get Blob Metadata
            response = _runner.ExecuteRequest(metadataUri,
                "HEAD",
                expectedStatusCode: HttpStatusCode.Redirect);
            Assert.AreEqual(redirectLocation, response.Headers.Location.GetLeftPart(UriPartial.Path));
            response = _runner.ExecuteRequest(metadataUri,
                "GET",
                expectedStatusCode: HttpStatusCode.Redirect);
            Assert.AreEqual(redirectLocation, response.Headers.Location.GetLeftPart(UriPartial.Path));
            // Set Blob Metadata
            content = new StringContent(String.Empty);
            content.Headers.Add("x-ms-meta-m1", "v1");
            content.Headers.Add("x-ms-meta-m2", "v2");
            response = _runner.ExecuteRequest(metadataUri,
                "PUT",
                content,
                HttpStatusCode.Redirect);
            Assert.AreEqual(redirectLocation, response.Headers.Location.GetLeftPart(UriPartial.Path));

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
            return HttpUtility.UrlEncode(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        }

        [TestMethod]
        public void PutBlobBlockTest()
        {
            string blobUri = "http://localhost/blob/test/" + Guid.NewGuid().ToString();
            string blockBlobUri = blobUri + "?comp=block&blockid=";
            string blockId1 = GetBlockId();
            string blockId2 = GetBlockId();
            var content = new StringContent("This is a block's worth of content", System.Text.Encoding.UTF8, "text/plain");
            var response = _runner.ExecuteRequest(blockBlobUri + blockId1,
                "PUT",
                content,
                HttpStatusCode.Redirect);
            Assert.IsNotNull(response.Headers.Location);
            string redirectLocation = response.Headers.Location.GetLeftPart(UriPartial.Path);
            // 2nd block - now an existing blob
            content = new StringContent("This is the next block", System.Text.Encoding.UTF8, "text/plain");
            response = _runner.ExecuteRequest(blockBlobUri + blockId2,
                "PUT",
                content,
                HttpStatusCode.Redirect);
            Assert.AreEqual(redirectLocation, response.Headers.Location.GetLeftPart(UriPartial.Path));
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
                HttpStatusCode.Redirect);
            Assert.AreEqual(redirectLocation, response.Headers.Location.GetLeftPart(UriPartial.Path));

            // Cleanup
            _runner.ExecuteRequest(blobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
        }
    }
}
