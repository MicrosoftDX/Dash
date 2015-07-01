//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using Microsoft.Dash.Common.Processors;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading;
using System.Text;
using System.Net.Http.Headers;

namespace Microsoft.Tests
{
    [TestClass]
    public class AccountManagementTests : PipelineTestBase
    {
        WebApiTestRunner _runner = new WebApiTestRunner();

        [TestInitialize]
        public void Init()
        {
            InitializeConfig(new Dictionary<string, string>()
                {
                    { "AccountName", "dashtest" },
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestimport;AccountKey=eQrjK2CLvgjrMkMqamI05DZWzQwVxVevKJThNc1pK2U7Kjs1Vcs9TAWV09OLj/FWe25gqPYHHdS1ZzGLFZw9rw==" },
                });
        }

        [TestMethod]
        public void ImportAccountContainersTest()
        {
            var importAccount = DashConfiguration.DataAccounts[1];
            var importClient = importAccount.CreateCloudBlobClient();
            InitializeImportClient(importClient);
            string containerName = "import-container-" + Guid.NewGuid().ToString("N");
            var newContainer = importClient.GetContainerReference(containerName);
            newContainer.CreateIfNotExists();

            AccountManager.ImportAccountAsync(importAccount.Credentials.AccountName).Wait();

            // Verify that our container was imported
            string baseUri = "http://mydashserver/container/" + containerName + "?restype=container";
            string appVersion = "2014-02-14";
            var requestHeaders = new[] {
                Tuple.Create("x-ms-version", appVersion),
            };
            var results = _runner.ExecuteRequestWithHeaders(baseUri, "GET", null, requestHeaders, HttpStatusCode.OK);
            // Verify that all the existing containers were created in the import account
            Assert.IsTrue(importClient.ListContainers().Count() > 1);

            // Cleanup
            _runner.ExecuteRequest(baseUri, "DELETE", (HttpContent)null, HttpStatusCode.Accepted);

            // Test container permissions & metadata on import
            containerName = "import-container-" + Guid.NewGuid().ToString("N");
            newContainer = importClient.GetContainerReference(containerName);
            newContainer.CreateIfNotExists(BlobContainerPublicAccessType.Blob);
            var permissions = newContainer.GetPermissions();
            var policy = new SharedAccessBlobPolicy { Permissions = SharedAccessBlobPermissions.Read, SharedAccessStartTime = DateTimeOffset.UtcNow, SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddDays(5) };
            permissions.SharedAccessPolicies.Add("TestPolicy", policy);
            newContainer.SetPermissions(permissions);
            var metadata = new[] {
                Tuple.Create("Metadata1", "Value1"),
                Tuple.Create("Metadata2", "Value2"),
            };
            CopyMetadata(newContainer.Metadata, metadata);
            newContainer.SetMetadata();

            AccountManager.ImportAccountAsync(importAccount.Credentials.AccountName).Wait();

            // Verify that our container was imported
            baseUri = "http://mydashserver/container/" + containerName + "?restype=container";
            results = _runner.ExecuteRequestWithHeaders(baseUri, "GET", null, requestHeaders, HttpStatusCode.OK);
            results = _runner.ExecuteRequestWithHeaders(baseUri + "&comp=acl", "GET", null, requestHeaders, HttpStatusCode.OK);
            Assert.AreEqual(results.Headers.GetValues("x-ms-blob-public-access").First(), "blob");
            var storedPolicyResponse = XDocument.Load(results.Content.ReadAsStreamAsync().Result);
            var storedPolicy = (XElement)storedPolicyResponse.Root.FirstNode;
            Assert.IsNotNull(storedPolicy);
            Assert.AreEqual(storedPolicy.Element("Id").Value, "TestPolicy");
            var accessPolicy = (XElement)storedPolicy.Element("AccessPolicy");
            Assert.AreEqual(accessPolicy.Element("Permission").Value, "r");
            Assert.AreEqual(DateTimeOffset.Parse(accessPolicy.Element("Start").Value).ToString(), policy.SharedAccessStartTime.Value.ToString());
            Assert.AreEqual(DateTimeOffset.Parse(accessPolicy.Element("Expiry").Value).ToString(), policy.SharedAccessExpiryTime.Value.ToString());

            results = _runner.ExecuteRequestWithHeaders(baseUri + "&comp=metadata", "GET", null, requestHeaders, HttpStatusCode.OK);
            ValidateMetadata(results.Headers, metadata);

            // Cleanup
            CleanupImportClient(importClient, containerName);
        }

        [TestMethod]
        public void ImportAccountBlobsTest()
        {
            var importAccount = DashConfiguration.DataAccounts[1];
            var importClient = importAccount.CreateCloudBlobClient();
            InitializeImportClient(importClient);

            string containerName = "import-container-" + Guid.NewGuid().ToString("N");
            var newContainer = importClient.GetContainerReference(containerName);
            newContainer.CreateIfNotExists();

            string blob1Name = "block-blob-" + Guid.NewGuid().ToString("N");
            string blob2Name = "page-blob-" + Guid.NewGuid().ToString("N");
            string blob3Name = "blob-metadata-" + Guid.NewGuid().ToString("N");
            var metadata = new[] {
                Tuple.Create("Metadata1", "Value1"),
                Tuple.Create("Metadata2", "Value2"),
            };

            newContainer.GetBlockBlobReference(blob1Name).UploadText("Block blob content");
            var pageContent = new byte[512];
            newContainer.GetPageBlobReference(blob2Name).UploadFromByteArray(pageContent, 0, pageContent.Length);
            var blob3 = newContainer.GetBlockBlobReference(blob3Name);
            CopyMetadata(blob3.Metadata, metadata);
            blob3.UploadText("Metadata block blob content");

            AccountManager.ImportAccountAsync(importAccount.Credentials.AccountName).Wait();

            // Verify that our blobs were imported
            string baseUri = "http://mydashserver/blob/" + containerName + "/";

            var response = _runner.ExecuteRequest(baseUri + blob1Name,
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Content.Headers.ContentLength.Value, 18);

            response = _runner.ExecuteRequest(baseUri + blob2Name,
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            Assert.AreEqual(response.Content.Headers.ContentLength.Value, 512);

            response = _runner.ExecuteRequest(baseUri + blob3Name + "?comp=metadata",
                "HEAD",
                expectedStatusCode: HttpStatusCode.OK);
            ValidateMetadata(response.Headers, metadata);

            // Cleanup
            CleanupImportClient(importClient, containerName);
        }

        static void InitializeImportClient(CloudBlobClient importClient)
        {
            // Remove all existing containers - note that this is a race condition with others executing the tests concurrently,
            // but this is unlikely enough to avoid contention
            // Use a wait to try to avoid contention
            if (importClient.ListContainers().Any())
            {
                Thread.Sleep(30000);
            }
            bool deletedContainers = false;
            foreach (var container in importClient.ListContainers())
            {
                container.Delete();
                deletedContainers = true;
            }
            // If we deleted any containers that will be recreated when the account is imported, we should
            // wait a while to allow XStore to get consistent around the deleted container
            if (deletedContainers)
            {
                Thread.Sleep(60000);
            }
        }

        void CleanupImportClient(CloudBlobClient importClient, string containerName)
        {
            _runner.ExecuteRequest("http://mydashserver/container/" + containerName + "?restype=container",
                "DELETE",
                (HttpContent)null,
                HttpStatusCode.Accepted);
            foreach (var container in importClient.ListContainers())
            {
                container.Delete();
            }
        }

        static void CopyMetadata(IDictionary<string, string> dest, IEnumerable<Tuple<string, string>> source)
        {
            foreach (var metadatum in source)
            {
                dest.Add(metadatum.Item1, metadatum.Item2);
            }
        }

        void ValidateMetadata(HttpResponseHeaders headers, IEnumerable<Tuple<string, string>> expected)
        {
            foreach (var metadatum in expected)
            {
                Assert.AreEqual(headers.GetValues("x-ms-meta-" + metadatum.Item1).First(), metadatum.Item2);
            }
        }
    }
}
