//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;

namespace Microsoft.Tests
{
    [TestClass]
    public class NamespaceBlobTests
    {
        private const string ContainerName = "test-namespaceblobunittest";
        private static readonly CloudBlobClient CloudBlobClient = DashConfiguration.NamespaceAccount.CreateCloudBlobClient();

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            var container = CloudBlobClient.GetContainerReference(ContainerName);
            container.CreateIfNotExists();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            // delete container
            var container = CloudBlobClient.GetContainerReference(ContainerName);
            try
            {
                Trace.WriteLine("Deleting test blob container: ", ContainerName);
                container.FetchAttributes();
                container.Delete();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);

                // swallow
            }
        }

        [TestCleanup]
        public void TestCleanup() 
        {
            NamespaceBlob.CacheIsEnabled = false;
        }

        public class TestNamespaceBlob : INamespaceBlob
        {
            public string AccountName { get; set; }

            public string Container { get; set; }

            public string BlobName { get; set; }

            public bool? IsMarkedForDeletion { get; set; }
            public string PrimaryAccountName { get; set; }
            public IList<string> DataAccounts { get; private set; }
            public bool AddDataAccount(string dataAccount)
            {
                throw new NotImplementedException();
            }

            public bool RemoveDataAccount(string dataAccount)
            {
                throw new NotImplementedException();
            }

            public bool IsReplicated { get; private set; }

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
        public void ToggleCacheFlagSaveMock()
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
        public void ToggleCacheFlagPropertyGetterMock()
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
        public void ToggleCacheFlagPropertySetterMock()
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

        [TestMethod]
        public void FetchNonExistentBlob()
        {
            // setup
            NamespaceBlob.CacheIsEnabled = true;

            var containerName = Guid.NewGuid().ToString();
            var blobName = Guid.NewGuid().ToString();

            // execute
            var namespaceBlob = NamespaceBlob.FetchAsync(containerName, blobName).Result;

            // assert
            Assert.IsFalse(namespaceBlob.ExistsAsync().Result);
            Assert.IsNull(namespaceBlob.AccountName);
            Assert.IsNull(namespaceBlob.BlobName);
            Assert.IsNull(namespaceBlob.Container);
            Assert.AreEqual(false, namespaceBlob.IsMarkedForDeletion);
        }

        [TestMethod]
        public void Save()
        {
            // setup
            NamespaceBlob.CacheIsEnabled = true;

            var blobName = Guid.NewGuid().ToString();

            var expectedAccountName = Guid.NewGuid().ToString();
            var expectedBlobName = Guid.NewGuid().ToString();
            var expectedContainer = Guid.NewGuid().ToString();

            var namespaceBlob = NamespaceBlob.FetchAsync(ContainerName, blobName).Result;
            namespaceBlob.AccountName = expectedAccountName;
            namespaceBlob.BlobName = expectedBlobName;
            namespaceBlob.Container = expectedContainer;

            // execute
            namespaceBlob.SaveAsync().Wait();

            // assert
            NamespaceBlob.CacheIsEnabled = false;
            Assert.IsTrue(namespaceBlob.ExistsAsync().Result);
            Assert.AreEqual(expectedAccountName, namespaceBlob.AccountName);
            Assert.AreEqual(expectedBlobName, namespaceBlob.BlobName);
            Assert.AreEqual(expectedContainer, namespaceBlob.Container);

            NamespaceBlob.CacheIsEnabled = true;
            Assert.IsTrue(namespaceBlob.ExistsAsync().Result);
            Assert.AreEqual(expectedAccountName, namespaceBlob.AccountName);
            Assert.AreEqual(expectedBlobName, namespaceBlob.BlobName);
            Assert.AreEqual(expectedContainer, namespaceBlob.Container);
        }
    }
}