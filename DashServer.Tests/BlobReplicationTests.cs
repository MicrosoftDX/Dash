//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Microsoft.Dash.Async;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Platform.Payloads;
using Microsoft.Dash.Common.Processors;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobReplicationTests : PipelineTestBase
    {
        static DashTestContext _ctx;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ctx = InitializeConfigAndCreateTestBlobs(ctx, "datax3", new Dictionary<string, string>
                {
                    { DashConfiguration.KeyWorkerQueueName, Guid.NewGuid().ToString() },
                    { "ReplicationMetadataName", ReplicateMetadataName },
                    { "ReplicationPathPattern", "^.*/test22(/.*|$)" },
                    { "LogNormalOperations", "true" }
                },
                new[] {
                    TestBlob.DefineBlob("fixed-test.txt"),
                });
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupTestBlobs(_ctx);
        }

        [TestMethod]
        public void ReplicateWithMetadataPutBlobPipelineTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = Guid.NewGuid().ToString();
                var result = BlobRequest("PUT", _ctx.GetBlobUri(blobName), new[] {
                    Tuple.Create("x-ms-blob-type", "BlockBlob"),
                    Tuple.Create("x-ms-meta-" + ReplicateMetadataName, "true"),
                    Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                    Tuple.Create("Expect", "100-Continue")
                });
                Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
            }
        }

        [TestMethod]
        public void ReplicateWithMetadataPutBlobControllerTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = Guid.NewGuid().ToString();
                string blobUri = _ctx.GetBlobUri(blobName); ;
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                content.Headers.Add("x-ms-meta-" + ReplicateMetadataName, "true");
                var response = _ctx.Runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
                // Because the blob is not replicated yet, the delete should not enque any delete replica messages
                AssertQueueIsDrained();
            }
        }

        [TestMethod]
        public void ReplicateWithPathSpecPutBlobPipelineTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = "test22/" + Guid.NewGuid().ToString();
                var result = BlobRequest("PUT", _ctx.GetBlobUri(blobName), new[] {
                    Tuple.Create("x-ms-blob-type", "BlockBlob"),
                    Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                    Tuple.Create("Expect", "100-Continue")
                });
                Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
            }
        }

        [TestMethod]
        public void ReplicateWithPathSpecPutBlobControllerTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = "test22/" + Guid.NewGuid().ToString();
                string blobUri = _ctx.GetBlobUri(blobName);
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _ctx.Runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
                AssertQueueIsDrained();
            }
        }

        [TestMethod]
        public void ReplicateWithAllOperationsControllerTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = "test22/" + Guid.NewGuid().ToString();
                string blobUri = _ctx.GetBlobUri(blobName);
                string blockBlobUri = blobUri + "?comp=block&blockid=";
                string blockId = GetBlockId();
                
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _ctx.Runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
                // Put a block - shouldn't trigger a replication
                content = new StringContent("This is the next block", System.Text.Encoding.UTF8, "text/plain");
                response = _ctx.Runner.ExecuteRequest(blockBlobUri + HttpUtility.UrlEncode(blockId),
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                AssertQueueIsDrained();
                // Commit the blocks - should trigger replication
                response = _ctx.Runner.ExecuteRequest(blobUri + "?comp=blocklist",
                    "PUT",
                    new XDocument(
                        new XElement("BlockList",
                            new XElement("Latest", blockId)
                        )
                    ),
                    null,
                    HttpStatusCode.Created);
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
                // Set metadata - should trigger replication
                content = new StringContent(String.Empty);
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-meta-m1", "v1");
                content.Headers.Add("x-ms-meta-m2", "v2");
                _ctx.Runner.ExecuteRequest(blobUri + "?comp=metadata",
                    "PUT",
                    content,
                    HttpStatusCode.OK);
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
                // Set Blob Properties - should trigger replication
                content = new StringContent(String.Empty);
                content.Headers.Add("x-ms-blob-content-encoding", "application/csv");
                response = _ctx.Runner.ExecuteRequest(blobUri + "?comp=properties",
                    "PUT",
                    content,
                    HttpStatusCode.OK);
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
                AssertQueueIsDrained();
            }
        }

        [TestMethod]
        public void ReplicateDeleteBlobControllerTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = Guid.NewGuid().ToString();
                string blobUri = _ctx.GetBlobUri(blobName);
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _ctx.Runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                AssertQueueIsDrained();
                // Directly manipulate the namespace blob so it appears that the blob is replicated
                var namespaceClient = DashConfiguration.NamespaceAccount.CreateCloudBlobClient();
                var containerReference = namespaceClient.GetContainerReference(_ctx.ContainerName);
                var nsBlob = NamespaceBlob.FetchForBlobAsync(containerReference.GetBlockBlobReference(blobName)).Result;
                foreach (var dataAccount in DashConfiguration.DataAccounts
                                                                .Where(account => !String.Equals(account.Credentials.AccountName, nsBlob.PrimaryAccountName, StringComparison.OrdinalIgnoreCase)))
                {
                    nsBlob.DataAccounts.Add(dataAccount.Credentials.AccountName);
                }
                nsBlob.SaveAsync().Wait();
                // Now we delete - we should get a delete replica message for every data account except the primary
                _ctx.Runner.ExecuteRequest(blobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
                AssertReplicationMessageIsEnqueued(MessageTypes.DeleteReplica, _ctx.ContainerName, blobName, nsBlob.PrimaryAccountName);
            }
        }

        [TestMethod]
        public void ReplicateCopyBlobControllerTest()
        {
            string blobName = "test22/" + Guid.NewGuid().ToString();
            string blobUri = _ctx.GetBlobUri(blobName);
            var response = _ctx.Runner.ExecuteRequestWithHeaders(blobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", _ctx.GetBlobUri("fixed-test.txt", false)),
                },
                HttpStatusCode.Accepted);
            // Fetch the blob so that we can determine it's primary data account
            var result = BlobRequest("HEAD", blobUri);
            string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
            AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, _ctx.ContainerName, blobName, dataAccountName);
            AssertQueueIsDrained();
        }

        [TestMethod]
        public void ReplicateWithAsyncWorkerTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                // Throw in the encodable name challenge here as well - unfortunately, the steps we've taken to ensure double-encoding works with IIS
                // doesn't apply when we're using HttpClient in direct mode - we have to have 2 encoded forms of the same name
                string blobSegment = "workernode2.jokleinhbase.d6.internal.cloudapp.net%2C60020%2C1436223739284.1436223741878";
                string encodedBlobSegment = WebUtility.UrlEncode(blobSegment);
                string uniqueFolderName = "test22/" + Guid.NewGuid().ToString();
                string baseBlobName = uniqueFolderName + "/workernode2.jokleinhbase.d6.internal.cloudapp.net,60020,1436223739284/";
                string blobName = baseBlobName + blobSegment;
                string encodedBlobName = baseBlobName + encodedBlobSegment;
                string baseBlobUri = _ctx.GetBlobUri(baseBlobName);
                string blobUri = baseBlobUri + blobSegment;
                string encodedBlobUri = baseBlobUri + encodedBlobSegment;
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _ctx.Runner.ExecuteRequest(encodedBlobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                // Verify that the blob is replicated by the async worker
                AssertReplicationWorker(_ctx.ContainerName, blobName, dataAccountName, false);

                // Verify that concurrent operations send us to the primary - read the ETag & modified dates first
                var listdoc = _ctx.Runner.ExecuteRequestResponse(
                    _ctx.GetContainerUri() + "&comp=list&prefix=" + encodedBlobName + "&include=metadata",
                    "GET");
                var enumerationResults = listdoc.Root;
                var blobs = enumerationResults.Element("Blobs");
                var eTag = blobs.Element("Blob").Element("Properties").Element("Etag").Value;
                var lastModified = blobs.Element("Blob").Element("Properties").Element("Last-Modified").Value;
                AssertRedirectDataAccount("HEAD", blobUri, dataAccountName, null);
                AssertConcurrentReads(blobUri, dataAccountName, new[] {
                    Tuple.Create("If-Match", eTag),
                    Tuple.Create("If-None-Match", eTag),
                    Tuple.Create("If-Modified-Since", lastModified),
                    Tuple.Create("If-Unmodified-Since", lastModified),
                    Tuple.Create("x-ms-if-sequence-number-le", "100"),
                    Tuple.Create("x-ms-if-sequence-number-lt", "100"),
                    Tuple.Create("x-ms-if-sequence-number-eq", "100"),
                });
                // Cleanup - do an orphaned replica test on the way out
                _ctx.Runner.ExecuteRequest(encodedBlobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
                // The namespace has been marked for deletion & the primary blob has been deleted, but the replicas are now
                // orphaned waiting to be async deleted - verify that a blob listing at this time doesn't return the blob
                listdoc = _ctx.Runner.ExecuteRequestResponse(
                    _ctx.GetContainerUri() + "&comp=list&prefix=" + encodedBlobName + "&include=metadata",
                    "GET");
                enumerationResults = listdoc.Root;
                Assert.AreEqual(0, enumerationResults.Element("Blobs").Elements().Count());
                // A special corner case for this same scenario is that we do a hierarchical listing a folder down (the container) & we 
                // shouldn't see the containing folder as it doesn't contain any non-deleted primary blobs
                listdoc = _ctx.Runner.ExecuteRequestResponse(
                    _ctx.GetContainerUri() + "&comp=list&delimiter=/&prefix=" + uniqueFolderName + "&include=metadata",
                    "GET");
                enumerationResults = listdoc.Root;
                Assert.AreEqual(0, enumerationResults.Element("Blobs").Elements().Count());

                // Verify the delete replica behavior
                AssertReplicationWorker(_ctx.ContainerName, blobName, dataAccountName, true);
            }
        }

        [TestMethod]
        public void ReplicateWithAsyncWorkerProgressTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                // The initial write doesn't replicate - we trigger that manually & then
                // directly invoke the replication progress methods to verify the progress behavior
                string blobName = "test/" + Guid.NewGuid().ToString();
                string blobUri = _ctx.GetBlobUri(blobName);
                var content = new StringContent(new String('b', 1024 * 1024 * 20), System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _ctx.Runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                foreach (var dataAccount in DashConfiguration.DataAccounts
                                                .Where(account => !String.Equals(account.Credentials.AccountName, dataAccountName, StringComparison.OrdinalIgnoreCase)))
                {
                    // Specify 0 wait time will cause a progress message to be enqueued
                    Assert.IsTrue(BlobReplicator.BeginBlobReplication(dataAccountName, dataAccount.Credentials.AccountName, _ctx.ContainerName, blobName, 0));
                }
                AssertReplicationWorker(_ctx.ContainerName, blobName, dataAccountName, false);
            }
        }

        void AssertReplicationMessageIsEnqueued(MessageTypes messageType, string container, string blobName, string primaryAccount)
        {
            // Wait for the messages to be fully enqueued
            Task.Delay(1000).Wait();
            var replicaAccounts = DashConfiguration.DataAccounts
                .Select(account => account.Credentials.AccountName)
                .Where(accountName => !String.Equals(accountName, primaryAccount, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(accountName => accountName, accountName => false, StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                var replicateMessage = _ctx.WorkerQueue.Dequeue();
                if (replicateMessage == null)
                {
                    break;
                }
                Assert.IsNotNull(replicateMessage);
                Assert.AreEqual(replicateMessage.MessageType, messageType);
                if (messageType == MessageTypes.BeginReplicate)
                {
                    Assert.AreEqual(replicateMessage.Payload[ReplicatePayload.Source], primaryAccount);
                }
                Assert.AreEqual(replicateMessage.Payload[ReplicatePayload.Container], container);
                Assert.AreEqual(replicateMessage.Payload[ReplicatePayload.BlobName], blobName);
                replicaAccounts[replicateMessage.Payload[messageType == MessageTypes.BeginReplicate ? ReplicatePayload.Destination : ReplicatePayload.Source]] = true;
                replicateMessage.Delete();
            }
            Assert.IsFalse(replicaAccounts.Any(account => !account.Value), 
                "Data accounts detected with no replication enqueued: {0}", 
                String.Join(", ", replicaAccounts
                    .Where(account => !account.Value)
                    .Select(account => account.Key)));
        }

        void AssertQueueIsDrained()
        {
            // Wait for the messages to be fully enqueued
            Task.Delay(1000).Wait();
            bool messageSeen = false;
            while (true)
            {
                var message = _ctx.WorkerQueue.Dequeue();
                if (message == null)
                {
                    break;
                }
                message.Delete();
                messageSeen = true;
            }
            if (messageSeen)
            {
                Assert.Fail("Expected queue to be empty");
            }
        }

        static string GetBlockId()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }

        void AssertReplicationWorker(string container, string blobName, string primaryAccount, bool isDeleteReplica)
        {
            // Wait for the messages to be fully enqueued
            Task.Delay(1000).Wait();
            int processed = 0, errors = 0;
            MessageProcessor.ProcessMessageLoop(ref processed, ref errors, 0);
            Assert.AreEqual(0, errors);
            Assert.IsTrue(DashConfiguration.DataAccounts.Count - 1 <= processed);
            foreach (var account in DashConfiguration.DataAccounts
                                        .Where(dataAccount => !String.Equals(dataAccount.Credentials.AccountName, primaryAccount, StringComparison.OrdinalIgnoreCase)))
            {
                var dataBlob = NamespaceHandler.GetBlobByName(account, container, blobName);
                Assert.AreEqual(!isDeleteReplica, dataBlob.Exists());
            }
        }

        static void AssertConcurrentReads(string blobUri, string expectedAccountName, IEnumerable<Tuple<string, string>> concurrentHeaders)
        {
            foreach (var concurrentHeader in concurrentHeaders)
            {
                AssertRedirectDataAccount("GET", blobUri, expectedAccountName, new[] { concurrentHeader, Tuple.Create("User-Agent", "WA-Storage/2.0.6.1") });
            }
        }

        static void AssertRedirectDataAccount(string method, string blobUri, string expectedAccountName, IEnumerable<Tuple<string, string>> headers = null)
        {
            HandlerResult result;
            if (headers == null)
            {
                result = BlobRequest(method, blobUri);
            }
            else
            {
                result = BlobRequest(method, blobUri, headers);
            }
            Assert.AreEqual(expectedAccountName, new Uri(result.Location).Host.Split('.')[0]);
        }
    }
}
