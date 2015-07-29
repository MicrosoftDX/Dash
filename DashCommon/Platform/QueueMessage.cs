﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Platform
{
    //Represents a payload of a queue message
    public class QueueMessage
    {
        public QueueMessage()
        {
            this.Payload = new Dictionary<string, string>();
        }

        public QueueMessage(MessageTypes type, Dictionary<string, string> payload, Guid? correlationId = null)
        {
            this.MessageType = type;
            this.Payload = payload;
            if (correlationId.HasValue && correlationId.Value != Guid.Empty)
            {
                this.CorrelationId = correlationId.Value;
            }
        }

        public MessageTypes MessageType { get; set; }
        public Guid? CorrelationId { get; set; }
        public IDictionary<string, string> Payload { get; private set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override string ToString()
        {
            return ToJson();
        }
    }
}