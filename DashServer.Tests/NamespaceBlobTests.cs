//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        [Serializable]
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
        public void ToggleCacheFlagPropertyGetter()
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
            Assert.AreEqual(fakeCloudObj.AccountName, namespaceBlob.AccountName);
            Assert.AreEqual(fakeCloudObj.BlobName, namespaceBlob.BlobName);
            Assert.AreEqual(fakeCloudObj.Container, namespaceBlob.Container);
            Assert.AreEqual(fakeCloudObj.IsMarkedForDeletion, namespaceBlob.IsMarkedForDeletion);

            // enable
            NamespaceBlob.CacheIsEnabled = true;
            Assert.AreEqual(fakeCacheObj.AccountName, namespaceBlob.AccountName);
            Assert.AreEqual(fakeCacheObj.BlobName, namespaceBlob.BlobName);
            Assert.AreEqual(fakeCacheObj.Container, namespaceBlob.Container);
            Assert.AreEqual(fakeCacheObj.IsMarkedForDeletion, namespaceBlob.IsMarkedForDeletion);
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