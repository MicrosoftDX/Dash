﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Common.Platform;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DashAsync
{
    class Program
    {
        static void Main(string[] args)
        {
            MessageQueue queue = new AzureMessageQueue();
            while (true)
            {
                QueueMessage payload = queue.Dequeue();
                if (payload == null)
                {
                    break;
                }
                // Right now, success/failure is indicated through a bool
                // Do we want to surround this with a try/catch and use exceptions instead?
                if (ProcessMessage(payload))
                {
                    queue.DeleteCurrentMessage();
                } //Else leave it in the queue
            }
            
        }

        static bool ProcessMessage(QueueMessage message)
        {
            var success = false;
            //TODO: Implement body of function
            switch (message.MessageType)
            {
                case MessageTypes.BeginReplicate:
                    success = DoReplicateJob(message.payload);
                    break;
                case MessageTypes.ReplicateProgress:
                    success = DoReplicateProgressJob(message.payload);
                    break;
                case MessageTypes.Unknown:
                    break;
            }
            return success;
        }

        static bool DoReplicateJob(Dictionary<string, string> payload)
        {
            //TODO
            return true;
        }

        static bool DoReplicateProgressJob(Dictionary<string, string> payload)
        {
            //TODO
            return true;
        }
    }
}
