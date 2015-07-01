//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Diagnostics
{
    public class WebOperationRunner : OperationRunner
    {
        private WebOperationRunner() : base("", null)
        {

        }

        public static async Task<HandlerResult> DoHandlerAsync(string operation, Func<Task<HandlerResult>> action)
        {
            return await DoActionAsync(operation, action, (ex) => HandlerResult.FromException(ex));
        }
    }
}