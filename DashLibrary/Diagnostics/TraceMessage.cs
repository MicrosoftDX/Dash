//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;

namespace Microsoft.Dash.Common.Diagnostics
{
    public class TraceMessage
    {
        public TraceMessage()
        {
            this.Time = DateTime.UtcNow;
        }

        public DateTime Time { get; set; }
        public string Operation { get; set; }
        public bool? Success { get; set; }
        public long? Duration { get; set; }
        public string Message { get; set; }
        public DashErrorInformation ErrorDetails { get; set; }
    }
}