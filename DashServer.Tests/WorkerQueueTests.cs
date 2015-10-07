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
    public class WorkerQueueTests : DashTestBase
    {
        static DashTestContext _ctx;
        static AzureMessageQueue _queue;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ctx = InitializeConfig(ctx, "datax2", new Dictionary<string, string>
                {
                    { "AccountName", "dashtest" },
                });
            _queue = new AzureMessageQueue(Guid.NewGuid().ToString());
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _queue.DeleteQueue();
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
            _queue.DeleteCurrentMessage();
        }
    }
}
