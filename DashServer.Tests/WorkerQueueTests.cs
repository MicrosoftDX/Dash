//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Dash.Common.Platform;
using Newtonsoft.Json;
using Microsoft.Dash.Common.Platform.Payloads;

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
            _queue = new AzureMessageQueue(null, Guid.NewGuid().ToString());
        }

        [TestMethod]
        public void BasicQueueTest()
        {
            Dictionary<string, string> payload = new Dictionary<string, string>();
            payload.Add(ReplicatePayload.Source, "foo");
            payload.Add(ReplicatePayload.Destination, "bar");
            QueueMessage message = new QueueMessage(MessageTypes.BeginReplicate, payload);
            _queue.Enqueue(message);

            QueueMessage fromServer = _queue.Dequeue();
            Assert.AreEqual(fromServer.MessageType, MessageTypes.BeginReplicate);
            var servPayload = fromServer.Payload;
            Assert.AreEqual(servPayload[ReplicatePayload.Source], payload[ReplicatePayload.Source]);
            Assert.AreEqual(servPayload[ReplicatePayload.Destination], payload[ReplicatePayload.Destination]);
            fromServer.Delete();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _queue.DeleteQueue();
        }
    }
}
