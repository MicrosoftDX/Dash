//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.Dash.Common.Cache;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StackExchange.Redis;

namespace Microsoft.Tests
{
    [TestClass]
    public class CacheStoreTests
    {
        private const string redisUrl = "dashtest.redis.cache.windows.net";
        private const string redisKey = "TGgq1fErigjkf9Lq9zp9I/+g7hpevYfNZYBPsKoQmeM=";

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

        [TestMethod]
        public void HappyPath()
        {
            var key = Guid.NewGuid().ToString();
            var value = "unit-test-expected-value";
            var expiry = new TimeSpan(0, 1, 0);

            var cacheStore = new CacheStore(redisUrl, redisKey);

            // set
            Assert.IsFalse(cacheStore.ExistsAsync(key).Result);
            Assert.IsTrue(cacheStore.SetAsync(key, value, expiry).Result);
            Assert.IsTrue(cacheStore.ExistsAsync(key).Result);

            // get
            var cacheValue = cacheStore.GetAsync<string>(key).Result;
            Assert.AreEqual(value, cacheValue);

            // delete
            Assert.IsTrue(cacheStore.DeleteAsync(key).Result);
            Assert.IsFalse(cacheStore.ExistsAsync(key).Result);

            // get
            cacheValue = cacheStore.GetAsync<string>(key).Result;
            Assert.IsNull(cacheValue);
        }
    }
}