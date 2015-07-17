//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Async
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!Trace.Listeners.OfType<ConsoleTraceListener>().Any())
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
            }
            AzureUtils.AddAzureDiagnosticsListener();
            DashTrace.TraceInformation("DashAsync (version: {0}): Asynchronous worker starting up.", Assembly.GetEntryAssembly().GetName().Version);

            int msgProcessed = 0, msgErrors = 0;
            MessageProcessor.ProcessMessageLoop(ref msgProcessed, ref msgErrors);
            DashTrace.TraceInformation("DashAsync completed. Messages processed [{0}], messages unprocessed [{1}]", msgProcessed, msgErrors);
        }
    }
}
