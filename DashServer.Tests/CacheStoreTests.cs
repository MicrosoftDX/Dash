//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using StackExchange.Redis;

namespace Microsoft.Tests
{
    [TestClass]
    public class CacheStoreTests
    {
        [TestMethod]
        public void SetAsyncTest()
        {
            // setup
            var mockDatabase = new Mock<IDatabase>(MockBehavior.Strict);
            mockDatabase.Setup(m => m.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Returns(Task.FromResult(true));

            var cloudBlockBlob = new CloudBlockBlob(new Uri("http://127.0.0.1:10000/container/blob"));
            var namespaceBlob = new NamespaceBlob(cloudBlockBlob);

            var cacheStore = new CacheStore(() => mockDatabase.Object);

            //var action = new Func<Task<NamespaceBlob>>(() => new Task<NamespaceBlob>(() => namespaceBlob));

            // execute
            var result = cacheStore.SetAsync("someKey", namespaceBlob, new TimeSpan(0, 0, 0, 60)).Result;

            Assert.AreEqual(result, true);
            // assert
        }
    }
}