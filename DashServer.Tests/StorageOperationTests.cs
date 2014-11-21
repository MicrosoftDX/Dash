//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using System.Collections.Generic;

namespace Microsoft.Tests
{
    [TestClass]
    public class StorageOperationTests
    {
        [TestMethod]
        public void RunStorageOperationTests()
        {
            Assert.AreEqual(StorageOperationTypes.ListContainers,           GetStorageOperation("GET",  "http://localhost/account?comp=list"));
            Assert.AreEqual(StorageOperationTypes.SetBlobServiceProperties, GetStorageOperation("PUT",  "http://localhost/account?restype=service&comp=properties"));
            Assert.AreEqual(StorageOperationTypes.GetBlobServiceProperties, GetStorageOperation("GET",  "http://localhost/account?restype=service&comp=properties"));
            Assert.AreEqual(StorageOperationTypes.GetBlobServiceStats,      GetStorageOperation("GET",  "http://localhost/account?restype=service&comp=stats"));
            Assert.AreEqual(StorageOperationTypes.CreateContainer,          GetStorageOperation("PUT",  "http://localhost/container/mycontainer?restype=container"));
            Assert.AreEqual(StorageOperationTypes.GetContainerProperties,   GetStorageOperation("GET",  "http://localhost/container/mycontainer?restype=container"));
            Assert.AreEqual(StorageOperationTypes.GetContainerProperties,   GetStorageOperation("HEAD", "http://localhost/container/mycontainer?restype=container"));
            Assert.AreEqual(StorageOperationTypes.GetContainerMetadata,     GetStorageOperation("GET",  "http://localhost/container/mycontainer?restype=container&comp=metadata"));
            Assert.AreEqual(StorageOperationTypes.GetContainerMetadata,     GetStorageOperation("HEAD", "http://localhost/container/mycontainer?restype=container&comp=metadata"));
            Assert.AreEqual(StorageOperationTypes.SetContainerMetadata,     GetStorageOperation("PUT",  "http://localhost/container/mycontainer?restype=container&comp=metadata"));
            Assert.AreEqual(StorageOperationTypes.GetContainerACL,          GetStorageOperation("GET",  "http://localhost/container/mycontainer?restype=container&comp=acl"));
            Assert.AreEqual(StorageOperationTypes.GetContainerACL,          GetStorageOperation("HEAD", "http://localhost/container/mycontainer?restype=container&comp=acl"));
            Assert.AreEqual(StorageOperationTypes.SetContainerACL,          GetStorageOperation("PUT",  "http://localhost/container/mycontainer?restype=container&comp=acl"));
            Assert.AreEqual(StorageOperationTypes.LeaseContainer,           GetStorageOperation("PUT",  "http://localhost/container/mycontainer?restype=container&comp=lease"));
            Assert.AreEqual(StorageOperationTypes.DeleteContainer,          GetStorageOperation("DELETE","http://localhost/container/mycontainer?restype=container"));
            Assert.AreEqual(StorageOperationTypes.ListBlobs,                GetStorageOperation("GET",  "http://localhost/container/mycontainer?restype=container&comp=list"));
            Assert.AreEqual(StorageOperationTypes.PutBlob,                  GetStorageOperation("PUT",  "http://localhost/blob/myblob/myblob"));
            Assert.AreEqual(StorageOperationTypes.GetBlob,                  GetStorageOperation("GET",  "http://localhost/blob/myblob/myblob"));
            Assert.AreEqual(StorageOperationTypes.GetBlobProperties,        GetStorageOperation("HEAD", "http://localhost/blob/myblob/myblob"));
            Assert.AreEqual(StorageOperationTypes.SetBlobProperties,        GetStorageOperation("PUT",  "http://localhost/blob/myblob/myblob?comp=properties"));
            Assert.AreEqual(StorageOperationTypes.GetBlobMetadata,          GetStorageOperation("GET",  "http://localhost/blob/myblob/myblob?comp=metadata"));
            Assert.AreEqual(StorageOperationTypes.GetBlobMetadata,          GetStorageOperation("HEAD", "http://localhost/blob/myblob/myblob?comp=metadata"));
            Assert.AreEqual(StorageOperationTypes.SetBlobMetadata,          GetStorageOperation("PUT",  "http://localhost/blob/myblob/myblob?comp=metadata"));
            Assert.AreEqual(StorageOperationTypes.DeleteBlob,               GetStorageOperation("DELETE","http://localhost/blob/myblob/myblob"));
            Assert.AreEqual(StorageOperationTypes.LeaseBlob,                GetStorageOperation("PUT",  "http://localhost/blob/myblob/myblob?comp=lease"));
            Assert.AreEqual(StorageOperationTypes.CopyBlob,                 GetStorageOperation("PUT",  "http://localhost/blob/myblob/myblob", new[] { Tuple.Create("x-ms-copy-source", "http://localhost/blob/myblob/myblob2") }));
            Assert.AreEqual(StorageOperationTypes.AbortCopyBlob,            GetStorageOperation("PUT",  "http://localhost/blob/myblob/myblob?comp=copy"));
            Assert.AreEqual(StorageOperationTypes.PutBlock,                 GetStorageOperation("PUT", "http://localhost/blob/myblob/myblob?comp=block"));
            Assert.AreEqual(StorageOperationTypes.PutBlockList,             GetStorageOperation("PUT", "http://localhost/blob/myblob/myblob?comp=blocklist"));
            Assert.AreEqual(StorageOperationTypes.GetBlockList,             GetStorageOperation("GET", "http://localhost/blob/myblob/myblob?comp=blocklist"));
        }

        StorageOperationTypes GetStorageOperation(string method, string uri, IEnumerable<Tuple<string, string>> headers = null)
        {
            var request = new MockHttpRequestWrapper
            {
                HttpMethod = method,
                Url = new Uri(uri),
            };            
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Item1, header.Item2);
                }
            }
            return StorageOperations.GetBlobOperation(request);
        }

    }
}
