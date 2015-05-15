//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Cache;
using Microsoft.Dash.Common.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StackExchange.Redis;

namespace Microsoft.Tests
{
    [TestClass]
    public class CacheStoreTests
    {
        public class TestNamespaceBlob : INamespaceBlob
        {
            public string AccountName { get; set; }
            public string Container { get; set; }
            public string BlobName { get; set; }
            public bool? IsMarkedForDeletion { get; set; }
            public Task SaveAsync()
            {
                throw new NotImplementedException();
            }

            public Task<bool> ExistsAsync(bool forceRefresh = false)
            {
                throw new NotImplementedException();
            }
        }

        [TestMethod]
        public void SerializeDeserializeTest()
        {
            // setup
            var mockDatabase = new Mock<IDatabase>(MockBehavior.Strict);

            var testNamespaceBlob = new TestNamespaceBlob
            {
                AccountName = "account-name",
                BlobName = "blob-name",
                Container = "container",
                IsMarkedForDeletion = false,
            };

            var namespaceBlob = new NamespaceBlobCache(testNamespaceBlob, "data-container", "data-blobName", "data-snapshot");

            var cacheStore = new CacheStore {GetDatabase = () => mockDatabase.Object};

            // execute
            var serialized = cacheStore.Serialize(namespaceBlob);
            var deserialized = cacheStore.Deserialize<NamespaceBlobCache>(serialized);

            // assert
            Assert.IsNotNull(serialized);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(testNamespaceBlob.AccountName, deserialized.AccountName);
            Assert.AreEqual(testNamespaceBlob.BlobName, deserialized.BlobName);
            Assert.AreEqual(testNamespaceBlob.Container, deserialized.Container);
            Assert.AreEqual(testNamespaceBlob.IsMarkedForDeletion, deserialized.IsMarkedForDeletion);
        }
    }
}