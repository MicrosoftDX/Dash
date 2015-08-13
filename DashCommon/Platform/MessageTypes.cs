//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;

namespace Microsoft.Dash.Common.Platform
{
    public enum MessageTypes
    {
        Unknown = 0,
        BeginReplicate = 1,
        ReplicateProgress = 2,
        DeleteReplica = 3,
        UpdateService = 4,
    }
}
