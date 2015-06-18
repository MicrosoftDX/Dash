//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Dash.Common.Utils;
using Newtonsoft.Json;

namespace Microsoft.Tests
{
    [TestClass]
    public class WorkerQueueTests
    {
        WebApiTestRunner _runner;
        AzureMessageQueue _queue;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>()
                {
                    { "AccountName", "dashtest" },
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestdata2;AccountKey=OOXSVWWpImRf79sbiEtpIwFsggv7VAhdjtKdt7o0gOLr2krzVXwZ+cb/gJeMqZRlXHTniRN6vnKKjs1glijihA==" },
                    { "ScaleoutNumberOfAccounts", "2"},
                });
            _queue = new AzureMessageQueue(Guid.NewGuid().ToString());
        }

        [TestMethod]
        public void BasicQueueTest()
        {
            ReplicatePayload payload = new ReplicatePayload("foo", "bar");
            QueueMessage message = new QueueMessage(MessageTypes.BeginReplicate, payload);
            _queue.Enqueue(message);

            QueueMessage fromServer = _queue.Dequeue();
            Assert.AreEqual(fromServer.MessageType, MessageTypes.BeginReplicate);
            ReplicatePayload servPayload = JsonConvert.DeserializeObject<ReplicatePayload>(fromServer.payload);
            Assert.AreEqual(servPayload.Source, payload.Source);
            Assert.AreEqual(servPayload.Destination, payload.Destination);
            _queue.DeleteCurrentMessage();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _queue.DeleteQueue();
        }
    }
}
