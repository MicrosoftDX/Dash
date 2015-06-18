//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.Dash.Common.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.Tests
{
    [TestClass]
    public class NamespaceBlobCacheTests
    {
        private Action cleanAction;
        
        [TestInitialize]
        public void Initialize()
        {
            cleanAction = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (cleanAction != null)
            {
                try
                {
                    cleanAction();
                }
                catch
                {
                   // swallow 
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof (ArgumentNullException))]
        public void NullCtor()
        {
            new NamespaceBlobCache(null);
        }

        [TestMethod]
        public void PropertyInitialization()
        {
            // setup
            var expectedAccountName = Guid.NewGuid().ToString();
            var expectedBlobName = Guid.NewGuid().ToString();
            var expectedContainer = Guid.NewGuid().ToString();
            var expectedIsMarkedForDeletion = false;

            var mockNamespaceBlobCloud = new Mock<NamespaceBlobCloud>(null);
            mockNamespaceBlobCloud.Setup(c => c.AccountName).Returns(expectedAccountName);
            mockNamespaceBlobCloud.Setup(c => c.BlobName).Returns(expectedBlobName);
            mockNamespaceBlobCloud.Setup(c => c.Container).Returns(expectedContainer);
            mockNamespaceBlobCloud.Setup(c => c.IsMarkedForDeletion).Returns(expectedIsMarkedForDeletion);

            // execute
            var namespaceBlobCache = new NamespaceBlobCache(mockNamespaceBlobCloud.Object);

            // assert
            Assert.AreEqual(expectedAccountName, namespaceBlobCache.AccountName);
            Assert.AreEqual(expectedBlobName, namespaceBlobCache.BlobName);
            Assert.AreEqual(expectedContainer, namespaceBlobCache.Container);
            Assert.AreEqual(expectedIsMarkedForDeletion, namespaceBlobCache.IsMarkedForDeletion);
        }

        [TestMethod]
        public void EmptyCache()
        {
            // setup
            var containerName = Guid.NewGuid().ToString();
            var blobName = Guid.NewGuid().ToString();

            var expectedAccountName = Guid.NewGuid().ToString();
            var expectedBlobName = Guid.NewGuid().ToString();
            var expectedContainer = Guid.NewGuid().ToString();

            var mockNamespaceBlobCloud = new Mock<NamespaceBlobCloud>(null);
            mockNamespaceBlobCloud.Setup(c => c.AccountName).Returns(expectedAccountName);
            mockNamespaceBlobCloud.Setup(c => c.BlobName).Returns(expectedBlobName);
            mockNamespaceBlobCloud.Setup(c => c.Container).Returns(expectedContainer);
            mockNamespaceBlobCloud.Setup(c => c.IsMarkedForDeletion).Returns(false);

            // execute
            var namespaceBlobCache = new NamespaceBlobCache(mockNamespaceBlobCloud.Object);

            // assert
            Assert.IsFalse(namespaceBlobCache.ExistsAsync(false).Result);
            Assert.IsNull(NamespaceBlobCache.FetchAsync(containerName, blobName).Result); 
        }

        [TestMethod]
        public void SaveFetchDelete()
        {
            // setup
            var expectedAccountName = Guid.NewGuid().ToString();
            var expectedBlobName = Guid.NewGuid().ToString();
            var expectedContainer = Guid.NewGuid().ToString();

            var mockNamespaceBlobCloud = new Mock<NamespaceBlobCloud>(null);
            mockNamespaceBlobCloud.Setup(c => c.AccountName).Returns(expectedAccountName);
            mockNamespaceBlobCloud.Setup(c => c.Container).Returns(expectedContainer);
            mockNamespaceBlobCloud.Setup(c => c.BlobName).Returns(expectedBlobName);
            mockNamespaceBlobCloud.Setup(c => c.IsMarkedForDeletion).Returns(false);

            var namespaceBlobCache = new NamespaceBlobCache(mockNamespaceBlobCloud.Object);
            cleanAction = () => namespaceBlobCache.DeleteAsync().Wait();

            Assert.AreEqual(expectedAccountName, namespaceBlobCache.AccountName);
            Assert.AreEqual(expectedBlobName, namespaceBlobCache.BlobName);
            Assert.AreEqual(expectedContainer, namespaceBlobCache.Container);

            // save
            namespaceBlobCache.SaveAsync().Wait();
            Assert.IsTrue(namespaceBlobCache.ExistsAsync(false).Result);

            // fetch
            var cached = NamespaceBlobCache.FetchAsync(expectedContainer, expectedBlobName).Result;
            Assert.IsNotNull(cached);
            Assert.AreEqual(expectedAccountName, cached.AccountName);
            Assert.AreEqual(expectedBlobName, cached.BlobName);
            Assert.AreEqual(expectedContainer, cached.Container);

            // delete
            Assert.IsTrue(namespaceBlobCache.DeleteAsync().Result);
            Assert.IsFalse(namespaceBlobCache.ExistsAsync(false).Result);
        }
    }
}