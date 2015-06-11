//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.Dash.Common.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Tests
{
    [TestClass]
    public class NamespaceBlobCloudTests
    {
        private readonly CloudStorageAccount _testAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==");

        [TestMethod]
        public void GetNonExistentBlob()
        {
            // setup
            var container = Guid.NewGuid().ToString();
            var blobName = Guid.NewGuid().ToString();

            // execute
            var cloudNamespaceBlob = new NamespaceBlobCloud(() => (CloudBlockBlob)NamespaceHandler.GetBlobByName(_testAccount, container, blobName));

            // assert
            Assert.IsNotNull(cloudNamespaceBlob);
            Assert.IsNull(cloudNamespaceBlob.AccountName);
            Assert.IsNull(cloudNamespaceBlob.BlobName);
            Assert.IsNull(cloudNamespaceBlob.Container);
            Assert.AreEqual(false, cloudNamespaceBlob.IsMarkedForDeletion);
            Assert.IsFalse(cloudNamespaceBlob.ExistsAsync().Result);
        }
    }
}