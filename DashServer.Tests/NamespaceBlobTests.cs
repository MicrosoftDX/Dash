//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.Tests
{
    [TestClass]
    public class NamespaceBlobTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            NamespaceBlob.CacheIsEnabled = false;
        }

        [DataContract]
        public class TestNamespaceBlob : INamespaceBlob
        {
            [DataMember]
            public string AccountName { get; set; }

            [DataMember]
            public string Container { get; set; }

            [DataMember]
            public string BlobName { get; set; }

            [DataMember]
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
        public void SaveTest()
        {
            // setup
            var mockNamespaceCloudBlob = new Mock<INamespaceBlob>(MockBehavior.Strict);
            mockNamespaceCloudBlob.Setup(s => s.SaveAsync()).Returns(() => Task.FromResult(true));

            var mockNamespaceCacheBlob = new Mock<INamespaceBlob>(MockBehavior.Strict);
            mockNamespaceCacheBlob.Setup(s => s.SaveAsync()).Returns(() => Task.FromResult(true));

            var namespaceBlob = new NamespaceBlob(mockNamespaceCacheBlob.Object, mockNamespaceCloudBlob.Object);

            // disabled cache
            //      execute
            NamespaceBlob.CacheIsEnabled = false;
            namespaceBlob.SaveAsync().Wait();

            //      assert
            mockNamespaceCloudBlob.Verify(s => s.SaveAsync(), Times.Once);
            mockNamespaceCacheBlob.Verify(s => s.SaveAsync(), Times.Never);

            // enable cache
            //      execute
            NamespaceBlob.CacheIsEnabled = true;
            namespaceBlob.SaveAsync().Wait();

            //      assert
            mockNamespaceCloudBlob.Verify(s => s.SaveAsync(), Times.Exactly(2));
            mockNamespaceCacheBlob.Verify(s => s.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public void ToggleCacheFlagPropertyGetter()
        {
            var mockNamespaceCloudBlob = new Mock<INamespaceBlob>(MockBehavior.Strict);
            mockNamespaceCloudBlob.SetupAllProperties();
            mockNamespaceCloudBlob.Object.AccountName = "cloud-account-name";
            mockNamespaceCloudBlob.Object.BlobName = "cloud-blob-name";
            mockNamespaceCloudBlob.Object.Container = "cloud-container";
            mockNamespaceCloudBlob.Object.IsMarkedForDeletion = false;

            var mockNamespaceCacheBlob = new Mock<INamespaceBlob>(MockBehavior.Strict);
            mockNamespaceCacheBlob.SetupAllProperties();
            mockNamespaceCacheBlob.Object.AccountName = "cache-account-name";
            mockNamespaceCacheBlob.Object.BlobName = "cache-blob-name";
            mockNamespaceCacheBlob.Object.Container = "cache-container";
            mockNamespaceCacheBlob.Object.IsMarkedForDeletion = true;

            var namespaceBlob = new NamespaceBlob(mockNamespaceCacheBlob.Object, mockNamespaceCloudBlob.Object);

            // disable - values should be from cloud
            NamespaceBlob.CacheIsEnabled = false;
            Assert.AreEqual(mockNamespaceCloudBlob.Object.AccountName, namespaceBlob.AccountName);
            Assert.AreEqual(mockNamespaceCloudBlob.Object.BlobName, namespaceBlob.BlobName);
            Assert.AreEqual(mockNamespaceCloudBlob.Object.Container, namespaceBlob.Container);
            Assert.AreEqual(mockNamespaceCloudBlob.Object.IsMarkedForDeletion, namespaceBlob.IsMarkedForDeletion);

            // enable - values should be from cache
            NamespaceBlob.CacheIsEnabled = true;
            Assert.AreEqual(mockNamespaceCacheBlob.Object.AccountName, namespaceBlob.AccountName);
            Assert.AreEqual(mockNamespaceCacheBlob.Object.BlobName, namespaceBlob.BlobName);
            Assert.AreEqual(mockNamespaceCacheBlob.Object.Container, namespaceBlob.Container);
            Assert.AreEqual(mockNamespaceCacheBlob.Object.IsMarkedForDeletion, namespaceBlob.IsMarkedForDeletion);
        }

        [TestMethod]
        public void ToggleCacheFlagPropertySetter()
        {
            var fakeCloudObj = new TestNamespaceBlob
            {
                AccountName = "cloud-account-name",
                BlobName = "cloud-blob-name",
                Container = "cloud-container",
                IsMarkedForDeletion = false,
            };

            var fakeCacheObj = new TestNamespaceBlob
            {
                AccountName = "cache-account-name",
                BlobName = "cloud-blob-name",
                Container = "cloud-container",
                IsMarkedForDeletion = true,
            };

            var namespaceBlob = new NamespaceBlob(fakeCacheObj, fakeCloudObj);

            // disable
            NamespaceBlob.CacheIsEnabled = false;

            var accountName = Guid.NewGuid().ToString();
            var blobName = Guid.NewGuid().ToString();
            var container = Guid.NewGuid().ToString();

            namespaceBlob.AccountName = accountName;
            namespaceBlob.BlobName = blobName;
            namespaceBlob.Container = container;
            namespaceBlob.IsMarkedForDeletion = true;

            Assert.AreEqual(accountName, fakeCloudObj.AccountName);
            Assert.AreEqual(blobName, fakeCloudObj.BlobName);
            Assert.AreEqual(container, fakeCloudObj.Container);
            Assert.AreEqual(true, fakeCloudObj.IsMarkedForDeletion);

            // enable
            NamespaceBlob.CacheIsEnabled = true;

            accountName = Guid.NewGuid().ToString();
            blobName = Guid.NewGuid().ToString();
            container = Guid.NewGuid().ToString();

            namespaceBlob.AccountName = accountName;
            namespaceBlob.BlobName = blobName;
            namespaceBlob.Container = container;
            namespaceBlob.IsMarkedForDeletion = true;

            Assert.AreEqual(fakeCacheObj.AccountName, fakeCloudObj.AccountName);
            Assert.AreEqual(fakeCacheObj.BlobName, fakeCloudObj.BlobName);
            Assert.AreEqual(fakeCacheObj.Container, fakeCloudObj.Container);
            Assert.AreEqual(fakeCacheObj.IsMarkedForDeletion, fakeCloudObj.IsMarkedForDeletion);
        }
    }
}