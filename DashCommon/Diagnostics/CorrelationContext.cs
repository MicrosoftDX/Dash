//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;

namespace Microsoft.Dash.Common.Diagnostics
{
    public class CorrelationContext : IDisposable
    {
        Guid _previousCorrelation;

        public CorrelationContext(Guid correlationId)
        {
            _previousCorrelation = DashTrace.CorrelationId;
            DashTrace.CorrelationId = correlationId;
        }

        public void Dispose()
        {
            DashTrace.CorrelationId = _previousCorrelation;
        }
    }
}
