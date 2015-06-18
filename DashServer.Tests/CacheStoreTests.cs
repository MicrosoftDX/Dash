//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.Dash.Common.Cache;
using Microsoft.Dash.Common.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Tests
{
    [TestClass]
    public class CacheStoreTests
    {
        [TestMethod]
        public void SerializeDeserializeTest()
        {
            // setup
            var testNamespaceBlob = new NamespaceBlobCache(new NamespaceBlobTests.TestNamespaceBlob
            {
                AccountName = "account-name",
                BlobName = "blob-name",
                Container = "container",
                IsMarkedForDeletion = false,
            });

            // execute
            var serialized = CacheStore.Serialize(testNamespaceBlob);
            var deserialized = CacheStore.Deserialize<NamespaceBlobCache>(serialized);

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

            // set
            Assert.IsFalse(CacheStore.ExistsAsync(key).Result);
            Assert.IsTrue(CacheStore.SetAsync(key, value, expiry).Result);
            Assert.IsTrue(CacheStore.ExistsAsync(key).Result);

            // get
            var cacheValue = CacheStore.GetAsync<string>(key).Result;
            Assert.AreEqual(value, cacheValue);

            // delete
            Assert.IsTrue(CacheStore.DeleteAsync(key).Result);
            Assert.IsFalse(CacheStore.ExistsAsync(key).Result);

            // get
            cacheValue = CacheStore.GetAsync<string>(key).Result;
            Assert.IsNull(cacheValue);
        }
    }
}