//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Common.Cache;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StackExchange.Redis;

namespace Microsoft.Tests
{
    [TestClass]
    public class CacheStoreTests
    {
        [TestMethod]
        public void SerializeDeserializeTest()
        {
            // setup
            var mockDatabase = new Mock<IDatabase>(MockBehavior.Strict);

            var testNamespaceBlob = new NamespaceBlobTests.TestNamespaceBlob
            {
                AccountName = "account-name",
                BlobName = "blob-name",
                Container = "container",
                IsMarkedForDeletion = false,
            };

            var cacheStore = new CacheStore("someRedisUrl", "someRedisPassword")
            {
                GetDatabase = () => mockDatabase.Object
            };

            // execute
            var serialized = cacheStore.Serialize(testNamespaceBlob);
            var deserialized = cacheStore.Deserialize<NamespaceBlobTests.TestNamespaceBlob>(serialized);

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