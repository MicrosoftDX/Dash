//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Microsoft.Dash.Server.Utils;
using Microsoft.Dash.Server.Handlers;
using System.Linq;
using System.Net;
using System.Web;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Common.Platform;
using System.Threading.Tasks;
using System.Net.Http;
using System.Xml.Linq;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Async;
using Microsoft.Dash.Common.Processors;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobReplicationTests : PipelineTestBase
    {
        const string ReplicateMetadataName  = "dash_replicate_blob";
        const string ContainerName          = "test";
        // We do both pipeline & controller invocations here, so we use both the base methods from PipelineTestBase
        // and the WebApiTestRunner instance
        WebApiTestRunner _runner;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestdata2;AccountKey=OOXSVWWpImRf79sbiEtpIwFsggv7VAhdjtKdt7o0gOLr2krzVXwZ+cb/gJeMqZRlXHTniRN6vnKKjs1glijihA==" },
                    { "ScaleoutStorage2", "DefaultEndpointsProtocol=https;AccountName=dashtestdata3;AccountKey=wXrVhqMZF/E5sJCstDUNMNG6AoVakaJHC5XkkNnAYeT/b0h9JGh7WxTEpblqGZx9pjXviHRBKBSVODhX4hGT7A==" },
                    { "WorkerQueueName", Guid.NewGuid().ToString() },
                    { "ReplicationMetadataName", ReplicateMetadataName },
                    { "ReplicationPathPattern", "^.*/test22(/.*|$)" },
                    { "LogNormalOperations", "true" }
                });
        }

        [TestCleanup]
        public void Cleanup()
        {
            var queue = new AzureMessageQueue();
            queue.DeleteQueue();
        }

        [TestMethod]
        public void ReplicateWithMetadataPutBlobPipelineTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = Guid.NewGuid().ToString();
                string blobUri = "http://localhost/blob/" + ContainerName + "/" + blobName;
                var result = BlobRequest("PUT", blobUri, new[] {
                    Tuple.Create("x-ms-blob-type", "BlockBlob"),
                    Tuple.Create("x-ms-meta-" + ReplicateMetadataName, "true"),
                    Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                    Tuple.Create("Expect", "100-Continue")
                });
                Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);
                // Cleanup - because we didn't actually create the data blob, the DELETE will fail
            }
        }

        [TestMethod]
        public void ReplicateWithMetadataPutBlobControllerTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = Guid.NewGuid().ToString();
                string blobUri = "http://localhost/blob/" + ContainerName + "/" + blobName;
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                content.Headers.Add("x-ms-meta-" + ReplicateMetadataName, "true");
                var response = _runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);
                // Cleanup 
                _runner.ExecuteRequest(blobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
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
                string blobUri = "http://localhost/blob/" + ContainerName + "/" + blobName;
                var result = BlobRequest("PUT", blobUri, new[] {
                    Tuple.Create("x-ms-blob-type", "BlockBlob"),
                    Tuple.Create("User-Agent", "WA-Storage/2.0.6.1"),
                    Tuple.Create("Expect", "100-Continue")
                });
                Assert.AreEqual(HttpStatusCode.Redirect, result.StatusCode);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);
                // Cleanup - because we didn't actually create the data blob, the DELETE will fail
            }
        }

        [TestMethod]
        public void ReplicateWithPathSpecPutBlobControllerTest()
        {
            // We need exclusive access to the queue to validate queue behavior
            lock (this)
            {
                string blobName = "test22/" + Guid.NewGuid().ToString();
                string blobUri = "http://localhost/blob/" + ContainerName + "/" + blobName;
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);
                // Cleanup 
                _runner.ExecuteRequest(blobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
                // Because the blob is not replicated yet, the delete should not enque any delete replica messages
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
                string blobUri = "http://localhost/blob/" + ContainerName + "/" + blobName;
                string blockBlobUri = blobUri + "?comp=block&blockid=";
                string blockId = GetBlockId();
                
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);
                // Put a block - shouldn't trigger a replication
                content = new StringContent("This is the next block", System.Text.Encoding.UTF8, "text/plain");
                response = _runner.ExecuteRequest(blockBlobUri + HttpUtility.UrlEncode(blockId),
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                AssertQueueIsDrained();
                // Commit the blocks - should trigger replication
                response = _runner.ExecuteRequest(blobUri + "?comp=blocklist",
                    "PUT",
                    new XDocument(
                        new XElement("BlockList",
                            new XElement("Latest", blockId)
                        )
                    ),
                    HttpStatusCode.Created);
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);
                // Set metadata - should trigger replication
                content = new StringContent(String.Empty);
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-meta-m1", "v1");
                content.Headers.Add("x-ms-meta-m2", "v2");
                _runner.ExecuteRequest(blobUri + "?comp=metadata",
                    "PUT",
                    content,
                    HttpStatusCode.OK);
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);
                // Set Blob Properties - should trigger replication
                content = new StringContent(String.Empty);
                content.Headers.Add("x-ms-blob-content-encoding", "application/csv");
                response = _runner.ExecuteRequest(blobUri + "?comp=properties",
                    "PUT",
                    content,
                    HttpStatusCode.OK);
                AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);

                // Cleanup 
                _runner.ExecuteRequest(blobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
                // Because the blob is not replicated yet, the delete should not enque any delete replica messages
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
                string blobUri = "http://localhost/blob/" + ContainerName + "/" + blobName;
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _runner.ExecuteRequest(blobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                AssertQueueIsDrained();
                // Directly manipulate the namespace blob so it appears that the blob is replicated
                var namespaceClient = DashConfiguration.NamespaceAccount.CreateCloudBlobClient();
                var nsBlob = NamespaceBlob.FetchAsync(ContainerName, blobName).Result;
                foreach (var dataAccount in DashConfiguration.DataAccounts
                                                                .Where(account => !String.Equals(account.Credentials.AccountName, nsBlob.PrimaryAccountName, StringComparison.OrdinalIgnoreCase)))
                {
                    nsBlob.DataAccounts.Add(dataAccount.Credentials.AccountName);
                }
                nsBlob.SaveAsync().Wait();
                // Now we delete - we should get a delete replica message for every data account except the primary
                _runner.ExecuteRequest(blobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
                AssertReplicationMessageIsEnqueued(MessageTypes.DeleteReplica, ContainerName, blobName, nsBlob.PrimaryAccountName);
            }
        }

        [TestMethod]
        public void ReplicateCopyBlobControllerTest()
        {
            string blobName = "test22/" + Guid.NewGuid().ToString();
            string blobUri = "http://localhost/blob/" + ContainerName + "/" + blobName;
            var response = _runner.ExecuteRequestWithHeaders(blobUri,
                "PUT",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15"),
                    Tuple.Create("x-ms-copy-source", "http://localhost/test/fixed-test.txt"),
                },
                HttpStatusCode.Accepted);
            // Fetch the blob so that we can determine it's primary data account
            var result = BlobRequest("HEAD", blobUri);
            string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
            AssertReplicationMessageIsEnqueued(MessageTypes.BeginReplicate, ContainerName, blobName, dataAccountName);
            // Cleanup 
            _runner.ExecuteRequest(blobUri,
                "DELETE",
                expectedStatusCode: HttpStatusCode.Accepted);
            // Because the blob is not replicated yet, the delete should not enque any delete replica messages
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
                string baseBlobName = "test22/" + Guid.NewGuid().ToString() + "/workernode2.jokleinhbase.d6.internal.cloudapp.net,60020,1436223739284/";
                string blobName = baseBlobName + blobSegment;
                string encodedBlobName = baseBlobName + encodedBlobSegment;
                string baseBlobUri = "http://localhost/blob/" + ContainerName + "/" + baseBlobName;
                string blobUri = baseBlobUri + blobSegment;
                string encodedBlobUri = baseBlobUri + encodedBlobSegment;
                var content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _runner.ExecuteRequest(encodedBlobUri,
                    "PUT",
                    content,
                    HttpStatusCode.Created);
                // Fetch the blob so that we can determine it's primary data account
                var result = BlobRequest("HEAD", blobUri);
                string dataAccountName = new Uri(result.Location).Host.Split('.')[0];
                // Verify that the blob is replicated by the async worker
                AssertReplicationWorker(ContainerName, blobName, dataAccountName, false);
                // Cleanup 
                _runner.ExecuteRequest(encodedBlobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
                // Verify the delete replica behavior
                AssertReplicationWorker(ContainerName, blobName, dataAccountName, true);
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
                string blobUri = "http://localhost/blob/" + ContainerName + "/" + blobName;
                var content = new StringContent(new String('b', 1024 * 1024 * 20), System.Text.Encoding.UTF8, "text/plain");
                content.Headers.Add("x-ms-version", "2013-08-15");
                content.Headers.Add("x-ms-date", "Wed, 23 Oct 2013 22:33:355 GMT");
                content.Headers.Add("x-ms-blob-type", "BlockBlob");
                var response = _runner.ExecuteRequest(blobUri,
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
                    Assert.IsTrue(BlobReplicator.BeginBlobReplication(dataAccountName, dataAccount.Credentials.AccountName, ContainerName, blobName, 0));
                }
                AssertReplicationWorker(ContainerName, blobName, dataAccountName, false);
                // Cleanup 
                _runner.ExecuteRequest(blobUri,
                    "DELETE",
                    expectedStatusCode: HttpStatusCode.Accepted);
                AssertReplicationWorker(ContainerName, blobName, dataAccountName, true);
            }
        }

        void AssertReplicationMessageIsEnqueued(MessageTypes messageType, string container, string blobName, string primaryAccount)
        {
            // Wait for the messages to be fully enqueued
            Task.Delay(1000).Wait();
            var queue = new AzureMessageQueue();
            var replicaAccounts = DashConfiguration.DataAccounts
                .Select(account => account.Credentials.AccountName)
                .Where(accountName => !String.Equals(accountName, primaryAccount, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(accountName => accountName, accountName => false, StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                var replicateMessage = queue.Dequeue();
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
                queue.DeleteCurrentMessage();
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
            var queue = new AzureMessageQueue();
            while (true)
            {
                var message = queue.Dequeue();
                if (message == null)
                {
                    break;
                }
                queue.DeleteCurrentMessage();
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
    }
}
